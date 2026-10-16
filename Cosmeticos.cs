using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Registro de cosmeticos liberados por personagem (B3, base comum aprovada em 14/10/2026):
    /// o que cada jogador comprou ou ganhou (titulo agora; chama, lapide, efeito de abate, vela,
    /// skin de montaria, pacote de Halloween e passe de temporada depois) fica guardado so no
    /// servidor -- um arquivo de texto por mundo, como o Pvp/PvpStore.cs -- e e sincronizado ao
    /// dono quando ele entra no mundo ou ganha um cosmetico novo. So o servidor grava: cliente
    /// modificado nao se da cosmetico, so pede (OpResgatar) e o servidor decide.
    ///
    /// API publica por reflexao: Deadheim.CosmeticosApi, CosmeticosApi.Version (mesmo esquema do
    /// Pvp/PvpApi.cs e do TreinadorApi -- sem referencia de compilacao entre os mods). O NpcValheim
    /// concede (servidor, apos confirmar a compra) com Grant/Has; as Velas consultam o que o
    /// jogador local tem com LocalHas/LocalKeys. Chave e um texto livre "categoria:nome"
    /// (ex.: "titulo:fundador"); cada mod decide as proprias chaves e documenta no plano.
    /// </summary>
    [HarmonyPatch]
    internal static class Cosmeticos
    {
        private const string ToServer = "DH_Cosm_ToServer";
        private const string ToClient = "DH_Cosm_ToClient";

        public const string TituloFundador = "titulo:fundador";

        // Ficha consumida (prefab) -> chave que ela libera. So a que ja tem para onde ir nesta
        // onda; Barbeiro, Halloween e Temporada entram quando os NPCs/o conteudo deles existirem.
        // FichaPet é especial: abre uma lista de pets para escolha (implementação futura com UI).
        private static readonly Dictionary<string, string> FichaParaChave = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "FichaFundador", TituloFundador },
            // FichaPet é tratada separadamente (abre lista de pets)
        };

        // ------------------------------------------------------------ armazenamento (servidor)

        private static Dictionary<long, HashSet<string>> _porJogador;
        private static string _path;
        private static bool _dirty;

        private static string WorldFileName()
        {
            string world = ZNet.instance != null ? ZNet.instance.GetWorldName() : "world";
            foreach (char c in Path.GetInvalidFileNameChars()) world = world.Replace(c, '_');
            return Path.Combine(Path.Combine(Paths.ConfigPath, "Deadheim"), "cosmeticos-" + world + ".txt");
        }

        private static void EnsureLoaded()
        {
            string path = WorldFileName();
            if (_porJogador != null && path == _path) return;
            if (_porJogador != null && _dirty) Save();

            _path = path;
            _porJogador = new Dictionary<long, HashSet<string>>();
            try
            {
                if (File.Exists(path))
                {
                    foreach (string linha in File.ReadAllLines(path))
                    {
                        if (string.IsNullOrWhiteSpace(linha)) continue;
                        string[] campos = linha.Split('\t');
                        if (campos.Length < 2 || !long.TryParse(campos[0], out long id)) continue;
                        _porJogador[id] = new HashSet<string>(
                            campos[1].Split(',').Where(k => k.Length > 0), StringComparer.Ordinal);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim] Cosmeticos: {path} ilegivel ({ex.Message}); comecando vazio.");
                _porJogador = new Dictionary<long, HashSet<string>>();
            }
            _dirty = false;
        }

        private static void Save()
        {
            if (_porJogador == null || string.IsNullOrEmpty(_path)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                IEnumerable<string> linhas = _porJogador.Where(kv => kv.Value.Count > 0)
                    .Select(kv => kv.Key + "\t" + string.Join(",", kv.Value));
                string temp = _path + ".tmp";
                File.WriteAllLines(temp, linhas);
                // Copy + Delete e nao File.Replace: o servidor da DatHost roda Mono em Linux.
                File.Copy(temp, _path, true);
                File.Delete(temp);
                _dirty = false;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Cosmeticos: nao foi possivel salvar " + _path + ": " + ex.Message);
            }
        }

        private static HashSet<string> SetOf(long playerId)
        {
            EnsureLoaded();
            if (!_porJogador.TryGetValue(playerId, out HashSet<string> set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                _porJogador[playerId] = set;
            }
            return set;
        }

        /// <summary>Concede (idempotente). Devolve true se concedeu agora (false = ja tinha).</summary>
        internal static bool ServerGrant(long playerId, string key)
        {
            if (playerId == 0L || string.IsNullOrWhiteSpace(key)) return false;
            HashSet<string> set = SetOf(playerId);
            if (!set.Add(key)) return false;
            _dirty = true;
            Save();
            SendStateTo(playerId);
            return true;
        }

        internal static bool ServerHas(long playerId, string key)
        {
            EnsureLoaded();
            return playerId != 0L && _porJogador.TryGetValue(playerId, out HashSet<string> set) && set.Contains(key);
        }

        private static void SendStateTo(long playerId)
        {
            if (ZRoutedRpc.instance == null || !Pvp.PvpPeer.TryFindByPlayerId(playerId, out Pvp.PvpPeer peer)) return;
            HashSet<string> set = SetOf(playerId);
            ZPackage pkg = new ZPackage();
            pkg.Write("estado");
            pkg.Write(string.Join(",", set));
            ZRoutedRpc.instance.InvokeRoutedRPC(peer.PeerId, ToClient, pkg);
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
            if (real == 0L || !Pvp.PvpPeer.TryResolve(real, out Pvp.PvpPeer peer)) return;
            try
            {
                string op = pkg.ReadString();
                if (op == "hello")
                {
                    SendStateTo(peer.PlayerId);
                }
                else if (op == "resgatar")
                {
                    string ficha = pkg.ReadString();
                    if (!FichaParaChave.TryGetValue(ficha, out string chave)) return;
                    bool concedeu = ServerGrant(peer.PlayerId, chave);
                    Pvp.PvpNet.Message(real, concedeu ? "Resgatado!" : "Voce ja tinha isso.");
                    Debug.Log($"[Deadheim] Cosmeticos: {peer.Name} trocou {ficha} por '{chave}' ({(concedeu ? "concedido" : "ja tinha")}).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Cosmeticos: pacote do cliente " + real + " falhou: " + ex);
            }
        }

        // ------------------------------------------------------------- cliente: cache e API

        private static readonly HashSet<string> _local = new HashSet<string>(StringComparer.Ordinal);

        private static void OnToClient(long sender, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID()) return;
            try
            {
                string op = pkg.ReadString();
                if (op != "estado") return;
                string csv = pkg.ReadString() ?? "";
                _local.Clear();
                foreach (string k in csv.Split(',')) if (k.Length > 0) _local.Add(k);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Cosmeticos: pacote do servidor falhou: " + ex);
            }
        }

        internal static bool LocalHas(string key) => key != null && _local.Contains(key);
        internal static string[] LocalKeys() => _local.ToArray();

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class SpawnPatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer || ZRoutedRpc.instance == null) return;
                _local.Clear();
                ZPackage pkg = new ZPackage();
                pkg.Write("hello");
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), ToServer, pkg);
            }
        }

        // ------------------------------------------------------- resgate das fichas (B3: Fundador)

        /// <summary>"Usar" a ficha no inventario troca ela pelo cosmetico, sem precisar de NPC.</summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        private static class UsarFichaPatch
        {
            private static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item)
            {
                if (__instance != Player.m_localPlayer || item?.m_dropPrefab == null || ZRoutedRpc.instance == null) return true;

                // FichaPet: tratamento especial (abre lista de pets)
                if (item.m_dropPrefab.name == "FichaPet")
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Use /pet resgatar <id> para trocar a ficha por um pet (wolf_cub, lox_cub ou corvo).");
                    return false;
                }

                if (!FichaParaChave.TryGetValue(item.m_dropPrefab.name, out string chave)) return true;

                if (LocalHas(chave))
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Voce ja tem isso.");
                    return false;
                }

                Inventory inv = inventory ?? __instance.GetInventory();
                if (inv == null || !inv.ContainsItem(item)) return false;
                inv.RemoveItem(item, 1);

                ZPackage pkg = new ZPackage();
                pkg.Write("resgatar");
                pkg.Write(item.m_dropPrefab.name);
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), ToServer, pkg);
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Pedido enviado ao servidor...");
                return false;
            }
        }
    }

    /// <summary>API publica por reflexao do registro de cosmeticos (ver Cosmeticos.cs).</summary>
    public static class CosmeticosApi
    {
        public const int Version = 1;

        /// <summary>Servidor: concede a chave ao jogador. Devolve true se concedeu agora (false = ja tinha).</summary>
        public static bool Grant(long playerId, string key) => Cosmeticos.ServerGrant(playerId, key);

        /// <summary>Servidor: o jogador ja tem a chave.</summary>
        public static bool Has(long playerId, string key) => Cosmeticos.ServerHas(playerId, key);

        /// <summary>Cliente: o jogador local ja tem a chave (sincronizado do servidor nesta sessao).</summary>
        public static bool LocalHas(string key) => Cosmeticos.LocalHas(key);

        /// <summary>Cliente: todas as chaves que o jogador local tem.</summary>
        public static string[] LocalKeys() => Cosmeticos.LocalKeys();
    }

    // ----------------------------------------------------------------------------- titulos

    /// <summary>
    /// [Aesir] (VIP, dinamico, nao fica guardado) e [Fundador] (liberado com a FichaFundador):
    /// aparecem no chat e na placa de nome de quem tem. O jogador publica o proprio titulo na
    /// propria ZDO (mesmo mecanismo das bandeiras de PvP, Pvp/PvpState.cs) para os outros clientes
    /// lerem sem round-trip nenhum.
    /// </summary>
    [HarmonyPatch]
    internal static class Titulos
    {
        private const string ZdoTitulo = "dh_titulo";
        private static string _ultimoPublicado = "";

        internal static string LocalTag()
        {
            bool aesir = VipList.VipListApi.IsLocalPlayerVip();
            bool fundador = Cosmeticos.LocalHas(Cosmeticos.TituloFundador);
            if (!aesir && !fundador) return "";
            string tag = aesir ? "[Aesir]" : "";
            if (fundador) tag += (tag.Length > 0 ? " " : "") + "[Fundador]";
            return tag;
        }

        /// <summary>Chamado do Plugin.Update(): publica o titulo do jogador local, so quando muda.</summary>
        internal static void Tick(Player player)
        {
            if (player == null || player.m_nview == null || !player.m_nview.IsValid()) return;
            string tag = LocalTag();
            if (tag == _ultimoPublicado) return;
            _ultimoPublicado = tag;
            player.m_nview.GetZDO().Set(ZdoTitulo, tag);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.GetHoverName))]
        private static class HoverNamePatch
        {
            private static void Postfix(Player __instance, ref string __result)
            {
                if (__instance == null || __instance == Player.m_localPlayer || __instance.m_nview == null || !__instance.m_nview.IsValid()) return;
                string tag = __instance.m_nview.GetZDO().GetString(ZdoTitulo, "");
                if (!string.IsNullOrEmpty(tag)) __result = $"<color=#d8ab4a>{tag}</color> " + __result;
            }
        }

        [HarmonyPatch(typeof(Chat), nameof(Chat.SendText))]
        private static class ChatTagPatch
        {
            private static void Prefix(ref string text)
            {
                if (string.IsNullOrEmpty(text)) return;
                string tag = LocalTag();
                if (!string.IsNullOrEmpty(tag)) text = tag + " " + text;
            }
        }
    }
}
