using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Deadheim.BossControl
{
    [BepInPlugin("EnderBombz_Holanda.AntiRush", "BossControll", "1.0.0")]
    [BepInProcess("valheim.exe")]
    public class BossControl : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("EnderBombz_Holanda.BossControll");
        public static int realDay = 31;
        public static int currentDay;
        public static string[] bosses = new string[5]
        {
      "$piece_offerbowl_eikthyr",
      "$prop_eldersummoningbowl_name",
      "$piece_offerbowl_bonemass",
      "$prop_dragonsummoningbowl_name",
      "$piece_offerbowl_yagluth"
        };
        public static bool Real;
        public static int EikthyrInvokeDay;
        public static int EikthyrItemAmount;
        public static int ElderInvokeDay;
        public static int ElderItemAmount;
        public static int BoneMassInvokeDay;
        public static int BoneMassItemAmount;
        public static int ModerInvokeDay;
        public static int YagluthInvokeDay;
        public static List<ControlBossConfig> bossList;

        private void Awake()
        {
            Real = false;
            EikthyrInvokeDay = 30;
            EikthyrItemAmount = 20;
            ElderInvokeDay = 800;
            ElderItemAmount = 30;
            BoneMassInvokeDay = 2000;
            BoneMassItemAmount = 50;
            ModerInvokeDay = 5000;
            YagluthInvokeDay = 10000;
            bossList = new List<ControlBossConfig>()
          {
            new ControlBossConfig()
            {
              NameTranslate = "Eikthyr",
              PlaceName = "$piece_offerbowl_eikthyr",
              Days = EikthyrInvokeDay,
              ItemAmount = EikthyrItemAmount
            },
            new ControlBossConfig()
            {
              NameTranslate = "Ancião",
              PlaceName = "$prop_eldersummoningbowl_name",
              Days = ElderInvokeDay,
              ItemAmount = ElderItemAmount
            },
            new ControlBossConfig()
            {
              NameTranslate = "Massa Óssea",
              PlaceName = "$piece_offerbowl_bonemass",
              Days = BoneMassInvokeDay,
              ItemAmount = BoneMassItemAmount
            },
            new ControlBossConfig()
            {
              NameTranslate = "Moder",
              PlaceName = "$prop_dragonsummoningbowl_name",
              Days = ModerInvokeDay,
              ItemAmount = 0
            },
            new ControlBossConfig()
            {
              NameTranslate = "Yagluth",
              PlaceName = "$piece_offerbowl_yagluth",
              Days = YagluthInvokeDay,
              ItemAmount = 0
            }
          };
        }

        public static bool isBossEnabled(string bossPlace, OfferingBowl __instance, Humanoid user)
        {
            foreach (ControlBossConfig boss in bossList)
            {
                if (Real)
                {
                    int num = realDay * boss.Days;
                    if (currentDay < num && bossPlace == boss.PlaceName)
                    {
                        Debug.Log((object)"Yes he is entering in exeption");
                        ((Character)user).Message((MessageHud.MessageType)2, string.Format("O {0} só pode ser invocado em {1} / {2} dias!", (object)boss.NameTranslate, (object)currentDay, (object)num), 0, (Sprite)null);
                        return false;
                    }
                }
                else if (currentDay < boss.Days && bossPlace == boss.PlaceName)
                {
                    Debug.Log((object)"Yes he is entering in exeption");
                    ((Character)user).Message((MessageHud.MessageType)2, string.Format("O {0} só pode ser invocado em {1} / {2} dias!", (object)boss.NameTranslate, (object)currentDay, (object)boss.Days), 0, (Sprite)null);
                    return false;
                }
            }
            return true;
        }

        public class ControlBossConfig
        {
            public string NameTranslate { get; set; }

            public int Days { get; set; }

            public string PlaceName { get; set; }

            public int ItemAmount { get; set; }

            public ControlBossConfig()
            {
            }

            public ControlBossConfig(string name) => this.PlaceName = name;
        }

        [HarmonyPatch(typeof(OfferingBowl), "Interact")]
        public static class AntiRushInteraction_patch
        {
            private static bool Prefix(OfferingBowl __instance, Humanoid user)
            {
                foreach (ControlBossConfig boss in bossList)
                {
                    if ((string)__instance.m_name == boss.PlaceName && boss.ItemAmount > 0)
                        __instance.m_bossItems = boss.ItemAmount;
                }
                currentDay = EnvMan.instance.GetDay(ZNet.instance.GetTimeSeconds());
                Debug.Log((object)"Interact debugging...");
                Debug.Log((object)string.Format("Current day is: {0}", (object)currentDay));
                Debug.Log((object)("Current boss is: " + Localization.instance.Localize((string)__instance.m_name)));
                Debug.Log((object)("Current boss altar name: " + (string)__instance.m_name));
                return isBossEnabled((string)__instance.m_name, __instance, user);
            }
        }

        [HarmonyPatch(typeof(OfferingBowl), "UseItem")]
        public static class AntiRushUseItem_patch
        {
            private static bool Prefix(OfferingBowl __instance, Humanoid user, ItemDrop.ItemData item)
            {
                foreach (ControlBossConfig boss in bossList)
                {
                    if ((string)__instance.m_name == boss.PlaceName && boss.ItemAmount > 0)
                        __instance.m_bossItems = boss.ItemAmount;
                }
                currentDay = EnvMan.instance.GetDay(ZNet.instance.GetTimeSeconds());
                Debug.Log((object)"UseItem debugging...");
                Debug.Log((object)string.Format("{0}<{1} && {2}=={3}?", (object)currentDay, (object)realDay, (object)__instance.m_name, (object)bosses[0]));
                return isBossEnabled((string)__instance.m_name, __instance, user);
            }
        }
    }
}
