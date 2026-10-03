using HarmonyLib;

namespace Deadheim.world
{
	internal class World
	{
        /// <summary>
        /// ResetWorldDay: volta o relogio do mundo para o dia 1 a cada save. No ZNet.SaveWorld, que
        /// roda na thread principal e prepara o save antes de abrir a thread dele; o patch antigo era
        /// no SaveWorldThread e mexia no m_netTime de fora da thread principal, no meio do jogo (B18).
        /// So no servidor: e ele quem manda a hora aos clientes.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), "SaveWorld")]
        internal class SaveWorld
        {
            private static void Prefix(ZNet __instance)
            {
				if (Plugin.ResetWorldDay.Value && __instance.IsServer()) __instance.m_netTime = 2040.0;
            }
        }
	}
}
