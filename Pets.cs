using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Deadheim.Vanilla;

namespace Deadheim
{
    /// <summary>
    /// Pet cosmético: bichinho visual que acompanha o jogador, sem interação com o jogo.
    /// Só visual: não ataca, não leva dano, não carrega nada, não bloqueia ninguém e não muda nada no PvP.
    /// Implementação: modelo visual no cliente, sincronização via ZDO do jogador (dh_pet).
    /// </summary>
    [HarmonyPatch]
    public static class Pets
    {
        private const string ZdoPetKey = "dh_pet";
        private const string ZdoPetNameKey = "dh_petnome";
        private const string RpcToServer = "DH_Pet_ToServer";
        private const string RpcToClient = "DH_Pet_ToClient";
        // D3: tabela de verdade (servidor envia, clientes obedecem)
        private const string RpcSync = "DH_Pet_Sync";
        private const string RpcRequestSync = "DH_Pet_RequestSync";

        // Configurações sincronizadas
        public static ConfigEntry<bool> PetEnabled;
        public static ConfigEntry<float> PetRenderDistance;
        public static ConfigEntry<int> PetMaxVisible;
        public static ConfigEntry<string> PetCatalog;
        public static ConfigEntry<string> PetHiddenZones;

        // Chave hash do ZDO de arena do NpcValheim (lida sem depender da DLL deles)
        private static readonly int ZdoArenaMatch = "npcv_arenaMatch".GetStableHashCode();

        // Catálogo de pets: id -> PetDef
        private static Dictionary<string, PetDef> _catalog;
        private static bool _catalogLoaded;

        // Pets ativos por jogador (playerID -> GameObject do pet)
        private static Dictionary<long, GameObject> _activePets = new Dictionary<long, GameObject>();
        private static readonly HashSet<string> _failedPrefabs = new HashSet<string>(StringComparer.Ordinal);

        // Para o Update: última vez que varreu os jogadores
        private static float _lastScanTime;
        private const float ScanInterval = 1f;

        // D3: tabela servidor playerId → (petId, nome) — enviada a todos os clientes
        // No servidor: preenchida por OnToServer. No cliente: recebida por OnSync.
        private struct PetEntry { public string PetId; public string Name; }
        private static readonly Dictionary<long, PetEntry> _serverTable = new Dictionary<long, PetEntry>();
        private static float _lastOwnershipCheck;
        // Restauração do pet salvo após relogar ou reiniciar o servidor: o cliente reenvia "set"
        // quando a tabela do servidor não tem a entrada dele.
        private static bool _tableReceived;
        private static bool _restoring;
        private static float _nextRestore;
        private static int _restoreTries;
        private const float OwnershipCheckInterval = 60f;

        // Pai inativo dos moldes de pet (como MountPrefab.Hidden)
        private static Transform _hiddenRoot;

        private static Transform HiddenRoot
        {
            get
            {
                if (_hiddenRoot != null) return _hiddenRoot;
                var go = new GameObject("Deadheim_PetHidden");
                go.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(go);
                _hiddenRoot = go.transform;
                return _hiddenRoot;
            }
        }

        /// <summary>
        /// Definição de um pet: prefab original, escala, origem (Aesir/Ficha) e parâmetros do Animator.
        /// WalkSpeed/RunSpeed são lidos do Character do prefab antes de Strip (em m/s reais).
        /// </summary>
        public class PetDef
        {
            public string Id;
            public string PrefabName;
            public float Scale;
            public string Origin; // origem comercial do item (sem efeito sobre a posse)
            public string SpeedParam;   // ex: "forward_speed" (float, m/s reais)
            public string SitParam;     // ex: "sleeping" (bool), null se não tiver
            public float WalkSpeed;     // lido de Character.m_walkSpeed antes de Strip
            public float RunSpeed;      // lido de Character.m_runSpeed antes de Strip
        }

        internal static void BindConfigs(ConfigFile config)
        {
            PetEnabled = Plugin.Synced(config.Bind("Pets", "PetEnabled", true,
                new ConfigDescription("Habilita o sistema de pets cosméticos.")));

            PetRenderDistance = Plugin.Synced(config.Bind("Pets", "PetRenderDistance", 40f,
                new ConfigDescription("Distância máxima (em metros) para renderizar pets de outros jogadores.")));

            PetMaxVisible = Plugin.Synced(config.Bind("Pets", "PetMaxVisible", 12,
                new ConfigDescription("Número máximo de pets visíveis ao mesmo tempo (os mais próximos da câmera).")));

            // Catálogo corrigido:
            //   - SpeedParam: "forward_speed" (nome real no assembly do jogo para criaturas)
            //   - SitParam: "sleeping" (bool) onde aplicável; vazio = sem animação de idle
            //   - Corvo: prefab Crow (Raven não existe como spawnável), escala 0.6
            // Formato: id=prefab;escala;origem;speedParam;sitParam
            PetCatalog = Plugin.Synced(config.Bind("Pets", "PetCatalog",
                "galinha=Hen;1;item;forward_speed;sleeping," +
                "pintinho=Chicken;1;item;forward_speed;sleeping," +
                "leitao=Boar_piggy;1.3;item;forward_speed;sleeping," +
                "wolf_cub=Wolf_cub;1;item;forward_speed;sleeping," +
                "lox_cub=Lox_Calf;0.8;item;forward_speed;," +
                "corvo=Crow;1;item;forward_speed;",
                new ConfigDescription("Catálogo de pets. Formato: id=prefab;escala;origem;speedParam;sitParam (separados por vírgula). speedParam=forward_speed, sitParam=sleeping ou vazio.")));
            PetCatalog.SettingChanged += (s, e) => LoadCatalog();

            PetHiddenZones = Plugin.Synced(config.Bind("Pets", "PetHiddenZones",
                "arena,battleground,raid",
                new ConfigDescription("Zonas onde os pets ficam ocultos (separadas por vírgula). Valores: arena (zonas do Deadheim), battleground (partida de arena NpcValheim), raid (janela de raid).")));
        }

