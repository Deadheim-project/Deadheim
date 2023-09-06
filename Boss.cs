using HarmonyLib;

namespace Deadheim
{
    [HarmonyPatch]
    public class Boss
    {
        [HarmonyPatch(typeof(OfferingBowl), "Interact")]
        public static class Interact
        {
            private static bool Prefix(OfferingBowl __instance, Humanoid user)
            {
                if (Validate(__instance.m_bossPrefab.name)) return true;

                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You don't know enough", 0, null);
                return false;
            }
        }

        [HarmonyPatch(typeof(OfferingBowl), "UseItem")]
        public static class UseItem
        {
            private static bool Prefix(OfferingBowl __instance, Humanoid user, ItemDrop.ItemData item)
            {
                if (Validate(__instance.m_bossPrefab.name)) return true;

                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You don't know enough", 0, null);
                return false;
            }
        }

        public static bool Validate(string bossName)
        {
            if (!Plugin.BlockedBosses.Value.Contains(bossName)) return true;

            return false;
        }
    }
}