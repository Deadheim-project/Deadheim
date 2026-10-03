using HarmonyLib;
using System;

namespace Deadheim
{
    /// <summary>
    /// Quem mandou, de verdade, o RPC roteado que o servidor esta tratando agora.
    ///
    /// O "sender" que o vanilla entrega ao handler (ZRoutedRpc.HandleRoutedRPC passa
    /// data.m_senderPeerID) vem escrito pelo proprio cliente dentro do pacote, e o servidor
    /// nao confere: um cliente modificado se passa por qualquer peer, inclusive um admin
    /// online. Aqui vale a conexao cujo pacote esta sendo lido (ZRpc.HandlePackage), o mesmo
    /// truque do ServerSync. Publico porque o RaidSystem usa o mesmo remetente.
    /// </summary>
    public static class RemetenteRpc
    {
        private static ZRpc _conexaoAtual;

        /// <summary>A conexao do pacote em tratamento. Null = chamada local (o proprio servidor ou o host).</summary>
        public static ZRpc ConexaoAtual => _conexaoAtual;

        /// <summary>Pacotes que chegaram ao servidor com o remetente de outro peer (cliente modificado).</summary>
        public static int Forjados { get; private set; }

        /// <summary>Onde vai o aviso de remetente forjado. Trocavel so para o teste sem o jogo.</summary>
        internal static Action<string> Aviso = texto => UnityEngine.Debug.LogWarning(texto);

        private static readonly System.Collections.Generic.Dictionary<long, double> _avisadoEm = new System.Collections.Generic.Dictionary<long, double>();
        // Relogio monotonico sem a Unity: o arquivo tambem roda no teste sem o jogo.
        private static readonly System.Diagnostics.Stopwatch _relogio = System.Diagnostics.Stopwatch.StartNew();

        /// <summary>
        /// Peer de verdade de quem mandou o RPC em tratamento: o uid do peer dono da conexao,
        /// ou o id do proprio servidor numa chamada local (host de um mundo nao dedicado).
        /// 0 = conexao que nao e de nenhum peer: descartar. So faz sentido no servidor, dentro
        /// do handler do RPC.
        /// </summary>
        public static long PeerId()
        {
            ZRpc conexao = _conexaoAtual;
            if (conexao == null) return ZRoutedRpc.instance != null ? ZRoutedRpc.instance.m_id : 0L;
            return PeerDa(conexao);
        }

        private static long PeerDa(ZRpc conexao)
        {
            if (conexao == null || ZNet.instance == null) return 0L;
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
                if (peer != null && peer.m_rpc == conexao) return peer.m_uid;
            return 0L;
        }

        /// <summary>
        /// No servidor, antes de o vanilla ler o pacote: o remetente (8 bytes depois do m_msgID,
        /// RoutedRPCData.Serialize) vira o uid do dono da conexao. O vanilla usa esse valor no
        /// handler do servidor e o repassa intacto aos outros clientes (RouteRPC), entao com isto o
        /// "sender" de todo RPC roteado passa a ser o de verdade, no servidor e nos clientes:
        /// um cliente nao se passa mais pelo servidor (punicao, sincronia do RaidSystem) nem por
        /// outro jogador. Cliente honesto sempre escreve o proprio id e nao muda nada.
        /// </summary>
        [HarmonyPatch(typeof(ZRoutedRpc), "RPC_RoutedRPC")]
        private static class RemetenteVerdadeiroPatch
        {
            private static void Prefix(ZRpc rpc, ZPackage pkg)
            {
                if (ZNet.instance == null || !ZNet.instance.IsServer() || rpc == null || pkg == null) return;
                long real = PeerDa(rpc);
                if (real == 0L) return;
                int start = pkg.GetPos();
                try
                {
                    pkg.SetPos(start + 8);
                    long declarado = pkg.ReadLong();
                    if (declarado == real) return;
                    pkg.SetPos(start + 8);
                    pkg.Write(real);
                    Forjados++;
                    AvisarForjado(real, declarado);
                }
                finally
                {
                    pkg.SetPos(start);
                }
            }
        }

        /// <summary>Um aviso por peer por minuto: um cliente modificado pode mandar muitos.</summary>
        private static void AvisarForjado(long real, long declarado)
        {
            double agora = _relogio.Elapsed.TotalSeconds;
            if (_avisadoEm.TryGetValue(real, out double ultimo) && agora - ultimo < 60d) return;
            _avisadoEm[real] = agora;
            string host = null;
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
                if (peer != null && peer.m_uid == real) host = peer.m_socket?.GetHostName();
            Aviso?.Invoke($"[Deadheim] RPC com remetente forjado: o pacote dizia {declarado}, a conexao e de {real} ({host ?? "?"}). " +
                          "Corrigido para o remetente de verdade.");
        }

        /// <summary>
        /// O remetente que vale para um RPC que chegou com <paramref name="declarado"/> no pacote.
        /// <paramref name="forjado"/> diz se o pacote mentiu (cliente modificado), para o log.
        /// </summary>
        public static long Real(long declarado, out bool forjado)
        {
            long real = PeerId();
            forjado = real != declarado;
            return real;
        }

        [HarmonyPatch(typeof(ZRpc), "HandlePackage")]
        private static class ConexaoAtualPatch
        {
            private static void Prefix(ZRpc __instance, out ZRpc __state)
            {
                __state = _conexaoAtual;
                _conexaoAtual = __instance;
            }

            private static Exception Finalizer(Exception __exception, ZRpc __state)
            {
                _conexaoAtual = __state;
                return __exception;
            }
        }
    }
}
