using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// B5 (14/10/2026): gastando a FichaBatizar, o jogador cria no mapa um marcador com nome,
    /// visivel a todos e guardado no servidor -- um arquivo de texto por mundo, como o
    /// Pvp/PvpStore.cs e o Cosmeticos.cs. O admin pode remover; o nome tem limite de tamanho
    /// (LugarNomeTamanhoMaximo).
    /// </summary>
    [HarmonyPatch]
    internal static class Lugares
    {
        private const string ToServer = "DH_Lugares_ToServer";
        private const string ToClient = "DH_Lugares_ToClient";
        private const string FichaBatizarNome = "Ficha de Batizar Lugar";

        public static ConfigEntry<bool> Ativo;
        public static ConfigEntry<int> NomeTamanhoMaximo;

        public static void Bind(ConfigFile config)
        {
            const string secao = "Lugares";
            Ativo = Plugin.Synced(config.Bind(secao, "LugaresAtivo", true,
                "Liga o /batizar: gasta a Ficha de Batizar Lugar para criar, onde o jogador estiver, um marcador com nome visivel a todos no mapa."));
            NomeTamanhoMaximo = Plugin.Synced(config.Bind(secao, "LugarNomeTamanhoMaximo", 24,
                new ConfigDescription("Tamanho maximo do nome de um lugar batizado.", new AcceptableValueRange<int>(1, 64))));
        }

        private sealed class Lugar
        {
            public string Nome;
            public float X, Y, Z;
            public long CriadorId;
            public string CriadorNome;
        }

        // Servidor: lista com autoridade sobre os lugares batizados.
        private static readonly List<Lugar> _lugares = new List<Lugar>();
        private static string _path;
        private static bool _carregado;

        // Cliente: pins que esta sessao colocou no mapa (para poder remover exatamente esses).
        private static readonly List<Minimap.PinData> _pinsLocais = new List<Minimap.PinData>();

        // ---------------------------------------------------------------- armazenamento (servidor)

        private static string WorldFileName()
        {
            string world = ZNet.instance != null ? ZNet.instance.GetWorldName() : "world";
            foreach (char c in Path.GetInvalidFileNameChars()) world = world.Replace(c, '_');
            return Path.Combine(Path.Combine(Paths.ConfigPath, "Deadheim"), "lugares-" + world + ".txt");
        }

        private static void EnsureLoaded()
        {
            string path = WorldFileName();
            if (_carregado && path == _path) return;
            _path = path;
            _lugares.Clear();
            try
            {
                if (File.Exists(path))
                {
                    foreach (string linha in File.ReadAllLines(path))
                    {
                        if (string.IsNullOrWhiteSpace(linha)) continue;
                        string[] c = linha.Split('\t');
                        if (c.Length < 6) continue;
                        if (!float.TryParse(c[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) continue;
                        if (!float.TryParse(c[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) continue;
                        if (!float.TryParse(c[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) continue;
                        if (!long.TryParse(c[4], out long criadorId)) continue;
                        _lugares.Add(new Lugar { Nome = c[0], X = x, Y = y, Z = z, CriadorId = criadorId, CriadorNome = c[5] });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim] Lugares: {path} ilegivel ({ex.Message}); comecando vazio.");
                _lugares.Clear();
            }
            _carregado = true;
        }

        private static void Save()
        {
            if (string.IsNullOrEmpty(_path)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                IEnumerable<string> linhas = _lugares.Select(l => string.Join("\t",
                    l.Nome,
                    l.X.ToString("R", CultureInfo.InvariantCulture),
                    l.Y.ToString("R", CultureInfo.InvariantCulture),
                    l.Z.ToString("R", CultureInfo.InvariantCulture),
                    l.CriadorId.ToString(CultureInfo.InvariantCulture),
                    l.CriadorNome));
                string temp = _path + ".tmp";
                File.WriteAllLines(temp, linhas);
                File.Copy(temp, _path, true);
                File.Delete(temp);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Lugares: nao foi possivel salvar " + _path + ": " + ex.Message);
            }
        }

        // ------------------------------------------------------------------------------- rede

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
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            long real = RemetenteRpc.Real(sender, out _);
            if (real == 0L) return;

            string op;
            try { op = pkg.ReadString(); }
            catch (Exception ex) { Debug.LogError("[Deadheim] Lugares: pacote de " + real + " falhou: " + ex); return; }

            if (op == "hello")
            {
                EnsureLoaded();
                SendAllTo(real);
                return;
            }

            if (op == "batizar")
            {
                if (!Ativo.Value || !Pvp.PvpPeer.TryResolve(real, out Pvp.PvpPeer peer)) return;
                string nome = (pkg.ReadString() ?? "").Trim();
                float x = pkg.ReadSingle(), y = pkg.ReadSingle(), z = pkg.ReadSingle();
                int max = Mathf.Max(1, NomeTamanhoMaximo.Value);
                if (nome.Length == 0 || nome.Length > max)
                {
                    Pvp.PvpNet.Message(real, $"Nome invalido (1 a {max} caracteres).");
                    return;
                }

                EnsureLoaded();
                if (_lugares.Any(l => string.Equals(l.Nome, nome, StringComparison.OrdinalIgnoreCase)))
                {
                    Pvp.PvpNet.Message(real, $"Ja existe um lugar chamado '{nome}'.");
                    return;
                }

                Lugar lugar = new Lugar { Nome = nome, X = x, Y = y, Z = z, CriadorId = peer.PlayerId, CriadorNome = peer.Name };
                _lugares.Add(lugar);
                Save();
                BroadcastAdd(lugar);
                Pvp.PvpNet.Broadcast($"{peer.Name} batizou um lugar: {nome}.");
                Debug.Log($"[Deadheim] Lugares: {peer.Name} batizou '{nome}' em ({x:F0},{z:F0}).");
                return;
            }

            if (op == "remover")
            {
                if (!Pvp.PvpServer.IsAdminPeer(real)) { Pvp.PvpNet.Message(real, "So admin."); return; }
                string nome = pkg.ReadString() ?? "";
                EnsureLoaded();
                int removidos = _lugares.RemoveAll(l => string.Equals(l.Nome, nome, StringComparison.OrdinalIgnoreCase));
                if (removidos > 0) { Save(); BroadcastRemove(nome); }
                Pvp.PvpNet.Message(real, removidos > 0 ? $"'{nome}' removido." : $"Nao achei '{nome}'.");
            }
        }

        private static void SendAllTo(long peerId)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write("tudo");
            pkg.Write(_lugares.Count);
            foreach (Lugar l in _lugares)
            {
                pkg.Write(l.Nome);
                pkg.Write(l.X); pkg.Write(l.Y); pkg.Write(l.Z);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(peerId, ToClient, pkg);
        }

        private static void BroadcastAdd(Lugar l)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write("add");
            pkg.Write(l.Nome);
            pkg.Write(l.X); pkg.Write(l.Y); pkg.Write(l.Z);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, ToClient, pkg);
        }

        private static void BroadcastRemove(string nome)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write("remover");
            pkg.Write(nome);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, ToClient, pkg);
        }

        // ------------------------------------------------------------------- cliente: pins no mapa

        private static void OnToClient(long sender, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID()) return;
            string op;
            try { op = pkg.ReadString(); }
            catch (Exception ex) { Debug.LogError("[Deadheim] Lugares: pacote do servidor falhou: " + ex); return; }

            if (op == "tudo")
            {
                LimparPinsLocais();
                int n = pkg.ReadInt();
                for (int i = 0; i < n; i++)
                {
                    string nome = pkg.ReadString();
                    float x = pkg.ReadSingle(), y = pkg.ReadSingle(), z = pkg.ReadSingle();
                    AdicionarPin(nome, x, y, z);
                }
            }
            else if (op == "add")
            {
                string nome = pkg.ReadString();
                float x = pkg.ReadSingle(), y = pkg.ReadSingle(), z = pkg.ReadSingle();
                AdicionarPin(nome, x, y, z);
            }
            else if (op == "remover")
            {
                RemoverPin(pkg.ReadString());
            }
        }

        private static void AdicionarPin(string nome, float x, float y, float z)
        {
            if (Minimap.instance == null) return;
            Minimap.PinData pin = Minimap.instance.AddPin(new Vector3(x, y, z), Minimap.PinType.Icon3, nome, save: false, isChecked: false);
            if (pin != null) _pinsLocais.Add(pin);
        }

        private static void RemoverPin(string nome)
        {
            if (Minimap.instance == null || string.IsNullOrEmpty(nome)) return;
            for (int i = _pinsLocais.Count - 1; i >= 0; i--)
            {
                Minimap.PinData pin = _pinsLocais[i];
                if (pin == null) { _pinsLocais.RemoveAt(i); continue; }
                if (string.Equals(pin.m_name, nome, StringComparison.OrdinalIgnoreCase))
                {
                    Minimap.instance.RemovePin(pin);
                    _pinsLocais.RemoveAt(i);
                }
            }
        }

        private static void LimparPinsLocais()
        {
            if (Minimap.instance != null)
                foreach (Minimap.PinData pin in _pinsLocais)
                    if (pin != null) Minimap.instance.RemovePin(pin);
            _pinsLocais.Clear();
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class SpawnPatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer || ZRoutedRpc.instance == null) return;
                ZPackage pkg = new ZPackage();
                pkg.Write("hello");
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), ToServer, pkg);
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private static class LogoutPatch
        {
            // Os pins somem com o mapa (mundo novo no proximo login); so limpa a lista daqui.
            private static void Postfix() => _pinsLocais.Clear();
        }

        // -------------------------------------------------------------------- comando /batizar

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class ComandoPatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("batizar", "<nome> - da um nome ao lugar onde voce esta (gasta a Ficha de Batizar Lugar); admin: batizar remover <nome>",
                    args =>
                    {
                        Player player = Player.m_localPlayer;
                        if (player == null || ZRoutedRpc.instance == null) return;

                        if (args.Length < 2) { args.Context.AddString("batizar <nome> | (admin) batizar remover <nome>"); return; }

                        if (string.Equals(args[1], "remover", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!Vanilla.Admin.LocalPlayerIsAdmin()) { args.Context.AddString("So admin."); return; }
                            if (args.Length < 3) { args.Context.AddString("batizar remover <nome>"); return; }
                            string alvo = string.Join(" ", args.Args, 2, args.Length - 2);
                            ZPackage rem = new ZPackage();
                            rem.Write("remover");
                            rem.Write(alvo);
                            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), ToServer, rem);
                            return;
                        }

                        if (!Ativo.Value) { args.Context.AddString("Batizar lugares esta desligado neste servidor."); return; }

                        string nome = string.Join(" ", args.Args, 1, args.Length - 1).Trim();
                        int max = Mathf.Max(1, NomeTamanhoMaximo.Value);
                        if (nome.Length == 0 || nome.Length > max)
                        {
                            args.Context.AddString($"Nome invalido (1 a {max} caracteres).");
                            return;
                        }

                        Inventory inv = player.GetInventory();
                        if (inv.CountItems(FichaBatizarNome, -1, false) <= 0)
                        {
                            args.Context.AddString("Voce precisa de uma Ficha de Batizar Lugar.");
                            return;
                        }

                        inv.RemoveItem(FichaBatizarNome, 1, -1, false);
                        Vector3 pos = player.transform.position;
                        ZPackage pkg = new ZPackage();
                        pkg.Write("batizar");
                        pkg.Write(nome);
                        pkg.Write(pos.x); pkg.Write(pos.y); pkg.Write(pos.z);
                        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), ToServer, pkg);
                        args.Context.AddString($"Batizando '{nome}'...");
                    });
            }
        }

        /// <summary>Intercepta o "Usar" da FichaBatizar no inventario: avisa a usar o comando /batizar.</summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        private static class UsarFichaPatch
        {
            private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
            {
                if (__instance != Player.m_localPlayer || item?.m_dropPrefab == null) return true;
                if (item.m_dropPrefab.name != "FichaBatizar") return true;
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Use o comando /batizar <nome> onde você quer batizar.");
                return false;
            }
        }
    }
}
