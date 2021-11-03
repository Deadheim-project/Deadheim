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
                var bossName = __instance.m_name;
                if (bossName == eikhthyr) __instance.m_bossItems = 20;
                if (bossName == elder) __instance.m_bossItems = 30;
                if (bossName == bonemass) __instance.m_bossItems = 50;

                if (Plugin.Age.Value == "stone")
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Não é possível invocar esse boss nessa era.", 0, null);

                    return false;
                }

                if (Plugin.Age.Value == "bronze")
                {
                    if (bossName == eikhthyr) return true;

                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Não é possível invocar esse boss nessa era.", 0, null);

                    return false;
                }

                if (Plugin.Age.Value == "iron")
                {
                    if (bossName == eikhthyr || bossName == elder) return true;
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Não é possível invocar esse boss nessa era.", 0, null);

                    return false;
                }

                if (Plugin.Age.Value == "silver")
                {
                    if (bossName == eikhthyr || bossName == elder || bossName == bonemass) return true;
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Não é possível invocar esse boss nessa era.", 0, null);

                    return false;
                }

                if (Plugin.Age.Value == "linen")
                {
                    if (bossName == eikhthyr || bossName == elder || bossName == bonemass || bossName == dragon) return true;

                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Não é possível invocar esse boss nessa era.", 0, null);

                    return false;
                }

                if (Plugin.Age.Value == "fire")
                {
                    if (bossName == eikhthyr || bossName == elder || bossName == bonemass || bossName == dragon || bossName == yagluth) return true;

                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Não é possível invocar esse boss nessa era.", 0, null);

                    return false;
                }  
                return true;
            }
        }
    }
}
