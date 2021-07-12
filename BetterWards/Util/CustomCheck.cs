using UnityEngine;
using System.Collections.Generic;
using HarmonyLib;

namespace BetterWards.Util
{
    [HarmonyPatch]
    public static class CustomCheck
    {
        public static bool CheckAccess(long playerID, Vector3 point, float radius = 0.0f, bool flash = true)
        {
            bool flag = false;
            List<PrivateArea> privateAreaList = new List<PrivateArea>();
            foreach (PrivateArea allArea in PrivateArea.m_allAreas)
            {
                if (allArea.IsEnabled() && allArea.IsInside(point, radius))
                {
                    Piece component = allArea.GetComponent<Piece>();
                    if ((UnityEngine.Object)component != (UnityEngine.Object)null && component.GetCreator() == playerID || allArea.IsPermitted(playerID))
                    {
                        flag = true;
                        break;
                    }
                    privateAreaList.Add(allArea);
                    break;
                }
            }
            if (flag || privateAreaList.Count <= 0)
                return true;
            if (flash)
            {
                foreach (PrivateArea privateArea in privateAreaList)
                    privateArea.FlashShield(false);
            }
            return false;
        }
    }
}
