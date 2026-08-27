using HarmonyLib;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RaidSystem
{
    /// <summary>
    /// Desenha os territorios na textura do minimapa.
    ///
    /// Minimap.UpdateMap e chamado do Update() do jogo, ou seja, todo frame. A versao
    /// anterior pintava e chamava m_mapTexture.Apply() em todos eles, subindo a textura
    /// inteira para a GPU 60 vezes por segundo, e pintava por cima do mapa explorado sem
    /// nunca restaurar, destruindo os pixels originais.
    ///
    /// Agora so redesenha quando a lista de territorios muda de verdade, e guarda os
    /// pixels que cobriu para devolve-los antes de repintar.
    /// </summary>
    [HarmonyPatch]
    public static class MapDrawer
    {
        private struct SavedPixel
        {
            public int X;
            public int Y;
            public Color Color;
        }

        private static readonly List<SavedPixel> _painted = new List<SavedPixel>();

        // O circulo passa duas vezes no mesmo pixel quando x == 0. Sem isto o segundo
        // backup guardaria a cor ja pintada, e restaurar devolveria a cor errada.
        private static readonly HashSet<int> _paintedKeys = new HashSet<int>();

        private static string _signature;
        private static float _nextCheck;

        [HarmonyPatch(typeof(Minimap), "UpdateMap")]
        [HarmonyPostfix]
        private static void OnUpdateMap(Minimap __instance)
        {
            Texture2D texture = __instance.m_mapTexture;
            if (texture == null) return;

            // Rodamos dentro do Update do jogo. Montar a assinatura aloca, entao nem isso
            // vale a pena todo frame: territorio nao muda em milissegundos.
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.5f;

            string signature = BuildSignature();
            if (signature == _signature) return;
            _signature = signature;

            RestorePainted(texture);

            int radius = RaidSystemPlugin.RadiusDrawMap.Value;
            if (radius > 0)
            {
                foreach (TerritoryInfo t in DataStore.Load().Territories)
                {
                    if (string.IsNullOrEmpty(t.OwnerTeamId)) continue;

                    Color color = GuildsIntegration.GetGuildColor(t.OwnerTeamId);
                    color.a = RaidSystemPlugin.ColorAlpha.Value;

                    __instance.WorldToMapPoint(new Vector3(t.X, t.Y, t.Z), out float mx, out float my);
                    Circle(texture, (int)(mx * texture.width), (int)(my * texture.height), radius, color);
                }
            }

            texture.Apply();
        }

        /// <summary>Muda quando algum territorio, dono ou posicao muda. So ai vale repintar.</summary>
        private static string BuildSignature()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(RaidSystemPlugin.RadiusDrawMap.Value).Append('|')
              .Append(RaidSystemPlugin.ColorAlpha.Value).Append('|');

            foreach (TerritoryInfo t in DataStore.Load().Territories)
                sb.Append(t.Name).Append(':').Append(t.OwnerTeamId).Append(':')
                  .Append((int)t.X).Append(':').Append((int)t.Z).Append('|');

            return sb.ToString();
        }

        private static void RestorePainted(Texture2D texture)
        {
            for (int i = 0; i < _painted.Count; i++)
                texture.SetPixel(_painted[i].X, _painted[i].Y, _painted[i].Color);
            _painted.Clear();
            _paintedKeys.Clear();
        }

        private static void Paint(Texture2D texture, int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height) return;

            if (_paintedKeys.Add(y * texture.width + x))
                _painted.Add(new SavedPixel { X = x, Y = y, Color = texture.GetPixel(x, y) });

            texture.SetPixel(x, y, color);
        }

        public static void Circle(Texture2D texture, int cx, int cy, int r, Color color)
        {
            for (int x = 0; x <= r; x++)
            {
                int d = (int)Mathf.Ceil(Mathf.Sqrt(r * r - x * x));
                for (int y = 0; y <= d; y++)
                {
                    Paint(texture, cx + x, cy + y, color);
                    Paint(texture, cx - x, cy + y, color);
                    Paint(texture, cx + x, cy - y, color);
                    Paint(texture, cx - x, cy - y, color);
                }
            }
        }
    }
}
