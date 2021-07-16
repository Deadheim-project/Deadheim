using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System;

namespace BetterWards.PatchClasses
{
    [HarmonyPatch]
    public class Local
    {
        private static Localization lcl;
        public static Dictionary<string, string> t; //= new Dictionary<string, string>();
        private static Dictionary<string, string> english = new Dictionary<string, string>() {
                {"piece_betterward","Better Ward"},
            { "piece_betterward_description", "<color=white>A ward crafted by the gods. Complete protection from monsters with various other benefits</color>" },
            {"piece_betterward2","<color=purple>Better Ward</color>"},
            { "piece_betterward_description2", "<color=white>A ward crafted by the gods. Complete protection from monsters with various other benefits</color>" },
            {"piece_betterward3","<color=red>Better Ward</color>"},
            { "piece_betterward_description3", "<color=white>A ward crafted by the gods. Complete protection from monsters with various other benefits</color>" },
            {"piece_betterward4","<color=cyan>Better Ward</color>"},
            { "piece_betterward_description4", "<color=white>A ward crafted by the gods. Complete protection from monsters with various other benefits</color>" },
};

        public static void init(string lang, Localization l)
        {
            lcl = l;
            if (lang == "English")
            {
                t = english;
            }
        }
        public static void AddWord(object[] element)
        {
            MethodInfo meth = AccessTools.Method(typeof(Localization), "AddWord", null, null);
            meth.Invoke(lcl, element);
        }
        public static void UpdateDictinary()
        {
            string missing = "Missing Words:";
            foreach (var el in english)
            {
                if (t.ContainsKey(el.Key))
                {
                    AddWord(new object[] { el.Key, t[el.Key] });
                    continue;
                }
                AddWord(new object[] { el.Key, el.Value });
                missing += el.Key;
            }
            //give some logger output here
        }

        [HarmonyPatch(typeof(Localization), "SetupLanguage")]
        public static class MyLocalizationPatch
        {
            public static void Postfix(Localization __instance, string language)
            {
                //Debug.LogWarning(language);
                Local.init(language, __instance);
                Local.UpdateDictinary();
            }
        }

    }
}
