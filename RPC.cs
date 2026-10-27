using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    [HarmonyPatch]
    class RPC
    {
        // Uma contagem por segundo por conexao: cada uma varre o mundo inteiro, e um cliente
        // modificado mandando o pedido em laco travaria o servidor. Dentro do intervalo vai a
        // ultima resposta (o OnSpawned e o martelo na mao podem pedir quase juntos).
        private const float IntervaloContagem = 1f;
        private static readonly Dictionary<long, (float em, long criador, string resultado)> _ultimaContagem =
            new Dictionary<long, (float em, long criador, string resultado)>();

        public static void RPC_PortalAndTotemCountServer(long sender, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer()) return;

            long real = RemetenteRpc.Real(sender, out _);
            if (real == 0L) return;
            long creatorId = pkg.ReadLong();

            float agora = Time.realtimeSinceStartup;
            string result;
            if (_ultimaContagem.TryGetValue(real, out var ultima) && agora - ultima.em < IntervaloContagem)
            {
                if (ultima.criador != creatorId) return;
                result = ultima.resultado;
            }
            else
            {
                // Conexoes que ja sairam ficam para tras: limpa tudo de vez em quando.
                if (_ultimaContagem.Count > 512) _ultimaContagem.Clear();
                result = Util.GetCreatorWardAndPortalCount(creatorId);
                _ultimaContagem[real] = (agora, creatorId, result);
            }

            ZPackage toSend = new();
            toSend.Write(result);

            ZRoutedRpc.instance.InvokeRoutedRPC(real, "DeadheimPortalAndTotemCountClient", toSend);
        }

        public static void RPC_PortalAndTotemCountClient(long sender, ZPackage pkg)
        {
            // So a conta do servidor: outro jogador podia mandar 999 e travar as construcoes de alguem.
            if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID()) return;
            string[] counts = pkg.ReadString().Split(',');

            // Logo depois de colocar, o servidor ainda pode nao ter a ZDO da peca nova e responder a
            // conta de antes (o pedido sai ate antes da peca existir). Por 10 s vale a maior: senao a
            // conta local, somada ao colocar, voltava e o limite deixava passar mais uma.
            bool recente = Time.time - ConstrucaoBarrada.UltimaColocacao < 10f;
            int Ler(int i, int local)
            {
                int servidor = counts.Length > i ? Convert.ToInt32(counts[i]) : 999;
                return recente && local < 999 && servidor < 999 ? Math.Max(local, servidor) : servidor;
            }

            Plugin.PlayerPortalCount = Ler(0, Plugin.PlayerPortalCount);
            Plugin.PlayerWardCount = Ler(1, Plugin.PlayerWardCount);
            Plugin.PlayerTerritoryWardCount = Ler(2, Plugin.PlayerTerritoryWardCount);
            Plugin.PlayerSafeAreaWardCount = Ler(3, Plugin.PlayerSafeAreaWardCount);
            TotemCidade.Contagem = Ler(4, TotemCidade.Contagem);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        public static class OnSpawned
        {
            public static void Postfix()
            {
                ZPackage pkg = new();
                pkg.Write(Player.m_localPlayer.GetPlayerID());
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "DeadheimPortalAndTotemCountServer", pkg);
            }
        }

        [HarmonyPatch(typeof(Game), "Start")]
        public static class GameStart
        {
            public static void Postfix()
            {
                if (ZRoutedRpc.instance == null)
                    return;

                ZRoutedRpc.instance.Register<ZPackage>("DeadheimPortalAndTotemCountServer", new Action<long, ZPackage>(RPC_PortalAndTotemCountServer));
                ZRoutedRpc.instance.Register<ZPackage>("DeadheimPortalAndTotemCountClient", new Action<long, ZPackage>(RPC_PortalAndTotemCountClient));
            }
        }
    }
}
