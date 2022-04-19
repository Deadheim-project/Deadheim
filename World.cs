using HarmonyLib;

namespace Deadheim.world
{
	internal class World
	{
        [HarmonyPatch(typeof(ZNet), "SaveWorldThread")]
        internal class SaveWorldThread
        {
            private static void Prefix(ref ZNet __instance)
            {
				if (Plugin.ResetWorldDay.Value == true)  __instance.m_netTime = 2040.0;
            }
        }
	}
}
