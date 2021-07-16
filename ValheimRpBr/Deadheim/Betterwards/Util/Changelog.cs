using HarmonyLib;
using System;
using UnityEngine;
using BetterWards;

namespace BetterWards.Util
{
    [HarmonyPatch]
    public static class Changelog
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ChangeLog), "Start")]
        private static void ChangeLog__Start(ref TextAsset ___m_changeLog)
        {
            if (___m_changeLog.text.Length > 0 && BetterWardsPlugin.wardEnabled.Value)
            {
                string str = string.Format("\n\n2021-05-10 {0} v{1}\n", (object)"Better Wards", (object)BetterWards.BetterWardsPlugin.version.ToString()) +
                "* Localization\n" +
                "* Offline raiding prevention BETA phase\n" +
                "* New ward cosmetics\n\n";
                ___m_changeLog = new TextAsset(str + ___m_changeLog.text);
            }
            else if (BetterWardsPlugin.wardEnabled.Value)
            {
                string str = string.Format("2021-05-10 {0} v{1}\n", (object)"Better Wards", (object)BetterWards.BetterWardsPlugin.version.ToString()) +
                "* Localization\n" +
                "* Offline raiding prevention BETA phase\n" +
                "* New ward cosmetics\n\n";
                ___m_changeLog = new TextAsset(str + ___m_changeLog.text);
            }
        }
    }
}
