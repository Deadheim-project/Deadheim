using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Aesir 35 -- Transmog: o VIP troca so a APARENCIA de um item de armadura/capa, o item real
    /// continua dando defesa, durabilidade e tudo mais (nada disso e tocado).
    ///
    /// Como funciona: todo cliente desenha o equipamento de qualquer jogador em
    /// VisEquipment.UpdateEquipmentVisuals, que le os hashes REAIS da ZDO e chama
    /// Set{Helmet,Chest,Leg,Shoulder}Equipped(hash). Um prefix nesses metodos troca so o hash
    /// argumento pelo da aparencia escolhida, lida do mapa "dh_transmog" na ZDO do jogador
    /// desenhado ("real>aparencia;..." com nomes de prefab). A ZDO real nao muda.
    ///
    /// Servidor (autoridade): arquivo transmog-&lt;mundo&gt;.txt com o que cada jogador DESBLOQUEOU
    /// (o servidor ve o item realmente equipado na ZDO do jogador) e o que ESCOLHEU. Cada escolha
    /// e revalidada: VIP, as duas pecas desbloqueadas, mesmo slot, fora da lista negra e (opcao)
    /// mesma classe de peso. Sem VIP o cliente nao publica o mapa e todos veem o visual real.
    /// Em arena e em zona de raid o prefix nao troca nada (config).
    /// </summary>
    [HarmonyPatch]
    internal static class Transmog
    {
        private const string ToServer = "DH_Transmog_ToServer";
        private const string ToClient = "DH_Transmog_ToClient";
        internal const string ZdoKey = "dh_transmog";

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> SameWeightClass;
        public static ConfigEntry<bool> DisabledInArena;
        public static ConfigEntry<bool> DisabledInRaidZone;
        public static ConfigEntry<string> Blacklist;
        public static ConfigEntry<int> MaxChoices;

        internal static void BindConfigs(ConfigFile config)
        {
            Enabled = Plugin.Synced(config.Bind("Transmog", "Enabled", true,
                new ConfigDescription("Habilita o transmog (so aparencia) para VIPs.")));
            SameWeightClass = Plugin.Synced(config.Bind("Transmog", "SameWeightClass", true,
                new ConfigDescription("Se verdadeiro, so troca a aparencia entre pecas da mesma classe de peso (pesada = movimento reduzido). Falso = livre dentro do slot.")));
            DisabledInArena = Plugin.Synced(config.Bind("Transmog", "DisabledInArena", true,
                new ConfigDescription("Em arena o visual real e mostrado (sem transmog).")));
            DisabledInRaidZone = Plugin.Synced(config.Bind("Transmog", "DisabledInRaidZone", true,
                new ConfigDescription("Dentro de zona de raid o visual real e mostrado (sem transmog).")));
            Blacklist = Plugin.Synced(config.Bind("Transmog", "Blacklist", "",
                new ConfigDescription("Prefabs (separados por virgula) que nunca podem ser aparencia nem alvo, ex.: itens de chefe/NPC.")));
            MaxChoices = Plugin.Synced(config.Bind("Transmog", "MaxChoices", 40,
                new ConfigDescription("Maximo de trocas ativas por jogador.")));
        }

        // ------------------------------------------------------------------ regras (compartilhadas)

        internal static bool IsSlotType(ItemDrop.ItemData.ItemType t) =>
            t == ItemDrop.ItemData.ItemType.Helmet || t == ItemDrop.ItemData.ItemType.Chest ||
            t == ItemDrop.ItemData.ItemType.Legs || t == ItemDrop.ItemData.ItemType.Shoulder;

        private static HashSet<string> BlacklistSet()
        {
            string raw = Blacklist?.Value ?? "";
            return new HashSet<string>(raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
        }

        internal static ItemDrop.ItemData.SharedData SharedOf(string prefab)
        {
            if (string.IsNullOrEmpty(prefab) || ObjectDB.instance == null) return null;
            GameObject go = ObjectDB.instance.GetItemPrefab(prefab);
            return go != null ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
        }

        private static bool Heavy(ItemDrop.ItemData.SharedData s) => s.m_movementModifier < 0f;

        /// <summary>Peca elegivel (desbloqueio e aparencia): armadura de slot, nao proibida.</summary>
        internal static bool Eligible(string prefab)
        {
            if (string.IsNullOrEmpty(prefab) || BlacklistSet().Contains(prefab)) return false;
            ItemDrop.ItemData.SharedData s = SharedOf(prefab);
            return s != null && IsSlotType(s.m_itemType) && !string.IsNullOrEmpty(s.m_name);
        }

        internal static bool Compatible(string real, string app, out string why)
        {
            why = null;
            if (!Eligible(real) || !Eligible(app)) { why = "Item nao permitido para transmog."; return false; }
            ItemDrop.ItemData.SharedData a = SharedOf(real), b = SharedOf(app);
            if (a.m_itemType != b.m_itemType) { why = "A aparencia precisa ser do mesmo slot."; return false; }
            if (SameWeightClass.Value && Heavy(a) != Heavy(b)) { why = "A aparencia precisa ser da mesma classe de peso."; return false; }
            return true;
        }

        // ------------------------------------------------------------------ servidor: armazenamento

        private sealed class Data
        {
            public readonly HashSet<string> Unlocked = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> Chosen = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private static Dictionary<long, Data> _data;
        private static string _path;

        private static string WorldFile()
        {
            string world = ZNet.instance != null ? ZNet.instance.GetWorldName() : "world";
            foreach (char c in Path.GetInvalidFileNameChars()) world = world.Replace(c, '_');
            return Path.Combine(Path.Combine(Paths.ConfigPath, "Deadheim"), "transmog-" + world + ".txt");
        }

        private static Data DataOf(long playerId)
        {
            string path = WorldFile();
            if (_data == null || path != _path)
            {
                _path = path;
                _data = new Dictionary<long, Data>();
                try
                {
                    if (File.Exists(path))
                        foreach (string linha in File.ReadAllLines(path))
                        {
                            string[] c = linha.Split('\t');
                            if (c.Length < 3 || !long.TryParse(c[0], out long id)) continue;
                            Data d = new Data();
                            foreach (string u in c[1].Split(',')) if (u.Length > 0) d.Unlocked.Add(u);
                            foreach (string p in c[2].Split(';'))
                            {
                                int i = p.IndexOf('>');
                                if (i > 0 && i < p.Length - 1) d.Chosen[p.Substring(0, i)] = p.Substring(i + 1);
                            }
                            _data[id] = d;
                        }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Deadheim] Transmog: {path} ilegivel ({ex.Message}); comecando vazio.");
                    _data = new Dictionary<long, Data>();
                }
            }
            if (!_data.TryGetValue(playerId, out Data data)) _data[playerId] = data = new Data();
            return data;
        }

        private static void Save()
        {
            if (_data == null || string.IsNullOrEmpty(_path)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                IEnumerable<string> linhas = _data.Where(kv => kv.Value.Unlocked.Count > 0)
                    .Select(kv => kv.Key + "\t" + string.Join(",", kv.Value.Unlocked) + "\t" + Encode(kv.Value.Chosen));
                string temp = _path + ".tmp";
                File.WriteAllLines(temp, linhas);
                File.Copy(temp, _path, true); // Mono/Linux da DatHost: sem File.Replace
                File.Delete(temp);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Transmog: nao foi possivel salvar " + _path + ": " + ex.Message);
            }
        }

        private static string Encode(Dictionary<string, string> map) =>
            string.Join(";", map.Select(kv => kv.Key + ">" + kv.Value));

        private static bool IsVipPeer(long peerId)
        {
            if (ZRoutedRpc.instance != null && peerId == ZRoutedRpc.instance.m_id)
                return VipList.VipListApi.IsLocalPlayerVip();
            string host = ZNet.instance?.GetPeer(peerId)?.m_socket?.GetHostName();
            return VipList.VipListApi.IsVip(host);
        }

        private static void SendState(long peerId, long playerId)
        {
            if (ZRoutedRpc.instance == null) return;
            Data d = DataOf(playerId);
            ZPackage pkg = new ZPackage();
            pkg.Write("estado");
            pkg.Write(string.Join(",", d.Unlocked));
            pkg.Write(Encode(d.Chosen));
            ZRoutedRpc.instance.InvokeRoutedRPC(peerId, ToClient, pkg);
        }

        // ------------------------------------------------------------------ servidor: desbloqueio

        private static Dictionary<int, string> _hashToName;

        private static string NameOfHash(int hash)
        {
            if (hash == 0 || ObjectDB.instance == null) return null;
            if (_hashToName == null || _hashToName.Count == 0)
            {
                _hashToName = new Dictionary<int, string>();
                foreach (GameObject go in ObjectDB.instance.m_items)
                    if (go != null) _hashToName[go.name.GetStableHashCode()] = go.name;
            }
            return _hashToName.TryGetValue(hash, out string n) ? n : null;
        }

        private static float _scanTimer;

        /// <summary>Servidor, a cada 5 s: o que o jogador realmente veste (ZDO) vira desbloqueado.</summary>
        private static void ServerScan()
        {
            bool changed = false;
            foreach (Pvp.PvpPeer peer in Pvp.PvpPeer.Online())
            {
                ZDO zdo = ZDOMan.instance?.GetZDO(peer.CharacterId);
                if (zdo == null) continue;
                Data d = DataOf(peer.PlayerId);
                bool novo = false;
                foreach (int hash in new[] { zdo.GetInt(ZDOVars.s_helmetItem), zdo.GetInt(ZDOVars.s_chestItem),
                                             zdo.GetInt(ZDOVars.s_legItem), zdo.GetInt(ZDOVars.s_shoulderItem) })
                {
                    string name = NameOfHash(hash);
                    if (name != null && Eligible(name) && d.Unlocked.Add(name)) novo = true;
                }
                if (novo) { changed = true; SendState(peer.PeerId, peer.PlayerId); }
            }
            if (changed) Save();
        }

        [HarmonyPatch(typeof(ZNet), "Update")]
        private static class ServerTickPatch
        {
            private static void Postfix(ZNet __instance)
            {
                if (!__instance.IsServer() || Enabled == null || !Enabled.Value) return;
                _scanTimer += Time.unscaledDeltaTime;
                if (_scanTimer < 5f) return;
                _scanTimer = 0f;
                try { ServerScan(); }
                catch (Exception ex) { Debug.LogError("[Deadheim] Transmog: varredura falhou: " + ex); }
            }
        }

        // ------------------------------------------------------------------ rede

        [HarmonyPatch(typeof(Game), "Start")]
        private static class RegisterPatch
        {
            private static void Postfix()
            {
                if (ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.Register<ZPackage>(ToServer, OnToServer);
                ZRoutedRpc.instance.Register<ZPackage>(ToClient, OnToClient);
            }
        }

        private static void OnToServer(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || Enabled == null || !Enabled.Value) return;
            long real = RemetenteRpc.Real(sender, out _);
            if (real == 0L || !Pvp.PvpPeer.TryResolve(real, out Pvp.PvpPeer peer)) return;
            try
            {
                string op = pkg.ReadString();
                Data d = DataOf(peer.PlayerId);
                if (op == "hello") { SendState(real, peer.PlayerId); return; }

                if (op == "set")
                {
                    string item = pkg.ReadString(), app = pkg.ReadString();
                    if (!IsVipPeer(real)) { Pvp.PvpNet.Message(real, "Transmog e exclusivo Aesir (VIP)."); return; }
                    if (!d.Unlocked.Contains(item) || !d.Unlocked.Contains(app))
                    { Pvp.PvpNet.Message(real, "Voce ainda nao vestiu essa peca (desbloqueia ao equipar)."); return; }
                    if (!Compatible(item, app, out string why)) { Pvp.PvpNet.Message(real, why); return; }
                    if (item == app) d.Chosen.Remove(item);
                    else
                    {
                        if (!d.Chosen.ContainsKey(item) && d.Chosen.Count >= MaxChoices.Value)
                        { Pvp.PvpNet.Message(real, "Limite de trocas ativas atingido."); return; }
                        d.Chosen[item] = app;
                    }
                    Save();
                    SendState(real, peer.PlayerId);
                    Debug.Log($"[Deadheim] Transmog: {peer.Name} {item} -> {app}");
                }
                else if (op == "off")
                {
                    string item = pkg.ReadString();
                    if (item == "*") d.Chosen.Clear(); else d.Chosen.Remove(item);
                    Save();
                    SendState(real, peer.PlayerId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Transmog: pacote do cliente " + real + " falhou: " + ex);
            }
        }

        // ------------------------------------------------------------------ cliente: estado

        internal static readonly HashSet<string> LocalUnlocked = new HashSet<string>(StringComparer.Ordinal);
        internal static readonly Dictionary<string, string> LocalChosen = new Dictionary<string, string>(StringComparer.Ordinal);

        private static void OnToClient(long sender, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID()) return;
            try
            {
                if (pkg.ReadString() != "estado") return;
                string unlocked = pkg.ReadString() ?? "", chosen = pkg.ReadString() ?? "";
                LocalUnlocked.Clear();
                foreach (string u in unlocked.Split(',')) if (u.Length > 0) LocalUnlocked.Add(u);
                LocalChosen.Clear();
                foreach (string p in chosen.Split(';'))
                {
                    int i = p.IndexOf('>');
                    if (i > 0 && i < p.Length - 1) LocalChosen[p.Substring(0, i)] = p.Substring(i + 1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Transmog: pacote do servidor falhou: " + ex);
            }
        }

        internal static void SendSet(string item, string app) => Send("set", item, app);
        internal static void SendOff(string item) => Send("off", item);

        private static void Send(params string[] fields)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            foreach (string f in fields) pkg.Write(f ?? "");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), ToServer, pkg);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class SpawnPatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                LocalUnlocked.Clear();
                LocalChosen.Clear();
                Send("hello");
            }
        }

        // Publica o mapa na ZDO do jogador local (so VIP e so se habilitado), 1x por segundo.
        private static float _pubTimer;

        [HarmonyPatch(typeof(Player), "Update")]
        private static class PublishPatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer || __instance.m_nview == null || !__instance.m_nview.IsValid()) return;
                _pubTimer += Time.deltaTime;
                if (_pubTimer < 1f) return;
                _pubTimer = 0f;
                string want = "";
                try
                {
                    if (Enabled != null && Enabled.Value && VipList.VipListApi.IsLocalPlayerVip())
                        want = Encode(LocalChosen);
                }
                catch { want = ""; }
                ZDO zdo = __instance.m_nview.GetZDO();
                if (zdo.GetString(ZdoKey, "") != want) zdo.Set(ZdoKey, want);
            }
        }

        // ------------------------------------------------------------------ cliente: desenho

        private static readonly Dictionary<string, Dictionary<int, int>> _parsed = new Dictionary<string, Dictionary<int, int>>();

        private static Dictionary<int, int> Parse(string raw)
        {
            if (_parsed.TryGetValue(raw, out Dictionary<int, int> m)) return m;
            if (_parsed.Count > 256) _parsed.Clear();
            m = new Dictionary<int, int>();
            foreach (string p in raw.Split(';'))
            {
                int i = p.IndexOf('>');
                if (i > 0 && i < p.Length - 1)
                    m[p.Substring(0, i).GetStableHashCode()] = p.Substring(i + 1).GetStableHashCode();
            }
            return _parsed[raw] = m;
        }

        private static bool Disabled(Vector3 pos)
        {
            if (Enabled != null && !Enabled.Value) return true;
            if (DisabledInArena.Value && Pvp.PvpZones.IsArena(pos)) return true;
            if (DisabledInRaidZone.Value && Wards.WardBridge.Governed(pos)) return true;
            return false;
        }

        /// <summary>Hash da aparencia para o item real nesse VisEquipment (ou o proprio hash).</summary>
        internal static int Swap(VisEquipment vis, int hash)
        {
            if (hash == 0 || vis == null || !vis.m_isPlayer || vis.m_nview == null) return hash;
            ZDO zdo = vis.m_nview.GetZDO();
            if (zdo == null) return hash;
            string raw = zdo.GetString(ZdoKey, "");
            if (raw.Length == 0) return hash;
            if (!Parse(raw).TryGetValue(hash, out int app)) return hash;
            return Disabled(vis.transform.position) ? hash : app;
        }

        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetHelmetEquipped))]
        private static class HelmetPatch { private static void Prefix(VisEquipment __instance, ref int hash) => hash = Swap(__instance, hash); }

        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetChestEquipped))]
        private static class ChestPatch { private static void Prefix(VisEquipment __instance, ref int hash) => hash = Swap(__instance, hash); }

        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetLegEquipped))]
        private static class LegPatch { private static void Prefix(VisEquipment __instance, ref int hash) => hash = Swap(__instance, hash); }

        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetShoulderEquipped))]
        private static class ShoulderPatch
        {
            private static void Prefix(VisEquipment __instance, ref int hash, ref int variant)
            {
                int app = Swap(__instance, hash);
                if (app != hash) { hash = app; variant = 0; } // variantes da capa real nao valem na outra
            }
        }

        // ------------------------------------------------------------------ comando e janela

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class ConsolePatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("transmog", "(Aesir) /transmog = janela; /transmog <real> <aparencia>; /transmog off <real|*>",
                    args => HandleCommand(args.Args));
            }
        }

        [HarmonyPatch(typeof(Chat), nameof(Chat.SendText))]
        private static class ChatPatch
        {
            private static bool Prefix(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return true;
                string[] p = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length == 0 || !p[0].Equals("/transmog", StringComparison.OrdinalIgnoreCase)) return true;
                HandleCommand(p);
                return false;
            }
        }

        private static void Say(string msg) => Player.m_localPlayer?.Message(MessageHud.MessageType.Center, msg);

        private static void HandleCommand(string[] parts)
        {
            if (Player.m_localPlayer == null) return;
            if (Enabled != null && !Enabled.Value) { Say("Transmog desativado."); return; }
            if (!VipList.VipListApi.IsLocalPlayerVip()) { Say("Transmog e exclusivo Aesir (VIP)."); return; }
            if (parts.Length <= 1) { TransmogUi.Toggle(); return; }
            if (parts[1].Equals("off", StringComparison.OrdinalIgnoreCase)) { SendOff(parts.Length > 2 ? parts[2] : "*"); return; }
            if (parts.Length >= 3) SendSet(parts[1], parts[2]);
            else Say("Uso: /transmog <real> <aparencia> | /transmog off <real|*>");
        }
    }

    /// <summary>Janela IMGUI do transmog: escolhe a peca real e a aparencia (so as desbloqueadas).</summary>
    internal sealed class TransmogUi : MonoBehaviour
    {
        private static TransmogUi _inst;
        private Rect _rect = new Rect(80, 80, 560, 460);
        private string _real;
        private Vector2 _scrollA, _scrollB;

        internal static bool Open => _inst != null && _inst.enabled;

        internal static void Toggle()
        {
            if (_inst == null)
            {
                GameObject go = new GameObject("DeadheimTransmogUi");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<TransmogUi>();
                return;
            }
            _inst.enabled = !_inst.enabled;
        }

        private static string Label(string prefab)
        {
            ItemDrop.ItemData.SharedData s = Transmog.SharedOf(prefab);
            string n = s != null && Localization.instance != null ? Localization.instance.Localize(s.m_name) : prefab;
            return n + (n == prefab ? "" : " (" + prefab + ")");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) enabled = false;
        }

        private void OnGUI()
        {
            _rect = GUI.Window(0x7D4A, _rect, Draw, "Transmog (so aparencia)");
        }

        private void Draw(int id)
        {
            GUILayout.Label("Esquerda: a peca que voce usa. Direita: a aparencia que os outros veem. Desbloqueia ao equipar a peca.");
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(265));
            GUILayout.Label("Peca real");
            _scrollA = GUILayout.BeginScrollView(_scrollA, GUILayout.Height(330));
            foreach (string p in Transmog.LocalUnlocked.OrderBy(x => x))
            {
                string shown = Transmog.LocalChosen.TryGetValue(p, out string a) ? Label(p) + "  ->  " + a : Label(p);
                if (GUILayout.Toggle(_real == p, shown, "Button")) _real = p;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(265));
            GUILayout.Label(_real == null ? "Aparencia (escolha uma peca)" : "Aparencia para " + Label(_real));
            _scrollB = GUILayout.BeginScrollView(_scrollB, GUILayout.Height(330));
            if (_real != null)
                foreach (string p in Transmog.LocalUnlocked.OrderBy(x => x))
                {
                    if (p == _real || !Transmog.Compatible(_real, p, out _)) continue;
                    if (GUILayout.Button(Label(p))) Transmog.SendSet(_real, p);
                }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (_real != null && GUILayout.Button("Voltar ao real")) Transmog.SendOff(_real);
            if (GUILayout.Button("Limpar tudo")) Transmog.SendOff("*");
            if (GUILayout.Button("Fechar")) enabled = false;
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        // Cursor livre e sem controle do personagem enquanto a janela esta aberta.
        [HarmonyPatch(typeof(GameCamera), "UpdateMouseCapture")]
        private static class MousePatch
        {
            private static bool Prefix()
            {
                if (!Open) return true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.TakeInput))]
        private static class InputPatch
        {
            private static void Postfix(ref bool __result) { if (Open) __result = false; }
        }
    }
}
