using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Deadheim
{
    public static class Util
    {
        public static string GetCreatorWardAndPortalCount(long creatorId)
        {
            // Uma varredura so, em WardCore: e ela que sabe quais prefabs contam.
            Wards.WardCore.PecasDoJogador p = Wards.WardCore.PecasDe(creatorId);
            return $"{p.Portais},{p.Wards},{p.Territorios},{p.WardsNaAreaSegura},{p.Totens.Count}";
        }

        private static byte[] ReadEmbeddedFileBytes(string name)
        {
            using MemoryStream stream = new();
            Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + "." + name)!.CopyTo(stream);
            return stream.ToArray();
        }

        public static Texture2D LoadTexture(string name)
        {
            Texture2D texture = new Texture2D(2, 2);
            byte[] imageBytes = ReadEmbeddedFileBytes("assets." + name);

            if (imageBytes == null || imageBytes.Length == 0) return texture;

            // This bypasses the .NET 4.8 compiler limit by looking up the method dynamically
            MethodInfo loadImageMethod = typeof(ImageConversion).GetMethod(
                "LoadImage",
                new System.Type[] { typeof(Texture2D), typeof(byte[]) }
            );

            if (loadImageMethod != null)
            {
                loadImageMethod.Invoke(null, new object[] { texture, imageBytes });
            }
            else
            {
                UnityEngine.Debug.LogError("[LoadTexture] Could not find LoadImage method via Reflection.");
            }

            return texture;
        }

        public static Sprite LoadSprite(string name, int width, int height) => Sprite.Create(LoadTexture(name), new Rect(0, 0, width, height), Vector2.zero);

        /// <summary>Sprite do tamanho da propria imagem: um icone de 128 px nao e cortado em 64.</summary>
        public static Sprite LoadSprite(string name) => SpriteOf(LoadTexture(name));

        public static Sprite SpriteOf(Texture2D texture) => Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
    }
}
