using HarmonyLib;
using BetterWards;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterWards.PatchClasses
{
    [HarmonyPatch]
    class StructureWearNTear
    {
        [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
        public static class WearNTear_Reduction
        {
            // reduces damage to all things. Stuctures, ships, beds etc.
            private static bool Prefix(WearNTear __instance, ref HitData hit, ZNetView ___m_nview)
            {
                if (PrivateArea.CheckInPrivateArea(__instance.transform.position) && (UnityEngine.Object)___m_nview != (UnityEngine.Object)null && BetterWardsPlugin.wardEnabled.Value)
                {
                    hit.m_damage.m_blunt *= (float)(1.0 - (double)BetterWardsPlugin.wardDamageReduction.Value / 100.0);
                    hit.m_damage.m_slash *= (float)(1.0 - (double)BetterWardsPlugin.wardDamageReduction.Value / 100.0);
                    hit.m_damage.m_pierce *= (float)(1.0 - (double)BetterWardsPlugin.wardDamageReduction.Value / 100.0);
                    hit.m_damage.m_chop *= (float)(1.0 - (double)BetterWardsPlugin.wardDamageReduction.Value / 100.0);
                    hit.m_damage.m_pickaxe *= (float)(1.0 - (double)BetterWardsPlugin.wardDamageReduction.Value / 100.0);
                    hit.m_damage.m_fire *= (float)(1.0 - (double)BetterWardsPlugin.wardDamageReduction.Value / 100.0);
                    hit.m_damage.m_frost *= (float)(1.0 - (double)BetterWardsPlugin.wardDamageReduction.Value / 100.0);
                    hit.m_damage.m_lightning *= (float)(1.0 - (double)BetterWardsPlugin.wardDamageReduction.Value / 100.0);
                    hit.m_damage.m_poison *= (float)(1.0 - (double)BetterWardsPlugin.wardDamageReduction.Value / 100.0);
                    hit.m_damage.m_spirit *= (float)(1.0 - (double)BetterWardsPlugin.wardDamageReduction.Value / 100.0);
                }

                return true;
            }
        }
    }
}
