using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Aesir 24: plantas salvas. O VIP salva as pecas que ele mesmo construiu num raio e as recoloca
    /// depois, uma a uma, pela fila. Cada peca passa por Player.PlacePiece, depois das travas de
    /// ward, raid, combate, material e do OK do servidor (o PlacePiece do jogo nao tem nenhuma delas).
    /// Sem wards, portais, baus nem pedestais. As plantas ficam no servidor.
    /// </summary>
    [HarmonyPatch]
    public static class Plantas
    {
        private const string RpcToServer = "DH_Planta_ToServer";
        private const string RpcToClient = "DH_Planta_ToClient";

        // Resultado do "check" (servidor -> cliente)
        private const int ResOk = 0;
        private const int ResSkip = 1;   // esta peca nao pode, segue para a proxima
        private const int ResRetry = 2;  // tentar de novo em instantes
        private const int ResFatal = 3;  // cancela a colocacao

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<int> MaxPieces;
        public static ConfigEntry<int> MaxSaved;
        public static ConfigEntry<float> PlaceIntervalSeconds;
        public static ConfigEntry<float> PlaceCooldownSeconds;
        public static ConfigEntry<float> CaptureRadius;
        public static ConfigEntry<float> MaxReach;
        public static ConfigEntry<string> PrefabDeny;

        internal const float HardMaxRadius = 30f;

        internal struct Peca
        {
            public string Prefab;
            public Vector3 Pos;       // relativo a ancora, no referencial do yaw
            public Quaternion Rot;
        }

        internal class Planta
        {
            public string Name;
            public long Created;
            public List<Peca> Pecas = new List<Peca>();
        }

        internal static void Bind(ConfigFile config)
        {
            const string s = "Blueprints";
            Enabled = Plugin.Synced(config.Bind(s, "Enabled", true,
                new ConfigDescription("Aesir 24: plantas salvas (VIP salva as proprias pecas e as recoloca pela fila).")));
            MaxPieces = Plugin.Synced(config.Bind(s, "MaxPieces", 300,
                new ConfigDescription("Maximo de pecas por planta.")));
            MaxSaved = Plugin.Synced(config.Bind(s, "MaxSaved", 5,
                new ConfigDescription("Maximo de plantas salvas por VIP.")));
            PlaceIntervalSeconds = Plugin.Synced(config.Bind(s, "PlaceIntervalSeconds", 0.5f,
                new ConfigDescription("Segundos entre uma peca e outra ao recolocar (0,5 = 2 pecas por segundo).")));
            PlaceCooldownSeconds = Plugin.Synced(config.Bind(s, "PlaceCooldownSeconds", 10f,
                new ConfigDescription("Espera minima entre o fim de uma colocacao e o inicio da proxima.")));
            CaptureRadius = Plugin.Synced(config.Bind(s, "CaptureRadius", 15f,
                new ConfigDescription("Raio padrao da captura, em metros (maximo 30).")));
            MaxReach = Plugin.Synced(config.Bind(s, "MaxReach", 40f,
                new ConfigDescription("Distancia maxima do jogador ate a peca que esta sendo colocada. Planta grande: o jogador anda.")));
            PrefabDeny = Plugin.Synced(config.Bind(s, "PrefabDeny", "guard_stone,piece_workbench_ext_dummy",
                new ConfigDescription("Prefabs que nunca entram numa planta, alem de wards, portais, baus, pedestais e as pecas so de admin.")));
        }

        // ------------------------------------------------------------------ regras de prefab

        private static HashSet<string> _denyCache;
        private static string _denyCacheSrc;

        private static HashSet<string> DenySet()
        {
            string src = (PrefabDeny?.Value ?? "") + "|" + (Plugin.OnlyAdminPieces?.Value ?? "");
            if (_denyCache != null && src == _denyCacheSrc) return _denyCache;
            _denyCacheSrc = src;
            _denyCache = new HashSet<string>(
                src.Split(',', '|').Select(x => x.Trim()).Where(x => x.Length > 0),
                StringComparer.OrdinalIgnoreCase);
            return _denyCache;
        }

        /// <summary>So pecas de construcao comuns: com Piece e WearNTear, sem ward, portal, bau, pedestal ou item.</summary>
        internal static bool IsAllowed(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName) || prefabName.Length > 64) return false;
            if (DenySet().Contains(prefabName)) return false;
            string low = prefabName.ToLowerInvariant();
            if (low.Contains("portal") || low.Contains("guard_stone") || low.Contains("raidward") || low.Contains("aesirchest"))
                return false;
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            if (prefab == null || prefab.GetComponent<Piece>() == null || prefab.GetComponent<WearNTear>() == null) return false;
            if (prefab.GetComponent<PrivateArea>() != null || prefab.GetComponent<TeleportWorld>() != null
                || prefab.GetComponent<Container>() != null || prefab.GetComponent<ItemStand>() != null
                || prefab.GetComponent<ArmorStand>() != null || prefab.GetComponent<ItemDrop>() != null)
                return false;
            try { if (Wards.WardProfiles.For(prefab) != null) return false; } catch { }
            return true;
        }

        // ------------------------------------------------------------------ geometria (igual no cliente e no servidor)

        internal static Quaternion YawRot(float yaw) => Quaternion.Euler(0f, yaw, 0f);

        internal static void WorldOf(Peca p, Vector3 anchor, float yaw, out Vector3 pos, out Quaternion rot)
        {
            Quaternion y = YawRot(yaw);
            pos = anchor + y * p.Pos;
            rot = y * p.Rot;
        }

        private static bool Finite(Vector3 v) => IsF(v.x) && IsF(v.y) && IsF(v.z);
        private static bool IsF(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        // ------------------------------------------------------------------ servidor: armazenamento

        private static Dictionary<long, List<Planta>> _store;
        private static string _path;

        private static string WorldFile()
        {
            string world = ZNet.instance != null ? ZNet.instance.GetWorldName() : "world";
            foreach (char c in Path.GetInvalidFileNameChars()) world = world.Replace(c, '_');
            return Path.Combine(Path.Combine(Paths.ConfigPath, "Deadheim"), "plantas-" + world + ".txt");
        }

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        private static string Q(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

        private static void EnsureLoaded()
        {
            string path = WorldFile();
            if (_store != null && path == _path) return;
            _path = path;
            _store = new Dictionary<long, List<Planta>>();
            try
            {
                if (!File.Exists(path)) return;
                foreach (string line in File.ReadAllLines(path))
                {
                    string[] c = line.Split('\t');
                    if (c.Length < 4 || !long.TryParse(c[0], out long owner)) continue;
                    var pl = new Planta { Name = c[1] };
                    long.TryParse(c[2], out pl.Created);
                    foreach (string item in c[3].Split(';'))
                    {
                        string[] f = item.Split(',');
                        if (f.Length != 8) continue;
                        float[] n = new float[7];
                        bool ok = true;
                        for (int i = 0; i < 7; i++)
                            ok &= float.TryParse(f[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out n[i]);
                        if (!ok) continue;
                        pl.Pecas.Add(new Peca { Prefab = f[0], Pos = new Vector3(n[0], n[1], n[2]), Rot = new Quaternion(n[3], n[4], n[5], n[6]) });
                    }
                    if (pl.Pecas.Count == 0) continue;
                    if (!_store.TryGetValue(owner, out List<Planta> list)) _store[owner] = list = new List<Planta>();
                    list.Add(pl);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim Plantas] " + path + " ilegivel (" + ex.Message + "); comecando vazio.");
                _store = new Dictionary<long, List<Planta>>();
            }
        }

        private static void SaveFile()
        {
            if (_store == null || string.IsNullOrEmpty(_path)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                var lines = new List<string>();
                foreach (var kv in _store)
                    foreach (Planta pl in kv.Value)
                    {
                        var sb = new StringBuilder();
                        for (int i = 0; i < pl.Pecas.Count; i++)
                        {
                            Peca p = pl.Pecas[i];
                            if (i > 0) sb.Append(';');
                            sb.Append(p.Prefab).Append(',').Append(F(p.Pos.x)).Append(',').Append(F(p.Pos.y)).Append(',').Append(F(p.Pos.z))
                              .Append(',').Append(Q(p.Rot.x)).Append(',').Append(Q(p.Rot.y)).Append(',').Append(Q(p.Rot.z)).Append(',').Append(Q(p.Rot.w));
                        }
                        lines.Add(kv.Key + "\t" + pl.Name + "\t" + pl.Created + "\t" + sb);
                    }
                string temp = _path + ".tmp";
                File.WriteAllLines(temp, lines);
                File.Copy(temp, _path, true);   // Mono/Linux da DatHost: nada de File.Replace
                File.Delete(temp);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim Plantas] nao foi possivel salvar " + _path + ": " + ex.Message);
            }
        }

        private static List<Planta> ListOf(long playerId)
        {
            EnsureLoaded();
            if (!_store.TryGetValue(playerId, out List<Planta> list)) _store[playerId] = list = new List<Planta>();
            return list;
        }

        internal static string CleanName(string name)
        {
            if (name == null) return "";
            var sb = new StringBuilder();
            foreach (char c in name.Trim())
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-') sb.Append(c);
            string r = sb.ToString().Trim();
            return r.Length > 24 ? r.Substring(0, 24).Trim() : r;
        }

        // ------------------------------------------------------------------ servidor: sessao de colocacao

        private class Session
        {
            public Planta Planta;
            public Vector3 Anchor;
            public float Yaw;
            public float LastActivity;
            public float LastCheck;
            public HashSet<int> Done = new HashSet<int>();
            public int Checks;
        }

        private static readonly Dictionary<long, Session> _sessions = new Dictionary<long, Session>();
        private static readonly Dictionary<long, float> _cooldownUntil = new Dictionary<long, float>();
        private const float SessionIdle = 120f;

        private static bool IsVipPeer(long peerId)
        {
            if (ZRoutedRpc.instance != null && peerId == ZRoutedRpc.instance.m_id)
                return VipList.VipListApi.IsLocalPlayerVip();
            string host = ZNet.instance?.GetPeer(peerId)?.m_socket?.GetHostName();
            return VipList.VipListApi.IsVip(host);
        }

        private static void EndSession(long peerId)
        {
            if (_sessions.Remove(peerId))
                _cooldownUntil[peerId] = Time.realtimeSinceStartup + Mathf.Max(0f, PlaceCooldownSeconds.Value);
        }

        [HarmonyPatch(typeof(Game), "Start")]
        private static class RegisterRpcPatch
        {
            private static void Postfix()
            {
                if (ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.Register<ZPackage>(RpcToServer, OnToServer);
                ZRoutedRpc.instance.Register<ZPackage>(RpcToClient, OnToClient);
                PlantasUi.Ensure();
            }
        }

        private static void WritePeca(ZPackage pkg, Peca p)
        {
            pkg.Write(p.Prefab);
            pkg.Write(p.Pos.x); pkg.Write(p.Pos.y); pkg.Write(p.Pos.z);
            pkg.Write(p.Rot.x); pkg.Write(p.Rot.y); pkg.Write(p.Rot.z); pkg.Write(p.Rot.w);
        }

        private static Peca ReadPeca(ZPackage pkg)
        {
            string prefab = pkg.ReadString();
            var pos = new Vector3(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());
            var rot = new Quaternion(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());
            return new Peca { Prefab = prefab, Pos = pos, Rot = rot };
        }

        private static void Reply(long peerId, ZPackage pkg)
            => ZRoutedRpc.instance.InvokeRoutedRPC(peerId, RpcToClient, pkg);

        private static ZPackage Head(string op, bool ok, string msg)
        {
            var pkg = new ZPackage();
            pkg.Write(op); pkg.Write(ok); pkg.Write(msg ?? "");
            return pkg;
        }

        private static void SendList(long peerId, long playerId, bool ok, string msg)
        {
            ZPackage pkg = Head("list", ok, msg);
            List<Planta> list = ListOf(playerId);
            pkg.Write(list.Count);
            foreach (Planta p in list) { pkg.Write(p.Name); pkg.Write(p.Pecas.Count); }
            Reply(peerId, pkg);
        }

        private static void OnToServer(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            long real = RemetenteRpc.Real(sender, out _);
            if (real == 0L || !Pvp.PvpPeer.TryResolve(real, out Pvp.PvpPeer peer)) return;

            try
            {
                string op = pkg.ReadString();
                if (!Enabled.Value) { Reply(real, Head(op, false, "Plantas desabilitadas no servidor.")); return; }

                bool vip = IsVipPeer(real);
                // VIP expirado: ainda lista e apaga, mas nao salva nem coloca.
                if (!vip && op != "list" && op != "delete" && op != "end")
                {
                    const string vipMsg = "Plantas salvas sao exclusivas dos Aesir (VIP).";
                    if (op == "check")
                    {
                        ZPackage f = Head("check", false, vipMsg);
                        f.Write(-1); f.Write(ResFatal);
                        Reply(real, f);
                    }
                    else Reply(real, Head(op, false, vipMsg));
                    return;
                }

                switch (op)
                {
                    case "list": SendList(real, peer.PlayerId, true, ""); break;
                    case "save": ServerSave(real, peer, pkg); break;
                    case "delete": ServerDelete(real, peer, pkg); break;
                    case "begin": ServerBegin(real, peer, pkg); break;
                    case "check": ServerCheck(real, peer, pkg); break;
                    case "end": EndSession(real); break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim Plantas] erro no RPC do servidor: " + ex);
            }
        }

        private static void ServerSave(long peerId, Pvp.PvpPeer peer, ZPackage pkg)
        {
            string name = CleanName(pkg.ReadString());
            int n = pkg.ReadInt();
            if (name.Length == 0) { SendList(peerId, peer.PlayerId, false, "De um nome valido a planta."); return; }
            if (n <= 0 || n > MaxPieces.Value)
            {
                SendList(peerId, peer.PlayerId, false, "Planta com " + n + " pecas; o limite e " + MaxPieces.Value + ".");
                return;
            }

            var planta = new Planta { Name = name, Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            for (int i = 0; i < n; i++)
            {
                Peca p = ReadPeca(pkg);
                if (!IsAllowed(p.Prefab) || !Finite(p.Pos) || p.Pos.magnitude > HardMaxRadius * 2f + 10f) continue;
                float qn = Mathf.Sqrt(p.Rot.x * p.Rot.x + p.Rot.y * p.Rot.y + p.Rot.z * p.Rot.z + p.Rot.w * p.Rot.w);
                if (!IsF(qn) || qn < 0.5f) continue;
                p.Rot = new Quaternion(p.Rot.x / qn, p.Rot.y / qn, p.Rot.z / qn, p.Rot.w / qn);
                planta.Pecas.Add(p);
            }
            if (planta.Pecas.Count == 0) { SendList(peerId, peer.PlayerId, false, "Nenhuma peca valida na planta."); return; }

            List<Planta> list = ListOf(peer.PlayerId);
            int idx = list.FindIndex(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (idx < 0 && list.Count >= MaxSaved.Value)
            {
                SendList(peerId, peer.PlayerId, false, "Limite de " + MaxSaved.Value + " plantas. Apague uma antes.");
                return;
            }
            if (idx >= 0) list[idx] = planta; else list.Add(planta);
            SaveFile();
            Debug.Log("[Deadheim Plantas] " + peer.Name + " salvou '" + name + "' (" + planta.Pecas.Count + " pecas).");
            SendList(peerId, peer.PlayerId, true, "Planta '" + name + "' salva (" + planta.Pecas.Count + " pecas).");
        }

        private static void ServerDelete(long peerId, Pvp.PvpPeer peer, ZPackage pkg)
        {
            string name = pkg.ReadString();
            List<Planta> list = ListOf(peer.PlayerId);
            int removed = list.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) SaveFile();
            SendList(peerId, peer.PlayerId, removed > 0, removed > 0 ? "Planta apagada." : "Planta nao encontrada.");
        }

        private static void ServerBegin(long peerId, Pvp.PvpPeer peer, ZPackage pkg)
        {
            string name = pkg.ReadString();
            var anchor = new Vector3(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());
            float yaw = pkg.ReadSingle();
            float now = Time.realtimeSinceStartup;

            string fail = null;
            Planta planta = ListOf(peer.PlayerId).Find(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (planta == null) fail = "Planta nao encontrada.";
            else if (!Finite(anchor) || !IsF(yaw)) fail = "Posicao invalida.";
            else if (_sessions.TryGetValue(peerId, out Session old) && now - old.LastActivity < SessionIdle)
                fail = "Ja existe uma colocacao em andamento.";
            else if (_cooldownUntil.TryGetValue(peerId, out float until) && now < until)
                fail = "Aguarde " + Mathf.CeilToInt(until - now) + " s para colocar outra planta.";
            else if (Vector3.Distance(anchor, peer.Position) > MaxReach.Value + 10f)
                fail = "Ponto de colocacao longe demais.";
            else if (Wards.WardBridge.Governed(anchor) || Wards.WardBridge.Governed(peer.Position))
                fail = "Nao e possivel colocar plantas em zona de raid.";

            if (fail != null) { Reply(peerId, Head("begin", false, fail)); return; }

            _sessions[peerId] = new Session { Planta = planta, Anchor = anchor, Yaw = yaw, LastActivity = now, LastCheck = -99f };
            ZPackage ok = Head("begin", true, "");
            ok.Write(planta.Pecas.Count);
            foreach (Peca p in planta.Pecas) WritePeca(ok, p);
            ok.Write(anchor.x); ok.Write(anchor.y); ok.Write(anchor.z); ok.Write(yaw);
            Reply(peerId, ok);
            Debug.Log("[Deadheim Plantas] " + peer.Name + " comecou a colocar '" + name + "'.");
        }

        private static void ServerCheck(long peerId, Pvp.PvpPeer peer, ZPackage pkg)
        {
            int idx = pkg.ReadInt();
            float now = Time.realtimeSinceStartup;

            ZPackage Res(int code, string msg)
            {
                ZPackage r = Head("check", code == ResOk, msg);
                r.Write(idx); r.Write(code);
                return r;
            }

            if (!_sessions.TryGetValue(peerId, out Session s) || now - s.LastActivity > SessionIdle)
            {
                _sessions.Remove(peerId);
                Reply(peerId, Res(ResFatal, "Colocacao nao iniciada ou expirada."));
                return;
            }
            if (idx < 0 || idx >= s.Planta.Pecas.Count || ++s.Checks > s.Planta.Pecas.Count * 3 + 20)
            {
                EndSession(peerId);
                Reply(peerId, Res(ResFatal, "Pedido invalido."));
                return;
            }
            if (s.Done.Contains(idx)) { s.LastActivity = now; Reply(peerId, Res(ResOk, "")); return; }
            if (now - s.LastCheck < 0.25f) { Reply(peerId, Res(ResRetry, "")); return; }

            s.LastCheck = now;
            s.LastActivity = now;
            Peca peca = s.Planta.Pecas[idx];
            WorldOf(peca, s.Anchor, s.Yaw, out Vector3 pos, out _);

            if (!IsAllowed(peca.Prefab)) { Reply(peerId, Res(ResSkip, "Peca nao permitida.")); return; }
            if (Vector3.Distance(pos, peer.Position) > MaxReach.Value + 15f) { Reply(peerId, Res(ResRetry, "Longe demais.")); return; }
            if (Wards.WardBridge.Governed(pos))
            {
                EndSession(peerId);
                Reply(peerId, Res(ResFatal, "Zona de raid: colocacao cancelada."));
                return;
            }
            if (Location.IsInsideNoBuildLocation(pos)) { Reply(peerId, Res(ResSkip, "Zona sem construcao.")); return; }
            if (ServerWardBlocks(pos, peer.PlayerId)) { Reply(peerId, Res(ResSkip, "Ward alheio.")); return; }

            s.Done.Add(idx);
            Reply(peerId, Res(ResOk, ""));
        }

        private static bool ServerWardBlocks(Vector3 pos, long playerId)
        {
            if (Wards.WardBridge.Governed(pos)) return false;
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || !area.IsEnabled() || !area.IsInside(pos, 0f)) continue;
                if (Wards.WardCore.IsPermittedIn(area, playerId)) continue;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ cliente: estado

        internal class Entry { public string Name; public int Count; }

        internal static readonly List<Entry> Saved = new List<Entry>();
        internal static string Status = "";
        internal static bool ListLoaded;

        internal class Run
        {
            public List<Peca> Pecas;
            public Vector3 Anchor;
            public float Yaw;
            public int Idx;
            public int Placed, Skipped, Existing;
            public bool InFlight;
            public int ApprovedIdx = -1;
            public float NextAt;
            public float SentAt;
            public string Waiting = "";
            public string Name;
        }

        internal static Run Current;
        internal static bool Starting;   // "begin" enviado, esperando resposta
        private static string _startName;
        private static Vector3 _startAnchor;
        private static float _startYaw;

        private static void SendToServer(ZPackage pkg)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RpcToServer, pkg);

        private static ZPackage Op(string op)
        {
            var pkg = new ZPackage();
            pkg.Write(op);
            return pkg;
        }

        internal static void RequestList()
        {
            if (ZRoutedRpc.instance == null) return;
            SendToServer(Op("list"));
        }

        internal static void RequestDelete(string name)
        {
            ZPackage p = Op("delete"); p.Write(name); SendToServer(p);
        }

        // ------------------------------------------------------------------ cliente: captura

        internal static void Capture(string rawName, float radius)
        {
            Player lp = Player.m_localPlayer;
            if (lp == null) return;
            string name = CleanName(rawName);
            if (name.Length == 0) { Status = "Escreva um nome (letras, numeros, espaco)."; return; }
            if (!VipList.VipListApi.IsLocalPlayerVip()) { Status = "Plantas salvas sao exclusivas dos Aesir (VIP)."; return; }

            radius = Mathf.Clamp(radius, 3f, Mathf.Min(HardMaxRadius, Mathf.Max(3f, CaptureRadius.Value * 2f)));
            var found = new List<Piece>();
            Piece.GetAllPiecesInRadius(lp.transform.position, radius, found);

            long me = lp.GetPlayerID();
            float yaw = lp.transform.eulerAngles.y;
            Quaternion inv = Quaternion.Inverse(YawRot(yaw));
            Vector3 origin = lp.transform.position;
            var pecas = new List<(Peca p, int crafting)>();
            int ignoradas = 0;
            foreach (Piece pc in found)
            {
                if (pc == null || pc.GetCreator() != me) { ignoradas++; continue; }
                string prefab = Utils.GetPrefabName(pc.gameObject);
                if (!IsAllowed(prefab)) { ignoradas++; continue; }
                pecas.Add((new Peca
                {
                    Prefab = prefab,
                    Pos = inv * (pc.transform.position - origin),
                    Rot = inv * pc.transform.rotation,
                }, pc.m_category == Piece.PieceCategory.Crafting ? 0 : 1));
            }

            if (pecas.Count == 0) { Status = "Nenhuma peca sua e permitida nesse raio (" + ignoradas + " ignoradas)."; return; }
            if (pecas.Count > MaxPieces.Value)
            {
                Status = "Sao " + pecas.Count + " pecas; o limite e " + MaxPieces.Value + ". Diminua o raio.";
                return;
            }

            // Bancadas primeiro (outras pecas podem exigir a estacao), depois de baixo para cima.
            pecas = pecas.OrderBy(x => x.crafting).ThenBy(x => Mathf.Round(x.p.Pos.y * 4f)).ToList();

            ZPackage pkg = Op("save");
            pkg.Write(name);
            pkg.Write(pecas.Count);
            foreach (var x in pecas) WritePeca(pkg, x.p);
            SendToServer(pkg);
            Status = "Salvando " + pecas.Count + " pecas (" + ignoradas + " ignoradas: alheias ou nao permitidas)...";
        }

        // ------------------------------------------------------------------ cliente: colocacao

        internal static void StartPlace(string name)
        {
            Player lp = Player.m_localPlayer;
            if (lp == null || Current != null || Starting) return;
            if (!VipList.VipListApi.IsLocalPlayerVip()) { Status = "Plantas salvas sao exclusivas dos Aesir (VIP)."; return; }
            if (BlockedNow(lp, out string why)) { Status = why; return; }

            Vector3 anchor = LookPoint(lp);
            float yaw = Mathf.Round(lp.transform.eulerAngles.y / 22.5f) * 22.5f;
            if (Wards.WardBridge.Governed(anchor)) { Status = "Nao e possivel colocar plantas em zona de raid."; return; }

            _startName = name; _startAnchor = anchor; _startYaw = yaw;
            Starting = true;
            ZPackage pkg = Op("begin");
            pkg.Write(name);
            pkg.Write(anchor.x); pkg.Write(anchor.y); pkg.Write(anchor.z); pkg.Write(yaw);
            SendToServer(pkg);
            Status = "Iniciando '" + name + "'...";
        }

        internal static void CancelPlace(string message = null)
        {
            if (Current != null || Starting)
            {
                if (ZRoutedRpc.instance != null) SendToServer(Op("end"));
                if (Current != null && message == null)
                    message = "Colocacao cancelada: " + Current.Placed + " pecas colocadas.";
            }
            Current = null;
            Starting = false;
            if (message != null) Status = message;
        }

        private static Vector3 LookPoint(Player lp)
        {
            Camera cam = GameCamera.instance != null ? GameCamera.instance.GetComponent<Camera>() : Camera.main;
            if (cam != null)
            {
                int mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
                if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 40f, mask))
                    return hit.point;
            }
            return lp.transform.position + lp.transform.forward * 4f;
        }

        private static bool BlockedNow(Player lp, out string why)
        {
            why = null;
            if (Pvp.PvpState.InEscapeCombat || Pvp.PvpState.InCombat) { why = "Em combate: nao e possivel colocar plantas."; return true; }
            if (lp.IsDead() || lp.InCutscene()) { why = "Indisponivel agora."; return true; }
            return false;
        }

        /// <summary>Chamado todo frame pelo PlantasUi (so no cliente).</summary>
        internal static void Tick()
        {
            Run r = Current;
            Player lp = Player.m_localPlayer;
            if (r == null) return;
            if (lp == null) { Current = null; return; }

            if (BlockedNow(lp, out string why)) { CancelPlace(why + " Colocacao cancelada."); return; }
            if (!VipList.VipListApi.IsLocalPlayerVip()) { CancelPlace("VIP expirado: colocacao cancelada."); return; }

            if (r.InFlight)
            {
                if (Time.time - r.SentAt > 6f) CancelPlace("Sem resposta do servidor: colocacao cancelada.");
                return;
            }
            if (Time.time < r.NextAt) return;

            if (r.Idx >= r.Pecas.Count)
            {
                string msg = "Planta '" + r.Name + "' pronta: " + r.Placed + " colocadas, " + r.Existing + " ja existiam, " + r.Skipped + " puladas.";
                CancelPlace(msg);
                return;
            }

            Peca peca = r.Pecas[r.Idx];
            WorldOf(peca, r.Anchor, r.Yaw, out Vector3 pos, out Quaternion rot);
            GameObject prefabGo = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(peca.Prefab) : null;
            Piece piece = prefabGo != null ? prefabGo.GetComponent<Piece>() : null;
            if (piece == null || !IsAllowed(peca.Prefab)) { r.Skipped++; r.Idx++; return; }

            // Travas locais, antes de pedir o OK ao servidor.
            if (Wards.WardBridge.Governed(pos) || Wards.WardBridge.Governed(lp.transform.position))
            { CancelPlace("Zona de raid: colocacao cancelada."); return; }
            if (AlreadyThere(peca.Prefab, pos, rot)) { r.Existing++; r.Idx++; return; }
            if (Location.IsInsideNoBuildLocation(pos) || !PrivateArea.CheckAccess(pos, 0f, false, false)
                || (piece.m_onlyInBiome != 0 && (Heightmap.FindBiome(pos) & piece.m_onlyInBiome) == 0))
            { r.Skipped++; r.Idx++; return; }

            if (Vector3.Distance(lp.transform.position, pos) > MaxReach.Value)
            {
                r.Waiting = "Ande ate a planta (peca " + (r.Idx + 1) + "/" + r.Pecas.Count + ").";
                r.NextAt = Time.time + 0.5f;
                return;
            }
            if (piece.m_craftingStation != null && !lp.m_noPlacementCost
                && CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, pos) == null)
            {
                r.Skipped++; r.Idx++; return;
            }
            if (!lp.m_noPlacementCost && !lp.HaveRequirements(piece, Player.RequirementMode.CanBuild))
            {
                r.Waiting = "Falta material para " + Localization.instance.Localize(piece.m_name) + ". Pegue mais ou cancele.";
                r.NextAt = Time.time + 1f;
                return;
            }

            r.Waiting = "";
            if (r.ApprovedIdx == r.Idx) { PlaceApproved(lp, r, piece, pos, rot); return; }

            r.InFlight = true;
            r.SentAt = Time.time;
            ZPackage pkg = Op("check");
            pkg.Write(r.Idx);
            SendToServer(pkg);
        }

        private static bool AlreadyThere(string prefab, Vector3 pos, Quaternion rot)
        {
            var near = new List<Piece>();
            Piece.GetAllPiecesInRadius(pos, 0.2f, near);
            foreach (Piece p in near)
                if (p != null && Utils.GetPrefabName(p.gameObject) == prefab && Quaternion.Angle(p.transform.rotation, rot) < 2f)
                    return true;
            return false;
        }

        private static void PlaceApproved(Player lp, Run r, Piece piece, Vector3 pos, Quaternion rot)
        {
            lp.PlacePiece(piece, pos, rot, false);
            if (!lp.m_noPlacementCost && !ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))
                lp.ConsumeResources(piece.m_resources, 0);
            r.Placed++;
            r.Idx++;
            r.ApprovedIdx = -1;
            r.NextAt = Time.time + Mathf.Max(0.1f, PlaceIntervalSeconds.Value);
        }

        // ------------------------------------------------------------------ cliente: respostas

        private static void OnToClient(long sender, ZPackage pkg)
        {
            try
            {
                string op = pkg.ReadString();
                bool ok = pkg.ReadBool();
                string msg = pkg.ReadString();

                switch (op)
                {
                    case "list":
                        Saved.Clear();
                        int n = pkg.ReadInt();
                        for (int i = 0; i < n; i++) Saved.Add(new Entry { Name = pkg.ReadString(), Count = pkg.ReadInt() });
                        ListLoaded = true;
                        if (!string.IsNullOrEmpty(msg)) Status = msg;
                        break;

                    case "begin":
                        Starting = false;
                        if (!ok) { Status = msg; break; }
                        int count = pkg.ReadInt();
                        var pecas = new List<Peca>(count);
                        for (int i = 0; i < count; i++) pecas.Add(ReadPeca(pkg));
                        Vector3 anchor = new Vector3(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());
                        float yaw = pkg.ReadSingle();
                        Current = new Run { Pecas = pecas, Anchor = anchor, Yaw = yaw, Name = _startName, NextAt = Time.time };
                        Status = "";
                        break;

                    case "check":
                        Run r = Current;
                        int idx = pkg.ReadInt();
                        int code = pkg.ReadInt();
                        if (code == ResFatal) { CancelPlace(string.IsNullOrEmpty(msg) ? "Colocacao cancelada pelo servidor." : msg); break; }
                        if (r == null || idx != r.Idx) break;
                        r.InFlight = false;
                        if (code == ResOk) r.ApprovedIdx = idx;
                        else if (code == ResSkip) { r.Skipped++; r.Idx++; }
                        else if (code == ResRetry) r.NextAt = Time.time + 0.6f;
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim Plantas] erro no RPC do cliente: " + ex);
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private static class LogoutPatch
        {
            private static void Prefix()
            {
                Current = null; Starting = false;
                Saved.Clear(); ListLoaded = false; Status = "";
                PlantasUi.Close();
            }
        }

        // ------------------------------------------------------------------ comando /planta

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class ConsolePatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("planta", "(Aesir) abre a janela de plantas salvas; /planta cancelar interrompe a colocacao",
                    args =>
                    {
                        if (args.Args.Length > 1 && args.Args[1].Equals("cancelar", StringComparison.OrdinalIgnoreCase))
                        {
                            CancelPlace();
                            return;
                        }
                        PlantasUi.Open();
                    });
            }
        }
    }
}
