using HarmonyLib;
using UnityEngine;

namespace Deadheim
{
    [HarmonyPatch]
    public class Drop
    {
        [HarmonyPatch(typeof(Character), "Awake")]
        public static class Awake
        {
            public static void Prefix(ref Character __instance)
            {
                var drops = __instance.GetComponent<CharacterDrop>();

                if (!drops) return;

                if (__instance.m_faction == Character.Faction.Boss) return;

                if (__instance.gameObject.name.ToLower() == "chickenboo" || __instance.gameObject.name.ToLower() == "skeleton") return;

                if (Plugin.Age.Value == "stone" || Plugin.Age.Value == "bronze")
                {
                    if (__instance.m_faction == Character.Faction.ForestMonsters) return;
                }   

                if (Plugin.Age.Value == "iron")
                {
                    if(__instance.m_faction == Character.Faction.Undead) return;
                    if (__instance.m_faction == Character.Faction.ForestMonsters) return;
                    if (__instance.m_faction == Character.Faction.Demon) return;
                }

                if (Plugin.Age.Value == "silver")
                {
                    if(__instance.m_faction == Character.Faction.Undead) return;
                    if (__instance.m_faction == Character.Faction.ForestMonsters) return;
                    if (__instance.m_faction == Character.Faction.MountainMonsters) return;
                    if (__instance.m_faction == Character.Faction.SeaMonsters) return;
                }   

                if (Plugin.Age.Value == "linen")
                {
                    if (__instance.m_faction == Character.Faction.Undead) return;
                    if (__instance.m_faction == Character.Faction.ForestMonsters) return;
                    if (__instance.m_faction == Character.Faction.MountainMonsters) return;
                    if (__instance.m_faction == Character.Faction.PlainsMonsters) return;
                }

                drops.m_dropsEnabled = false;
                drops.m_drops.RemoveAll(x => x.m_chance > 0);
            }
        }
    }
}
