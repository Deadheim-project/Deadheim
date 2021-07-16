using HarmonyLib;
using Steamworks;

namespace Deadheim.Server
{
    [HarmonyPatch]
    public class ClientPatches
    {
        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        private static class ZNet__OnNewConnection
        {
            public static void Postfix(ZNet __instance, ZNetPeer peer)
            {
                if (!__instance.IsServer())
                {
                    Plugin.steamId = SteamUser.GetSteamID().ToString();
                }
            }
        }
    }
}
