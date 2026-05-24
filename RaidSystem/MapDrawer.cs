using HarmonyLib;
using UnityEngine;

namespace RaidSystem
{
    [HarmonyPatch]
    public static class MapDrawer
    {
        // Called from GUI — actual drawing happens on next Minimap.UpdateMap
        public static void RefreshMap() { }

        [HarmonyPatch(typeof(Minimap), "UpdateMap")]
        [HarmonyPostfix]
        private static void OnUpdateMap(Minimap __instance)
        {
            int radius = RaidSystemPlugin.RadiusDrawMap.Value;
            if (radius <= 0 || __instance.m_mapTexture == null) return;

            var data = DataStore.Load();
            foreach (var t in data.Territories)
            {
                if (string.IsNullOrEmpty(t.OwnerTeamId)) continue;
                Color color = GuildsIntegration.GetGuildColor(t.OwnerTeamId);
                color.a = RaidSystemPlugin.ColorAlpha.Value;

                __instance.WorldToMapPoint(new Vector3(t.X, t.Y, t.Z), out float mx, out float my);
                Circle(__instance.m_mapTexture,
                    (int)(mx * __instance.m_mapTexture.width),
                    (int)(my * __instance.m_mapTexture.height),
                    radius, color);
            }
            __instance.m_mapTexture.Apply();
        }

        public static void Circle(Texture2D tex, int cx, int cy, int r, Color col)
        {
            for (int x = 0; x <= r; x++)
            {
                int d = (int)Mathf.Ceil(Mathf.Sqrt(r * r - x * x));
                for (int y = 0; y <= d; y++)
                {
                    tex.SetPixel(cx + x, cy + y, col);
                    tex.SetPixel(cx - x, cy + y, col);
                    tex.SetPixel(cx + x, cy - y, col);
                    tex.SetPixel(cx - x, cy - y, col);
                }
            }
        }
    }
}
