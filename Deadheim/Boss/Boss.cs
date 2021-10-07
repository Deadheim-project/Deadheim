using HarmonyLib;

namespace Deadheim.Boss
{
    public class Boss
    {
        public static string eikhthyr = "$piece_offerbowl_eikthyr";
        public static string elder = "$prop_eldersummoningbowl_name";
        public static string bonemass = "$piece_offerbowl_bonemass";
        public static string dragon = "$prop_dragonsummoningbowl_name";
        public static string yagluth = "$piece_offerbowl_yagluth";

        [HarmonyPatch(typeof(OfferingBowl), "Interact")]
        public static class Interact
        {
            private static bool Prefix(OfferingBowl __instance, Humanoid user)
            {
                if (__instance.m_name == yagluth)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Não é possível invocar esse boss nessa era.", 0, null);

                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(OfferingBowl), "UseItem")]
        public static class UseItem
        {
            private static bool Prefix(OfferingBowl __instance, Humanoid user, ItemDrop.ItemData item)
            {
                if (__instance.m_name == yagluth)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Não é possível invocar esse boss nessa era.", 0, null);

                    return false;
                }

                if (__instance.m_name == eikhthyr) __instance.m_bossItems = 20;
                if (__instance.m_name == elder) __instance.m_bossItems = 30;
                if (__instance.m_name == bonemass) __instance.m_bossItems = 50;

                return true;
            }
        }
    }
}