        internal static void Init()
        {
            LoadCatalog();
        }

        private static void LoadCatalog()
        {
            _catalog = new Dictionary<string, PetDef>(StringComparer.OrdinalIgnoreCase);
            string raw = PetCatalog.Value;
            if (string.IsNullOrWhiteSpace(raw))
            {
                Debug.LogWarning("[Deadheim Pets] Catálogo vazio; pets desabilitados.");
                _catalogLoaded = true;
                return;
            }

            try
            {
                string[] entries = raw.Split(',');
                foreach (string entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry)) continue;
                    // id=prefab;escala;origem;speedParam;sitParam
                    string[] parts = entry.Split(';');
                    if (parts.Length < 3)
                    {
                        Debug.LogWarning($"[Deadheim Pets] Entrada do catálogo ignorada (poucos campos): '{entry.Trim()}'.");
                        continue;
                    }

                    int eq = parts[0].IndexOf('=');
                    if (eq <= 0)
                    {
                        Debug.LogWarning($"[Deadheim Pets] Entrada do catálogo ignorada (falta 'id=prefab'): '{entry.Trim()}'.");
                        continue;
                    }

                    string id = parts[0].Substring(0, eq).Trim();
                    string prefab = parts[0].Substring(eq + 1).Trim();
                    if (!float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float scale) || scale <= 0f)
                        scale = 1f;
                    string origin = parts[2].Trim().ToLowerInvariant();
                    string speedParam = parts.Length > 3 ? parts[3].Trim() : "forward_speed";
                    if (string.IsNullOrEmpty(speedParam)) speedParam = "forward_speed";
                    string sitParam = parts.Length > 4 ? parts[4].Trim() : "";
                    if (sitParam == "null") sitParam = "";

                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(prefab))
                    {
                        Debug.LogWarning($"[Deadheim Pets] Entrada do catálogo ignorada (id ou prefab vazio): '{entry.Trim()}'.");
                        continue;
                    }

