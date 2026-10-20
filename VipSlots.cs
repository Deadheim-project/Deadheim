using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

// Limite de jogadores do servidor + Aesir 19 (vaga reservada para VIP).
//
// O jogo tem o limite 10 fixo (ZNet.ServerPlayerLimit, inlinado). Pontos trocados, todos so no servidor:
//  - ZNet.RPC_PeerInfo: o literal 10 do teste "GetNrOfPlayers() >= 10" vira PlayerLimit;
//  - SteamGameServer.SetMaxPlayerCount: o maximo anunciado ao Steam (lista de servidores);
//  - ZPlayFabMatchmaking.CreateLobby / CreateAndJoinNetwork: MaxPlayers/MaxPlayerCount do PlayFab.
// VIP (VipList, pelo host id do socket) entra com ate ReservedSlots a mais: Ocupacao(int) desconta essas
// vagas do resultado de GetNrOfPlayers so para o peer VIP. Nao-VIP fica exatamente em PlayerLimit.
// Substitui o mod MaxPlayerCount do Azumatt (que nao faz parte do pack).
namespace Deadheim
{
    internal static class VipSlotsConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<int> ReservedSlots;
        public static ConfigEntry<int> PlayerLimit;

        public static void Bind(ConfigFile config)
        {
            const string secao = "VipSlots";
            Enabled = Plugin.Synced(config.Bind(secao, "Enabled", true,
                "Aesir 19: VIP entra mesmo com o servidor cheio, usando as vagas reservadas."));
            PlayerLimit = config.Bind(secao, "PlayerLimit", 40,
                new ConfigDescription("Limite de jogadores do servidor (o jogo traz 10 fixo). Vale so no servidor.",
                    new AcceptableValueRange<int>(10, 100)));
            ReservedSlots = Plugin.Synced(config.Bind(secao, "ReservedSlots", 3,
                new ConfigDescription("Quantos jogadores VIP podem entrar alem de PlayerLimit.",
                    new AcceptableValueRange<int>(0, 10))));
        }
    }

    internal static class VipSlots
    {
        // Host id do peer que esta passando por RPC_PeerInfo agora (a rede do Valheim e de uma thread so).
        private static string _hostAtual;

        internal static bool Vaga(string host)
        {
            if (VipSlotsConfig.Enabled == null || !VipSlotsConfig.Enabled.Value) return false;
            if (VipSlotsConfig.ReservedSlots.Value <= 0) return false;
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return false;
            return !string.IsNullOrEmpty(host) && VipList.VipListApi.IsVip(host);
        }

        internal static int Limite() => VipSlotsConfig.PlayerLimit != null ? VipSlotsConfig.PlayerLimit.Value : 10;

        // Troca o "10" que vai para o setter indicado (MaxPlayers / MaxPlayerCount do PlayFab).
        internal static IEnumerable<CodeInstruction> TrocaMaximo(IEnumerable<CodeInstruction> instructions, string setter)
        {
            MethodInfo limite = AccessTools.Method(typeof(VipSlots), nameof(Limite));
            List<CodeInstruction> lista = new List<CodeInstruction>(instructions);
            bool feito = false;
            for (int i = 0; i + 1 < lista.Count; i++)
            {
                if (lista[i].opcode == OpCodes.Ldc_I4_S && System.Convert.ToInt32(lista[i].operand) == 10
                    && lista[i + 1].operand is MethodInfo m && m.Name == setter)
                {
                    lista[i] = new CodeInstruction(OpCodes.Call, limite) { labels = lista[i].labels, blocks = lista[i].blocks };
                    feito = true;
                }
            }
            if (!feito) UnityEngine.Debug.LogWarning("VipSlots: nao achei o maximo 10 antes de " + setter + ".");
            return lista;
        }

        /// <summary>Ajusta o resultado de ZNet.GetNrOfPlayers() no teste de lotacao do RPC_PeerInfo.</summary>
        public static int Ocupacao(int n)
        {
            return Vaga(_hostAtual) ? n - VipSlotsConfig.ReservedSlots.Value : n;
        }

        [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
        internal static class PeerInfoPatch
        {
            private static void Prefix(ZNet __instance, ZRpc rpc)
            {
                _hostAtual = null;
                if (!__instance.IsServer()) return;
                _hostAtual = __instance.GetPeer(rpc)?.m_socket?.GetHostName();
            }

            private static void Finalizer() => _hostAtual = null;

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo alvo = AccessTools.Method(typeof(ZNet), nameof(ZNet.GetNrOfPlayers));
                MethodInfo troca = AccessTools.Method(typeof(VipSlots), nameof(Ocupacao));
                MethodInfo limite = AccessTools.Method(typeof(VipSlots), nameof(Limite));
                int trocados = 0;
                bool aposChamada = false, limiteTrocado = false;
                foreach (CodeInstruction ins in instructions)
                {
                    if (ins.Calls(alvo))
                    {
                        trocados++;
                        aposChamada = true;
                        yield return ins;
                        yield return new CodeInstruction(OpCodes.Call, troca);
                    }
                    else if (aposChamada && !limiteTrocado && ins.opcode == OpCodes.Ldc_I4_S && System.Convert.ToInt32(ins.operand) == 10)
                    {
                        limiteTrocado = true;
                        yield return new CodeInstruction(OpCodes.Call, limite) { labels = ins.labels, blocks = ins.blocks };
                    }
                    else yield return ins;
                }
                if (trocados != 1 || !limiteTrocado)
                    UnityEngine.Debug.LogWarning("VipSlots: esperava 1 GetNrOfPlayers + limite 10 em ZNet.RPC_PeerInfo (achei " + trocados + ", limite trocado=" + limiteTrocado + "). Limite/vaga reservada podem nao funcionar.");
            }
        }

        // Steam: maximo anunciado na lista de servidores.
        [HarmonyPatch]
        internal static class SteamMaxPatch
        {
            private static MethodBase TargetMethod() =>
                AccessTools.Method(AccessTools.TypeByName("Steamworks.SteamGameServer"), "SetMaxPlayerCount");

            private static bool Prepare() => TargetMethod() != null;

            private static void Prefix(ref int cPlayersMax)
            {
                if (VipSlotsConfig.PlayerLimit != null) cPlayersMax = VipSlotsConfig.PlayerLimit.Value;
            }
        }

        [HarmonyPatch(typeof(ZPlayFabMatchmaking), "CreateLobby")]
        internal static class PlayFabLobbyPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> i) => TrocaMaximo(i, "set_MaxPlayers");
        }

        [HarmonyPatch(typeof(ZPlayFabMatchmaking), "CreateAndJoinNetwork")]
        internal static class PlayFabNetworkPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> i) => TrocaMaximo(i, "set_MaxPlayerCount");
        }
    }
}
