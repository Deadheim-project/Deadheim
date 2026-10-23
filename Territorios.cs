using BepInEx;
using BepInEx.Configuration;
using Deadheim.Vanilla;
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
    /// Modo territorio (como o do KGmarketplace): o admin demarca areas circulares no mundo e liga,
    /// para cada uma, as regras que valem la dentro:
    ///   construir  - ninguem constroi nem desmonta, a nao ser admin e os jogadores permitidos;
    ///   estruturas - as construcoes nao tomam dano nenhum (raid, monstro, jogador);
    ///   seguro     - zona segura do PvP (ninguem se fere).
    /// A lista fica no servidor, um arquivo de texto por mundo (como o Lugares.cs), e e enviada
    /// a cada cliente ao entrar e a cada mudanca. Quem constroi e quem apanha decide no proprio
    /// cliente, como o resto do mod; so o admin consegue mudar a lista (conferido no servidor).
    /// </summary>
    [HarmonyPatch]
    internal static class Territorios
    {
        private const string ToServer = "DH_Territorios_ToServer";
        private const string ToClient = "DH_Territorios_ToClient";
        private const float RaioPadrao = 50f;
        private const float RaioMaximo = 3000f;
        private const int NomeMaximo = 32;

        public static ConfigEntry<bool> Ativo;

        public static void Bind(ConfigFile config)
        {
            Ativo = Plugin.Synced(config.Bind("Territorios", "TerritoriosAtivo", true,
                "Liga o modo territorio: as areas que o admin demarca (/territorio) passam a valer. Desligado, nenhuma regra de territorio vale."));
        }

        internal sealed class Territorio
        {
            public string Nome;
            public float X, Z, Raio;
            public bool Construir = true;
            public bool Estruturas = true;
            public bool Seguro;
            /// <summary>Bloqueia stands de item/armadura, itens no chao e baus para quem nao e permitido.</summary>
            public bool Interacao;
            public List<string> Permitidos = new List<string>();

            public bool Contem(Vector3 p)
            {
                float dx = p.x - X, dz = p.z - Z;
                return dx * dx + dz * dz <= Raio * Raio;
            }

            public bool Permite(string jogador)
                => !string.IsNullOrEmpty(jogador) && Permitidos.Any(n => string.Equals(n, jogador, StringComparison.OrdinalIgnoreCase));

            public string Flags()
                => string.Join(", ", new[]
                {
                    "construir=" + (Construir ? "bloqueado" : "livre"),
                    "estruturas=" + (Estruturas ? "invulneraveis" : "normais"),
                    "seguro=" + (Seguro ? "sim" : "nao"),
                    "interacao=" + (Interacao ? "bloqueada" : "livre"),
                });
        }

        // Servidor: lista com autoridade (arquivo). Cliente: copia recebida do servidor.
        private static List<Territorio> _lista = new List<Territorio>();
        private static string _path;
        private static bool _carregado;

        private static string _dentro;
        private static float _proximaChecagem;

        private static List<Territorio> Lista
        {
            get
            {
                if (ZNet.instance != null && ZNet.instance.IsServer()) EnsureLoaded();
                return _lista;
            }
        }

        // ------------------------------------------------------------------ consultas

        public static Territorio Em(Vector3 ponto)
        {
            if (Ativo == null || !Ativo.Value) return null;
            List<Territorio> lista = Lista;
            for (int i = 0; i < lista.Count; i++)
                if (lista[i].Contem(ponto)) return lista[i];
            return null;
        }

        /// <summary>Nome do territorio seguro que cobre o ponto, para o PvP (ou null).</summary>
        public static string NomeSeguro(Vector3 ponto)
        {
            Territorio t = Em(ponto);
            return t != null && t.Seguro ? "Territorio " + t.Nome : null;
        }

        private static bool PodeConstruir(Player player, Territorio t)
            => !t.Construir || Admin.LocalPlayerIsAdmin() || t.Permite(player.GetPlayerName());

        // ---------------------------------------------------------------- armazenamento (servidor)

        private static string ArquivoDoMundo()
        {
            string world = ZNet.instance != null ? ZNet.instance.GetWorldName() : "world";
            foreach (char c in Path.GetInvalidFileNameChars()) world = world.Replace(c, '_');
            return Path.Combine(Path.Combine(Paths.ConfigPath, "Deadheim"), "territorios-" + world + ".txt");
        }

        private static string Limpar(string texto)
            => (texto ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();

        private static void EnsureLoaded()
        {
            string path = ArquivoDoMundo();
            if (_carregado && path == _path) return;
            _path = path;
            _lista = new List<Territorio>();
            try
            {
                if (File.Exists(path))
                {
                    foreach (string linha in File.ReadAllLines(path))
                    {
                        if (string.IsNullOrWhiteSpace(linha)) continue;
                        string[] c = linha.Split('\t');
                        if (c.Length < 7) continue;
                        if (!float.TryParse(c[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) continue;
                        if (!float.TryParse(c[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) continue;
                        if (!float.TryParse(c[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float r)) continue;
                        _lista.Add(new Territorio
                        {
                            Nome = c[0], X = x, Z = z, Raio = r,
                            Construir = c[4] == "1", Estruturas = c[5] == "1", Seguro = c[6] == "1",
                            Interacao = c.Length > 8 && c[8] == "1",
                            Permitidos = (c.Length > 7 ? c[7] : "").Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToList(),
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim] Territorios: {path} ilegivel ({ex.Message}); comecando vazio.");
                _lista = new List<Territorio>();
            }
            _carregado = true;
        }

        private static void Save()
        {
            if (string.IsNullOrEmpty(_path)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                IEnumerable<string> linhas = _lista.Select(t => string.Join("\t",
                    t.Nome,
                    t.X.ToString("R", CultureInfo.InvariantCulture),
                    t.Z.ToString("R", CultureInfo.InvariantCulture),
                    t.Raio.ToString("R", CultureInfo.InvariantCulture),
                    t.Construir ? "1" : "0", t.Estruturas ? "1" : "0", t.Seguro ? "1" : "0",
                    string.Join(",", t.Permitidos),
                    t.Interacao ? "1" : "0"));
                string temp = _path + ".tmp";
                File.WriteAllLines(temp, linhas);
                File.Copy(temp, _path, true);
                File.Delete(temp);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Territorios: nao foi possivel salvar " + _path + ": " + ex.Message);
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

        private static void EnviarPara(long peerId)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(_lista.Count);
            foreach (Territorio t in _lista)
            {
                pkg.Write(t.Nome);
                pkg.Write(t.X); pkg.Write(t.Z); pkg.Write(t.Raio);
                pkg.Write(t.Construir); pkg.Write(t.Estruturas); pkg.Write(t.Seguro);
                pkg.Write(string.Join(",", t.Permitidos));
                pkg.Write(t.Interacao);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(peerId, ToClient, pkg);
        }

        private static void OnToClient(long sender, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID()) return;
            // O servidor (ou host) ja tem a lista com autoridade, lida do arquivo.
            if (ZNet.instance != null && ZNet.instance.IsServer()) return;
            try
            {
                List<Territorio> nova = new List<Territorio>();
                int n = pkg.ReadInt();
                for (int i = 0; i < n; i++)
                {
                    Territorio t = new Territorio
                    {
                        Nome = pkg.ReadString(),
                        X = pkg.ReadSingle(), Z = pkg.ReadSingle(), Raio = pkg.ReadSingle(),
                        Construir = pkg.ReadBool(), Estruturas = pkg.ReadBool(), Seguro = pkg.ReadBool(),
                    };
                    t.Permitidos = (pkg.ReadString() ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    t.Interacao = pkg.ReadBool();
                    nova.Add(t);
                }
                _lista = nova;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Territorios: pacote do servidor falhou: " + ex);
            }
        }

        private static void OnToServer(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            long real = RemetenteRpc.Real(sender, out _);
            if (real == 0L) return;

            string op;
            try { op = pkg.ReadString(); }
            catch (Exception ex) { Debug.LogError("[Deadheim] Territorios: pacote de " + real + " falhou: " + ex); return; }

            EnsureLoaded();
            if (op == "hello") { EnviarPara(real); return; }

            if (!Pvp.PvpServer.IsAdminPeer(real)) { Pvp.PvpNet.Message(real, "So admin."); return; }

            try
            {
                string nome = Limpar(pkg.ReadString());
                Territorio t = _lista.FirstOrDefault(x => string.Equals(x.Nome, nome, StringComparison.OrdinalIgnoreCase));

                switch (op)
                {
                    case "criar":
                    {
                        float x = pkg.ReadSingle(), z = pkg.ReadSingle(), raio = pkg.ReadSingle();
                        if (nome.Length == 0 || nome.Length > NomeMaximo || nome.Any(char.IsWhiteSpace))
                        { Pvp.PvpNet.Message(real, $"Nome invalido (1 a {NomeMaximo} caracteres, sem espacos)."); return; }
                        if (t != null) { Pvp.PvpNet.Message(real, $"Ja existe o territorio '{nome}'."); return; }
                        if (!(raio >= 1f && raio <= RaioMaximo)) { Pvp.PvpNet.Message(real, $"Raio de 1 a {RaioMaximo:0} m."); return; }
                        _lista.Add(new Territorio { Nome = nome, X = x, Z = z, Raio = raio });
                        Pvp.PvpNet.Message(real, $"Territorio '{nome}' criado: raio {raio:0} m, construcao bloqueada e estruturas invulneraveis.");
                        Debug.Log($"[Deadheim] Territorios: '{nome}' criado em ({x:F0},{z:F0}) raio {raio:F0}.");
                        break;
                    }
                    case "remover":
                        if (t == null) { Pvp.PvpNet.Message(real, $"Nao achei '{nome}'."); return; }
                        _lista.Remove(t);
                        Pvp.PvpNet.Message(real, $"Territorio '{nome}' removido.");
                        break;
                    case "editar":
                    {
                        float raio = pkg.ReadSingle();
                        bool construir = pkg.ReadBool(), estruturas = pkg.ReadBool(), seguro = pkg.ReadBool();
                        string permitidos = pkg.ReadString() ?? "";
                        bool interacao = pkg.ReadBool();
                        if (t == null) { Pvp.PvpNet.Message(real, $"Nao achei '{nome}'."); return; }
                        if (!(raio >= 1f && raio <= RaioMaximo)) { Pvp.PvpNet.Message(real, $"Raio de 1 a {RaioMaximo:0} m."); return; }
                        t.Raio = raio; t.Construir = construir; t.Estruturas = estruturas; t.Seguro = seguro; t.Interacao = interacao;
                        t.Permitidos = permitidos.Split(',').Select(Limpar).Where(s => s.Length > 0 && !s.Contains(',')).Distinct().ToList();
                        Pvp.PvpNet.Message(real, $"'{nome}' atualizado: raio {raio:0} m, {t.Flags()}.");
                        break;
                    }
                    default:
                        return;
                }
                Save();
                EnviarPara(ZRoutedRpc.Everybody);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Territorios: pedido '" + op + "' de " + real + " falhou: " + ex);
            }
        }

        /// <summary>Copia local da lista (a do servidor, no cliente); so leitura.</summary>
        internal static IReadOnlyList<Territorio> Todos => _lista;

        internal static void PedirCriar(string nome, Vector3 onde, float raio)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write("criar"); pkg.Write(nome); pkg.Write(onde.x); pkg.Write(onde.z); pkg.Write(raio);
            Pedir(pkg);
        }

        internal static void PedirRemover(string nome)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write("remover"); pkg.Write(nome);
            Pedir(pkg);
        }

        internal static void PedirEditar(string nome, float raio, bool construir, bool estruturas, bool seguro, bool interacao, IEnumerable<string> permitidos)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write("editar"); pkg.Write(nome); pkg.Write(raio);
            pkg.Write(construir); pkg.Write(estruturas); pkg.Write(seguro);
            pkg.Write(string.Join(",", permitidos));
            pkg.Write(interacao);
            Pedir(pkg);
        }

        private static void Pedir(ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), ToServer, pkg);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class SpawnPatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                ZPackage pkg = new ZPackage();
                pkg.Write("hello");
                Pedir(pkg);
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private static class LogoutPatch
        {
            private static void Postfix()
            {
                _dentro = null;
                if (!(ZNet.instance != null && ZNet.instance.IsServer())) _lista = new List<Territorio>();
            }
        }

        // ------------------------------------------------------------------ regras (cliente e servidor)

        [HarmonyPatch(typeof(Player), "PlacePiece")]
        private static class PlacePiecePatch
        {
            private static bool Prefix(Player __instance)
            {
                if (__instance == null || __instance != Player.m_localPlayer) return true;
                Territorio t = Em(__instance.transform.position);
                if (t == null || PodeConstruir(__instance, t)) return true;
                __instance.Message(MessageHud.MessageType.Center, $"Territorio {t.Nome}: construcao bloqueada.");
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), "CheckCanRemovePiece")]
        private static class RemoverPatch
        {
            private static void Postfix(Player __instance, Piece piece, ref bool __result)
            {
                if (!__result || piece == null || __instance == null) return;
                Territorio t = Em(piece.transform.position);
                if (t == null || PodeConstruir(__instance, t)) return;
                __result = false;
                __instance.Message(MessageHud.MessageType.Center, $"Territorio {t.Nome}: nao da para desmontar aqui.");
            }
        }

        // ---------------------------------------------------- interacao: stands, itens no chao, baus

        private static bool InteracaoBloqueada(Component alvo, Humanoid usuario)
        {
            Player player = usuario as Player;
            if (alvo == null || player == null || player != Player.m_localPlayer) return false;
            Territorio t = Em(alvo.transform.position);
            if (t == null || !t.Interacao || Admin.LocalPlayerIsAdmin() || t.Permite(player.GetPlayerName())) return false;
            player.Message(MessageHud.MessageType.Center, $"Territorio {t.Nome}: voce nao pode mexer nisso.");
            return true;
        }

        [HarmonyPatch(typeof(ItemStand), "Interact")]
        private static class StandInteragirPatch
        {
            private static bool Prefix(ItemStand __instance, Humanoid user, ref bool __result)
            {
                if (!InteracaoBloqueada(__instance, user)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(ItemStand), "UseItem")]
        private static class StandUsarPatch
        {
            private static bool Prefix(ItemStand __instance, Humanoid user, ref bool __result)
            {
                if (!InteracaoBloqueada(__instance, user)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(ArmorStand), "Interact")]
        private static class ArmaduraInteragirPatch
        {
            private static bool Prefix(ArmorStand __instance, Humanoid user, ref bool __result)
            {
                if (!InteracaoBloqueada(__instance, user)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(ArmorStand), "UseItem")]
        private static class ArmaduraUsarPatch
        {
            private static bool Prefix(ArmorStand __instance, Humanoid user, ref bool __result)
            {
                if (!InteracaoBloqueada(__instance, user)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(ItemDrop), "Interact")]
        private static class ItemNoChaoPatch
        {
            private static bool Prefix(ItemDrop __instance, Humanoid character, ref bool __result)
            {
                if (!InteracaoBloqueada(__instance, character)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Container), "Interact")]
        private static class BauPatch
        {
            private static bool Prefix(Container __instance, Humanoid character, ref bool __result)
            {
                if (!InteracaoBloqueada(__instance, character)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
        private static class DanoPatch
        {
            [HarmonyPriority(Priority.High)]
            private static bool Prefix(WearNTear __instance)
            {
                try
                {
                    if (__instance == null) return true;
                    Territorio t = Em(__instance.transform.position);
                    return t == null || !t.Estruturas;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Deadheim] Territorios: checagem de dano falhou: " + ex.Message);
                    return true;
                }
            }
        }

        // ------------------------------------------------------------- aviso de entrada e saida

        /// <summary>Chamado a cada quadro pelo Plugin.Update; confere a posicao duas vezes por segundo.</summary>
        public static void Tick(Player player)
        {
            if (player == null) { _dentro = null; return; }
            if (Time.time < _proximaChecagem) return;
            _proximaChecagem = Time.time + 0.5f;

            string agora = Em(player.transform.position)?.Nome;
            if (agora == _dentro) return;
            if (_dentro != null) player.Message(MessageHud.MessageType.Center, "Saindo do territorio " + _dentro);
            if (agora != null) player.Message(MessageHud.MessageType.Center, "Territorio " + agora + ": " + Em(player.transform.position).Flags());
            _dentro = agora;
        }

        // -------------------------------------------------------------------- comando /territorio

        private const string Ajuda =
            "territorio (sem argumentos abre a janela) | criar <nome> [raio] | remover <nome> | raio <nome> <metros> | construir|estruturas|seguro|interacao <nome> <on|off> | permitir|negar <nome> <jogador> | lista | aqui";

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class ComandoPatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("territorio", "(admin) " + Ajuda, Comando);
            }
        }

        private static void Comando(Terminal.ConsoleEventArgs args)
        {
            Terminal ctx = args.Context;
            Player eu = Player.m_localPlayer;
            if (eu == null || ZRoutedRpc.instance == null) return;
            if (!Admin.LocalPlayerIsAdmin()) { ctx.AddString("So admin."); return; }

            if (args.Length < 2) { TerritoriosUi.Abrir(); return; }

            string sub = args[1].ToLowerInvariant();
            string nome = args.Length >= 3 ? args[2] : "";

            if (sub == "lista")
            {
                if (_lista.Count == 0) ctx.AddString("Nenhum territorio.");
                foreach (Territorio t in _lista)
                    ctx.AddString($"{t.Nome}: ({t.X:F0},{t.Z:F0}) raio {t.Raio:0} m, {t.Flags()}" +
                                  (t.Permitidos.Count > 0 ? ", permitidos: " + string.Join(", ", t.Permitidos) : ""));
                return;
            }
            if (sub == "aqui")
            {
                Territorio t = Em(eu.transform.position);
                ctx.AddString(t == null ? "Voce nao esta em nenhum territorio." : $"Territorio {t.Nome}, {t.Flags()}.");
                return;
            }
            if (nome.Length == 0) { ctx.AddString(Ajuda); return; }

            if (sub == "criar")
            {
                float raio = RaioPadrao;
                if (args.Length >= 4 && !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out raio))
                { ctx.AddString("Raio invalido."); return; }
                PedirCriar(nome, eu.transform.position, raio);
                return;
            }
            if (sub == "remover")
            {
                PedirRemover(nome);
                return;
            }

            Territorio alvo = _lista.FirstOrDefault(x => string.Equals(x.Nome, nome, StringComparison.OrdinalIgnoreCase));
            if (alvo == null) { ctx.AddString($"Nao achei '{nome}'. Use: territorio lista"); return; }

            float novoRaio = alvo.Raio;
            bool construir = alvo.Construir, estruturas = alvo.Estruturas, seguro = alvo.Seguro, interacao = alvo.Interacao;
            List<string> permitidos = new List<string>(alvo.Permitidos);
            string valor = args.Length >= 4 ? args[3] : "";

            switch (sub)
            {
                case "raio":
                    if (!float.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out novoRaio))
                    { ctx.AddString("territorio raio <nome> <metros>"); return; }
                    break;
                case "construir":
                case "estruturas":
                case "seguro":
                case "interacao":
                {
                    bool? ligado = valor == "on" ? true : valor == "off" ? (bool?)false : null;
                    if (ligado == null) { ctx.AddString($"territorio {sub} <nome> <on|off>"); return; }
                    if (sub == "construir") construir = ligado.Value;
                    else if (sub == "estruturas") estruturas = ligado.Value;
                    else if (sub == "interacao") interacao = ligado.Value;
                    else seguro = ligado.Value;
                    break;
                }
                case "permitir":
                case "negar":
                {
                    string jogador = args.Length >= 4 ? string.Join(" ", args.Args, 3, args.Length - 3).Trim() : "";
                    if (jogador.Length == 0) { ctx.AddString($"territorio {sub} <nome> <jogador>"); return; }
                    permitidos.RemoveAll(n => string.Equals(n, jogador, StringComparison.OrdinalIgnoreCase));
                    if (sub == "permitir") permitidos.Add(jogador);
                    break;
                }
                default:
                    ctx.AddString(Ajuda);
                    return;
            }

            PedirEditar(alvo.Nome, novoRaio, construir, estruturas, seguro, interacao, permitidos);
        }
    }
}