                    _catalog[id] = new PetDef
                    {
                        Id = id,
                        PrefabName = prefab,
                        Scale = scale,
                        Origin = origin,
                        SpeedParam = speedParam,
                        SitParam = sitParam,
                        WalkSpeed = 0f,  // preenchido em CreateTemplate ao ler o Character
                        RunSpeed = 0f
                    };
                }
                Debug.Log($"[Deadheim Pets] Catálogo carregado: {_catalog.Count} pets.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim Pets] Erro ao carregar catálogo: {ex.Message}");
            }
            _catalogLoaded = true;
        }


        // ------------------------------------------------------------------- posse

        /// <summary>
        /// Servidor: o inventário só existe no cliente dono (o Player remoto no servidor tem
        /// inventário vazio), então o servidor não consegue conferir o item. O pet é cosmético,
        /// sem vantagem de combate: o servidor aceita pet do catálogo e o cliente confere o item
        /// (UsePetItemPatch ao usar; ScanPlayers manda "off" quando o item sai do inventário).
        /// </summary>
        private static bool ServerOwns(long peerId, long playerId, PetDef def) => def != null;

        /// <summary>Cliente: o jogador local tem esse pet?</summary>
        private static bool LocalOwns(PetDef def)
        {
            return HasPetItem(Player.m_localPlayer, def);
        }

        private static string ItemPrefab(PetDef def) => "Pet_" + def.Id;

        private static bool HasPetItem(Player player, PetDef def)
        {
            if (player == null || def == null) return false;
            Inventory inventory = player.GetInventory();
            return inventory != null && inventory.GetAllItems().Any(item =>
                item?.m_dropPrefab != null && string.Equals(item.m_dropPrefab.name, ItemPrefab(def), StringComparison.Ordinal));
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        private static class UsePetItemPatch
        {
            private static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item)
            {
                if (__instance != Player.m_localPlayer || item?.m_dropPrefab == null || ZRoutedRpc.instance == null)
                    return true;
                string prefab = item.m_dropPrefab.name;
                if (!prefab.StartsWith("Pet_", StringComparison.Ordinal)) return true;
                string petId = prefab.Substring("Pet_".Length);
                if (_catalog == null || !_catalog.TryGetValue(petId, out PetDef def))
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Este item de pet não corresponde ao catálogo atual.");
                    return false;
                }
                if (!HasPetItem(Player.m_localPlayer, def)) return false;
                string active = Player.m_localPlayer.m_customData.TryGetValue(ZdoPetKey, out string current) ? current : "";
                SendToServer(string.Equals(active, def.Id, StringComparison.OrdinalIgnoreCase) ? "off" : "set", def.Id, "");
                return false;
            }
        }

        /// <summary>Lista os pets que o jogador local tem.</summary>
        public static string[] GetLocalPets()
        {
            if (_catalog == null) return Array.Empty<string>();
            return _catalog.Values.Where(LocalOwns).Select(d => d.Id).ToArray();
        }

        private static string SanitizeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in raw.Trim())
            {
                if (c == '<' || c == '>' || char.IsControl(c)) continue;
                sb.Append(c);
                if (sb.Length >= 20) break;
            }
            return sb.ToString().Trim();
        }

        // -------------------------------------------------------------------- RPC

        [HarmonyPatch(typeof(Game), "Start")]
        private static class RegisterRpcPatch
        {
            private static void Postfix()
            {
                if (ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.Register<ZPackage>(RpcToServer, OnToServer);
                ZRoutedRpc.instance.Register<ZPackage>(RpcToClient, OnToClient);
                ZRoutedRpc.instance.Register<ZPackage>(RpcSync, OnSync);
                ZRoutedRpc.instance.Register<ZPackage>(RpcRequestSync, OnRequestSync);
            }
        }

        // D3: cliente pede a tabela ao servidor ao spawnar
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class PlayerOnSpawnedPatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                if (ZRoutedRpc.instance == null) return;
                // Cliente requisita a tabela completa ao servidor
                ZRoutedRpc.instance.InvokeRoutedRPC(
                    ZRoutedRpc.instance.GetServerPeerID(),
                    RpcRequestSync,
                    new ZPackage());
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private static class LogoutPatch
        {
            private static void Prefix()
            {
                DestroyAll();
                // D3: limpar tabela local ao deslogar
                _serverTable.Clear();
                _tableReceived = false;
                _restoring = false;
                _restoreTries = 0;
            }
        }

        // D3: envia tabela completa a um peer (ao entrar no servidor) ou mudança individual a todos.
        private static void BroadcastSync(long targetPeer = ZRoutedRpc.Everybody)
        {
            if (ZRoutedRpc.instance == null || !ZNet.instance.IsServer()) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(_serverTable.Count);
            foreach (var kv in _serverTable)
            {
                pkg.Write(kv.Key);          // playerId
                pkg.Write(kv.Value.PetId);
                pkg.Write(kv.Value.Name);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(targetPeer, RpcSync, pkg);
        }

        // D3: recebida no cliente: substitui a tabela local pela do servidor.
        private static void OnSync(long sender, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null) return;
            // Só aceita do servidor
            if (sender != ZRoutedRpc.instance.GetServerPeerID()) return;
            try
            {
                _serverTable.Clear();
                _tableReceived = true;
                int count = pkg.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    long playerId = pkg.ReadLong();
                    string petId = pkg.ReadString();
                    string name = pkg.ReadString();
                    _serverTable[playerId] = new PetEntry { PetId = petId, Name = name };
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim Pets] Erro em OnSync: {ex.Message}");
            }
        }

        // D3: servidor recebe pedido de tabela completa de um cliente recém-conectado.
        private static void OnRequestSync(long sender, ZPackage _pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            BroadcastSync(sender);
        }

        // Servidor: valida posse e responde. Quem grava no ZDO é o cliente dono (o ZDO é dele).
        private static void OnToServer(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            long real = RemetenteRpc.Real(sender, out _);
            if (real == 0L || !Pvp.PvpPeer.TryResolve(real, out Pvp.PvpPeer peer)) return;

            try
            {
                string op = pkg.ReadString();
                if (!PetEnabled.Value)
                {
                    SendResponse(real, op, false, "Pets desabilitados no servidor.", "", "");
                    return;
                }

                if (op == "off")
                {
                    // D3: remover da tabela e sincronizar
                    _serverTable.Remove(peer.PlayerId);
                    BroadcastSync();
                    SendResponse(real, op, true, "", "", "");
                    return;
                }

                if (op == "nome")
                {
                    string newName = SanitizeName(pkg.ReadString());
                    // D3: atualizar o nome na tabela (se o pet estiver ativo)
                    if (_serverTable.TryGetValue(peer.PlayerId, out PetEntry entry))
                    {
                        _serverTable[peer.PlayerId] = new PetEntry { PetId = entry.PetId, Name = newName };
                        BroadcastSync();
                    }
                    SendResponse(real, op, true, "", "", newName);
                    return;
                }

                string petId = (pkg.ReadString() ?? "").Trim();
                if (!_catalog.TryGetValue(petId, out PetDef def))
                {
                    SendResponse(real, op, false, "Pet desconhecido.", "", "");
                    return;
                }

                if (op == "set")
                {
                    string name = SanitizeName(pkg.ReadString());
                    if (!ServerOwns(real, peer.PlayerId, def))
                    {
                        SendResponse(real, op, false, "Você não tem este pet.", "", "");
                        return;
                    }
                    // D3: gravar na tabela e sincronizar com todos
                    _serverTable[peer.PlayerId] = new PetEntry { PetId = def.Id, Name = name };
                    BroadcastSync();
                    SendResponse(real, op, true, "", def.Id, name);
                    Debug.Log($"[Deadheim Pets] {peer.Name} ativou o pet '{def.Id}'.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim Pets] Erro no RPC do servidor: {ex}");
            }
        }

        private static void SendResponse(long peerId, string op, bool success, string message, string petId, string name)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(op);
            pkg.Write(success);
            pkg.Write(message ?? "");
            pkg.Write(petId ?? "");
            pkg.Write(name ?? "");
            ZRoutedRpc.instance.InvokeRoutedRPC(peerId, RpcToClient, pkg);
        }

        private static void WriteLocal(string petId, string name, bool writePet, bool writeName)
        {
            Player lp = Player.m_localPlayer;
            if (lp == null) return;
            if (writePet) lp.m_customData[ZdoPetKey] = petId ?? "";
            if (writeName) lp.m_customData[ZdoPetNameKey] = name ?? "";
            if (lp.m_nview != null && lp.m_nview.IsValid())
            {
                ZDO zdo = lp.m_nview.GetZDO();
                if (writePet) zdo.Set(ZdoPetKey, petId ?? "");
                if (writeName) zdo.Set(ZdoPetNameKey, name ?? "");
            }
        }

        private static void OnToClient(long sender, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID()) return;

            try
            {
                string op = pkg.ReadString();
                bool success = pkg.ReadBool();
                string message = pkg.ReadString();
                string petId = pkg.ReadString();
                string name = pkg.ReadString();
                Player lp = Player.m_localPlayer;
                if (lp == null) return;

                if (!success)
                {
                    // Falha numa restauração automática: sem mensagem (para no limite de tentativas).
                    if (op == "set" && _restoring) _restoring = false;
                    else lp.Message(MessageHud.MessageType.TopLeft, message);
                    return;
                }

                if (op == "set")
                {
                    WriteLocal(petId, name, true, !string.IsNullOrEmpty(name));
                    if (_restoring) _restoring = false; // restauração automática: sem mensagem
                    else lp.Message(MessageHud.MessageType.Center, "Pet ativado!");
                }
                else if (op == "off")
                {
                    WriteLocal("", "", true, false);
                    lp.Message(MessageHud.MessageType.Center, "Pet desativado.");
                }
                else if (op == "nome")
                {
                    WriteLocal("", name, false, true);
                    lp.Message(MessageHud.MessageType.Center, string.IsNullOrEmpty(name) ? "Nome removido." : "Nome do pet alterado.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim Pets] Erro no RPC do cliente: {ex}");
            }
        }

        // ---------------------------------------------------------------------- Update

        /// <summary>Chamado do Plugin.Update: varre jogadores e atualiza pets visíveis.</summary>
        internal static void Tick()
        {
            if (_catalog == null || !PetEnabled.Value || !_catalogLoaded || ZNetScene.instance == null) return;

            float now = Time.realtimeSinceStartup;

            // D3: revalidação VIP a cada 60 s (no servidor: remove Aesir de quem perdeu o VIP)
            if (ZNet.instance != null && ZNet.instance.IsServer() && now - _lastOwnershipCheck > OwnershipCheckInterval)
            {
                _lastOwnershipCheck = now;
                CheckVipExpiry();
            }

            if (now - _lastScanTime < ScanInterval) return;
            _lastScanTime = now;

            try
            {
                ScanPlayers();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim Pets] Erro no scan de jogadores: {ex}");
            }
        }

        // Remove da tabela pets de jogadores que transferiram ou perderam o item.
        private static void CheckVipExpiry()
        {
            bool changed = false;
            foreach (var kv in _serverTable.ToList())
            {
                long playerId = kv.Key;
                string petId = kv.Value.PetId;

                // Quem saiu do servidor sai da tabela; ao voltar, o cliente reenvia o pet salvo.
                if (!Pvp.PvpPeer.TryFindByPlayerId(playerId, out _))
                {
                    _serverTable.Remove(playerId);
                    changed = true;
                    continue;
                }

                if (!_catalog.TryGetValue(petId, out PetDef def)) continue;
                if (!Pvp.PvpPeer.TryFindByPlayerId(playerId, out Pvp.PvpPeer peer)) continue;
                if (!ServerOwns(peer.PeerId, playerId, def))
                {
                    _serverTable.Remove(playerId);
                    changed = true;
                    Debug.Log($"[Deadheim Pets] Pet '{petId}' de {peer.Name} (playerId={playerId}) removido após transferência/perda do item.");
                }
            }
            if (changed) BroadcastSync();
        }

        /// <summary>Depois de relogar o ZDO do jogador é novo: republica o pet guardado no personagem.</summary>
        private static void RepublishLocal(Player lp)
        {
            if (lp.m_nview == null || !lp.m_nview.IsValid()) return;
            ZDO zdo = lp.m_nview.GetZDO();
            if (zdo.GetString(ZdoPetKey, "").Length > 0) return;
            if (!lp.m_customData.TryGetValue(ZdoPetKey, out string saved) || string.IsNullOrEmpty(saved)) return;
            if (!_catalog.TryGetValue(saved, out PetDef def) || !LocalOwns(def)) return;
            lp.m_customData.TryGetValue(ZdoPetNameKey, out string n);
            zdo.Set(ZdoPetKey, saved);
            if (!string.IsNullOrEmpty(n)) zdo.Set(ZdoPetNameKey, n);

            // Os outros clientes só mostram o pet que a tabela do servidor confirma. Depois de
            // relogar ou de um restart a entrada some: pede ao servidor de novo (sem mensagem),
            // com intervalo e limite de tentativas.
            long myId = lp.GetPlayerID();
            bool inTable = _serverTable.TryGetValue(myId, out PetEntry mine) && mine.PetId == saved;
            if (inTable) { _restoreTries = 0; return; }
            if (!_tableReceived || _restoreTries >= 6 || ZRoutedRpc.instance == null) return;
            float now = Time.realtimeSinceStartup;
            if (now < _nextRestore) return;
            _nextRestore = now + 10f;
            _restoreTries++;
            _restoring = true;
            SendToServer("set", saved, n ?? "");
        }

        private static void ScanPlayers()
        {
            Vector3 cameraPos = GameCamera.instance != null ? GameCamera.instance.transform.position : Vector3.zero;
            float renderDistSqr = PetRenderDistance.Value * PetRenderDistance.Value;

            HashSet<long> seen = new HashSet<long>();
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null) continue;
                long playerId = player.GetPlayerID();
                if (playerId == 0L) continue;
                seen.Add(playerId);

                bool isLocal = player == Player.m_localPlayer;
                if (isLocal) RepublishLocal(player);

                if (!isLocal && (player.transform.position - cameraPos).sqrMagnitude > renderDistSqr)
                {
                    DestroyPet(playerId);
                    continue;
                }

                if (player.m_nview == null || !player.m_nview.IsValid())
                {
                    DestroyPet(playerId);
                    continue;
                }

                ZDO zdo = player.m_nview.GetZDO();
                string petId = zdo.GetString(ZdoPetKey, "");
                if (isLocal && !string.IsNullOrEmpty(petId) &&
                    (!_catalog.TryGetValue(petId, out PetDef activeDef) || !LocalOwns(activeDef)))
                {
                    // O item foi negociado ou saiu do inventário: o novo dono fica com a posse.
                    WriteLocal("", "", true, false);
                    SendToServer("off");
                    petId = "";
                }
                if (string.IsNullOrEmpty(petId) || !_catalog.TryGetValue(petId, out PetDef def) || ShouldHide(player))
                {
                    DestroyPet(playerId);
                    continue;
                }

                // D3: para pets de outros jogadores, verificar a tabela do servidor.
                // O jogador local confia em si mesmo (ZDO é o dele).
                // Clientes remotos só mostram o pet se a tabela do servidor confirmar.
                if (!isLocal)
                {
                    if (!_serverTable.TryGetValue(playerId, out PetEntry tableEntry) || tableEntry.PetId != petId)
                    {
                        DestroyPet(playerId);
                        continue;
                    }
                }

                string petName = isLocal
                    ? zdo.GetString(ZdoPetNameKey, "")
                    : (_serverTable.TryGetValue(playerId, out PetEntry nameEntry) ? nameEntry.Name : "");

                UpdateOrCreatePet(player, def, petName);
            }

            foreach (long id in _activePets.Keys.ToList())
                if (!seen.Contains(id)) DestroyPet(id);

            ApplyVisibleLimit();
        }

        /// <summary>
        /// D2: pet oculto em arena, partida de arena (NpcValheim) e janela de raid.
        /// Recebe o Player para poder ler o ZDO do jogador (battleground) e a posição (arena/raid).
        /// </summary>
        private static float _nextHideLog;
        private static void LogHide(string why)
        {
            if (Time.realtimeSinceStartup < _nextHideLog) return;
            _nextHideLog = Time.realtimeSinceStartup + 30f;
            Debug.Log($"[Deadheim Pets] pet oculto: zona '{why}'.");
        }

        private static bool ShouldHide(Player player)
        {
            string zones = PetHiddenZones.Value?.ToLowerInvariant() ?? "";
            if (string.IsNullOrWhiteSpace(zones)) return false;

            Vector3 pos = player.transform.position;
            ZDO zdo = player.m_nview?.GetZDO();

            foreach (string zone in zones.Split(','))
            {
                string z = zone.Trim();

                // arena: zona geográfica configurada no Deadheim
                if (z == "arena" && Pvp.PvpZones.IsArena(pos))
                { LogHide("arena"); return true; }

                // battleground: partida de arena do NpcValheim (chave npcv_arenaMatch no ZDO do jogador)
                if (z == "battleground" && zdo != null && zdo.GetLong(ZdoArenaMatch, 0L) != 0L)
                { LogHide("battleground"); return true; }

                // raid: o castelo na posição tem uma zona de raid ativa.
                // PvpBridge.CastleAt retorna o nome do castelo que cobre o ponto; se há um castelo
                // e ele não caiu (CastleSafeAt=false), estamos em zona de raid.
                if (z == "raid")
                {
                    string castle = Pvp.PvpBridge.CastleAt?.Invoke(pos);
                    if (castle != null && !Pvp.PvpBridge.CastleSafe(pos))
                    { LogHide("raid (" + castle + ")"); return true; }
                }
            }
            return false;
        }

        private static void UpdateOrCreatePet(Player player, PetDef def, string petName)
        {
            long playerId = player.GetPlayerID();

            if (_activePets.TryGetValue(playerId, out GameObject existing))
            {
                PetFollower follower = existing != null ? existing.GetComponent<PetFollower>() : null;
                if (follower != null && follower.Def != null && follower.Def.Id == def.Id)
                {
                    follower.SetTarget(player);
                    follower.SetName(petName);
                    return;
                }
                DestroyPet(playerId); // pet trocado: refaz o modelo
            }

            GameObject pet = CreatePetVisual(def);
            if (pet == null) return;
            PetFollower f = pet.AddComponent<PetFollower>();
            f.SetDef(def);
            f.SetTarget(player);
            f.SetName(petName);
            _activePets[playerId] = pet;
        }

        private static void DestroyPet(long playerId)
        {
            if (_activePets.TryGetValue(playerId, out GameObject pet))
            {
                if (pet != null) UnityEngine.Object.Destroy(pet);
                _activePets.Remove(playerId);
            }
        }

        private static void DestroyAll()
        {
            foreach (long id in _activePets.Keys.ToList()) DestroyPet(id);
        }

        private static void ApplyVisibleLimit()
        {
            int max = PetMaxVisible.Value;
            Vector3 cameraPos = GameCamera.instance != null ? GameCamera.instance.transform.position : Vector3.zero;

            var followers = _activePets.Values
                .Where(g => g != null)
                .Select(g => g.GetComponent<PetFollower>())
                .Where(f => f != null)
                .OrderBy(f => (f.transform.position - cameraPos).sqrMagnitude)
                .ToList();

            for (int i = 0; i < followers.Count; i++)
                followers[i].LimitHidden = max > 0 && i >= max;
        }

        // -------------------------------------------------------------------- modelo visual

        private static GameObject CreatePetVisual(PetDef def)
        {
            if (ZNetScene.instance == null || _failedPrefabs.Contains(def.PrefabName)) return null;

            GameObject original = ZNetScene.instance.GetPrefab(def.PrefabName);
            if (original == null)
            {
                _failedPrefabs.Add(def.PrefabName);
                Debug.LogWarning($"[Deadheim Pets] Prefab '{def.PrefabName}' (pet '{def.Id}') não encontrado.");
                return null;
            }

            GameObject template = CreateTemplate(def, original);
            if (template == null) return null;

            GameObject pet = UnityEngine.Object.Instantiate(template);
            pet.name = $"Pet_{def.Id}";
            pet.SetActive(true);
            return pet;
        }

        private static GameObject CreateTemplate(PetDef def, GameObject original)
        {
            string templateName = $"PetTemplate_{def.Id}_{def.PrefabName}_{def.Scale}";
            Transform existing = HiddenRoot.Find(templateName);
            if (existing != null) return existing.gameObject;

            // D6: Ler WalkSpeed/RunSpeed do Character ANTES de Strip.
            Character ch = original.GetComponent<Character>();
            if (ch != null)
            {
                def.WalkSpeed = ch.m_walkSpeed;
                def.RunSpeed  = ch.m_runSpeed > 0f ? ch.m_runSpeed : ch.m_walkSpeed * 2f;
            }
            else
            {
                def.WalkSpeed = 2f;
                def.RunSpeed  = 5f;
            }

            // Também registrar no log os parâmetros do Animator (para depuração).
            Animator animatorRef = original.GetComponentInChildren<Animator>();
            if (animatorRef != null)
            {
                var paramNames = animatorRef.parameters
                    .Select(p => $"{p.name}({p.type})")
                    .ToArray();
                Debug.Log($"[Deadheim Pets] Parâmetros do Animator de '{def.PrefabName}': {string.Join(", ", paramNames)}");
            }

            GameObject template = UnityEngine.Object.Instantiate(original, HiddenRoot);
            template.name = templateName;
            template.SetActive(false);
            template.transform.localScale = Vector3.one * def.Scale;
            StripGameComponents(template);
            return template;
        }

        private static bool IsUnityType(Type t)
        {
            string ns = t.Namespace ?? "";
            return ns.StartsWith("UnityEngine", StringComparison.Ordinal)
                || ns.StartsWith("Unity.", StringComparison.Ordinal)
                || ns.StartsWith("TMPro", StringComparison.Ordinal);
        }

        private static int Depth(Type t)
        {
            int d = 0;
            for (; t != null; t = t.BaseType) d++;
            return d;
        }

        /// <summary>
        /// Deixa só o visual: tira colisores, juntas, áudio e todo script do jogo (Character, AnimalAI,
        /// MonsterAI, ZNetView, RandomFlyingBird...). Compara por herança (is), não por tipo exato, e
        /// destrói os scripts mais derivados primeiro, o ZNetView e o Rigidbody por último, para não
        /// esbarrar em [RequireComponent].
        /// </summary>
        private static void StripGameComponents(GameObject root)
        {
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (Joint j in root.GetComponentsInChildren<Joint>(true)) UnityEngine.Object.DestroyImmediate(j);
            foreach (AudioSource a in root.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(a);

            var scripts = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(m => m != null && !IsUnityType(m.GetType()) && !(m is ZNetView))
                .OrderBy(m => m is Character ? 1 : 0)   // CharacterDrop etc. dependem de Humanoid: Character por ultimo
                .ThenByDescending(m => Depth(m.GetType()))
                .ToList();
            foreach (MonoBehaviour m in scripts)
                if (m != null) UnityEngine.Object.DestroyImmediate(m);

            // segunda passada: o que resistiu na primeira (dependencia entre componentes)
            foreach (MonoBehaviour m in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (m != null && !IsUnityType(m.GetType()) && !(m is ZNetView)) UnityEngine.Object.DestroyImmediate(m);

            foreach (ZNetView z in root.GetComponentsInChildren<ZNetView>(true)) UnityEngine.Object.DestroyImmediate(z);
            foreach (Rigidbody r in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(r);

            foreach (Animator an in root.GetComponentsInChildren<Animator>(true))
            {
                an.fireEvents = false;
                an.applyRootMotion = false;
            }
        }

        // ---------------------------------------------------------------------- comandos

        [HarmonyPatch(typeof(Chat), "SendText")]
        private static class ChatCommandPatch
        {
            private static bool Prefix(ref string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return true;
                string[] parts = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || !parts[0].Equals("/pet", StringComparison.OrdinalIgnoreCase)) return true;
                HandlePetCommand(parts);
                return false;
            }
        }

        // O chat do Valheim manda "/comando" para o Terminal antes do SendText, entao o
        // comando precisa ser registrado como ConsoleCommand (como /depositar).
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class PetConsolePatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("pet", "/pet <id>, off, nome <texto>",
                    args =>
                    {
                        string[] parts = (string[])args.Args.Clone();
                        parts[0] = "/pet";
                        HandlePetCommand(parts);
                    });
            }
        }

        private static void SendToServer(params string[] fields)
        {
            ZPackage pkg = new ZPackage();
            foreach (string f in fields) pkg.Write(f ?? "");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RpcToServer, pkg);
        }

        private static void HandlePetCommand(string[] parts)
        {
            Player lp = Player.m_localPlayer;
            if (lp == null || ZRoutedRpc.instance == null || _catalog == null) return;

            if (parts.Length == 1)
            {
                string[] owned = GetLocalPets();
                lp.Message(MessageHud.MessageType.Center, owned.Length == 0
                    ? "Você não tem nenhum pet."
                    : "Seus pets: " + string.Join(", ", owned) + ". Use o item do pet para invocá-lo ou /pet off para guardá-lo.");
                return;
            }

            string sub = parts[1].ToLowerInvariant();

            if (sub == "off")
            {
                SendToServer("off");
                return;
            }

            if (sub == "nome")
            {
                SendToServer("nome", parts.Length > 2 ? string.Join(" ", parts.Skip(2)) : "");
                return;
            }

            // /pet debug: só admin — lista parâmetros do Animator de cada prefab do catálogo
            if (sub == "debug")
            {
                if (!Vanilla.Admin.LocalPlayerIsAdmin())
                {
                    lp.Message(MessageHud.MessageType.Center, "Só admin.");
                    return;
                }
                if (_catalog == null || ZNetScene.instance == null)
                {
                    lp.Message(MessageHud.MessageType.Center, "Catálogo ou cena não disponível.");
                    return;
                }
                foreach (PetDef def in _catalog.Values)
                {
                    GameObject original = ZNetScene.instance.GetPrefab(def.PrefabName);
                    if (original == null)
                    {
                        Debug.Log($"[Deadheim Pets] /pet debug: prefab '{def.PrefabName}' não encontrado.");
                        continue;
                    }
                    Animator anim = original.GetComponentInChildren<Animator>();
                    if (anim == null)
                    {
                        Debug.Log($"[Deadheim Pets] /pet debug: '{def.PrefabName}' sem Animator.");
                        continue;
                    }
                    string paramList = string.Join(", ", anim.parameters.Select(p => $"{p.name}({p.type})"));
                    Debug.Log($"[Deadheim Pets] /pet debug [{def.Id}] {def.PrefabName}: {paramList}");
                }
                lp.Message(MessageHud.MessageType.Center, "Parâmetros do Animator no log do jogo.");
                return;
            }

            // /pet <id> [nome]: ativa o pet
            SendToServer("set", sub, parts.Length > 2 ? string.Join(" ", parts.Skip(2)) : "");
        }
    }

    // ---------------------------------------------------------------------- PetFollower

    /// <summary>Faz o pet seguir o dono. Roda localmente em cada cliente; nada disso é rede.</summary>
    public class PetFollower : MonoBehaviour
    {
        private Player _target;
        private Animator _animator;
        private Renderer[] _renderers;
        // D6: parâmetros detectados por nome no Animator real do prefab
        private bool _hasSpeed;     // forward_speed (float, m/s)
        private bool _hasOnGround;  // onGround (bool)
        private bool _hasSleep;     // sleeping (bool)
        private float _idleTime;
        private Vector3 _lastPetPos;
        private bool _rendering = true;
        private string _petName = "";

        // D4: label de nome 3D sobre o pet
        private TextMeshPro _nameLabel;
        private const float NameMaxDistance = 15f;

        public Pets.PetDef Def { get; private set; }
        public bool LimitHidden;

        private const float FollowDistance = 2.5f;
        private const float TeleportDistance = 15f;
        private const float SitDelay = 5f;

        public void SetTarget(Player player) { _target = player; }

        public void SetDef(Pets.PetDef def)
        {
            Def = def;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                // D6: detectar parâmetros reais do Animator; evitar SetFloat/SetBool de nome errado.
                foreach (AnimatorControllerParameter p in _animator.parameters)
                {
                    if (p.name == def.SpeedParam && p.type == AnimatorControllerParameterType.Float)
                        _hasSpeed = true;
                    if (p.name == "onGround" && p.type == AnimatorControllerParameterType.Bool)
                        _hasOnGround = true;
                    if (!string.IsNullOrEmpty(def.SitParam) && p.name == def.SitParam && p.type == AnimatorControllerParameterType.Bool)
                        _hasSleep = true;
                }
            }
            _lastPetPos = transform.position;
        }

        /// <summary>D4: cria ou atualiza o label de nome 3D sobre o pet.</summary>
        public void SetName(string name)
        {
            _petName = name ?? "";
            UpdateNameLabel();
        }

        private void UpdateNameLabel()
        {
            if (string.IsNullOrEmpty(_petName))
            {
                if (_nameLabel != null) _nameLabel.gameObject.SetActive(false);
                return;
            }

            if (_nameLabel == null)
            {
                var go = new GameObject("PetName");
                go.transform.SetParent(transform, false);
                // Posiciona acima do pet (altura aprox 0.8 * escala do prefab)
                go.transform.localPosition = new Vector3(0f, 1.5f / (Def?.Scale > 0f ? Def.Scale : 1f), 0f);
                _nameLabel = go.AddComponent<TextMeshPro>();
                _nameLabel.alignment = TextAlignmentOptions.Center;
                _nameLabel.fontSize = 3f;
                _nameLabel.color = new Color(1f, 1f, 0.8f, 1f);
                _nameLabel.outlineWidth = 0.15f;
                _nameLabel.outlineColor = Color.black;
            }
            _nameLabel.text = _petName;
            _nameLabel.gameObject.SetActive(true);
        }

        private void SetRendering(bool on)
        {
            if (_rendering == on || _renderers == null) return;
            _rendering = on;
            foreach (Renderer r in _renderers) if (r != null) r.enabled = on;
            // Esconder/mostrar o label junto com o pet
            if (_nameLabel != null)
                _nameLabel.gameObject.SetActive(on && !string.IsNullOrEmpty(_petName));
        }

        private void LateUpdate()
        {
            // D4: billboard do nome — vira para a câmera em LateUpdate para suavidade
            if (_nameLabel != null && _nameLabel.gameObject.activeSelf && GameCamera.instance != null)
            {
                Vector3 camPos = GameCamera.instance.transform.position;
                float dist = Vector3.Distance(transform.position, camPos);
                _nameLabel.gameObject.SetActive(dist <= NameMaxDistance && _rendering && !string.IsNullOrEmpty(_petName));
                if (_nameLabel.gameObject.activeSelf)
                    _nameLabel.transform.rotation = Quaternion.LookRotation(_nameLabel.transform.position - camPos);
            }
        }

        private void Update()
        {
            if (_target == null || Def == null) return;

            try
            {
                bool away = _target.InNumShipVolumes > 0 || _target.IsAttachedToShip() || _target.IsRiding();
                UpdateMovement(away);
                SetRendering(!away && !LimitHidden);
                UpdateAnimation();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim PetFollower] Erro no Update: {ex.Message}");
            }
        }

        private static Vector3 Grounded(Vector3 p)
        {
            if (ZoneSystem.instance != null) p.y = ZoneSystem.instance.GetGroundHeight(p);
            return p;
        }

        private void UpdateMovement(bool away)
        {
            Vector3 targetPos = _target.transform.position;
            Vector3 currentPos = transform.position;

            if (away || Vector3.Distance(currentPos, targetPos) > TeleportDistance)
            {
                transform.position = Grounded(targetPos - _target.transform.forward * FollowDistance);
                return;
            }

            Vector3 desiredPos = Grounded(targetPos - _target.transform.forward * FollowDistance);
            float ownerSpeed = _target.GetVelocity().magnitude;
            // D6: velocidade de movimento do pet proporcional à do dono, dentro do range walk/run do prefab
            float walkSpeed = Def.WalkSpeed > 0f ? Def.WalkSpeed : 2f;
            float runSpeed  = Def.RunSpeed  > 0f ? Def.RunSpeed  : 5f;
            float moveSpeed = Mathf.Lerp(walkSpeed, runSpeed, Mathf.Clamp01(ownerSpeed / 6f));
            // alcanca o dono: se ficou para tras, corre mais que ele (o pet pequeno tem passo curto)
            float gap = Vector3.Distance(currentPos, desiredPos);
            if (gap > 1f) moveSpeed = Mathf.Max(moveSpeed, ownerSpeed * 1.2f, Mathf.Min(gap * 2f, 12f));
            transform.position = Vector3.MoveTowards(currentPos, desiredPos, moveSpeed * Time.deltaTime);

            Vector3 moveDir = desiredPos - currentPos;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 5f * Time.deltaTime);
            }
        }

        private void UpdateAnimation()
        {
            if (_animator == null) return;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            // D6: velocidade real do pet em m/s (não normalizada 0-1) para o parâmetro forward_speed
            float petSpeed = (transform.position - _lastPetPos).magnitude / dt;
            _lastPetPos = transform.position;

            // D6: onGround = true sempre (o pet anda no chão; sem isso o controlador pode ficar em queda)
            if (_hasOnGround) _animator.SetBool("onGround", true);

            // D6: passar a velocidade real em m/s (não normalizada)
            if (_hasSpeed) _animator.SetFloat(Def.SpeedParam, petSpeed);

            // D6: sleeping é uma bool, não um trigger — usar SetBool
            if (petSpeed < 0.05f)
            {
                _idleTime += dt;
                if (_idleTime > SitDelay && _hasSleep)
                    _animator.SetBool(Def.SitParam, true);
            }
            else
            {
                _idleTime = 0f;
                if (_hasSleep) _animator.SetBool(Def.SitParam, false);
            }
        }
    }
}
