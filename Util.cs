using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Deadheim
{
    public static class Util
    {
        public static int CREATORHASH = "creator".GetStableHashCode();

        public static string GetCreatorWardAndPortalCount(long creatorId)
        {
            int portalCount = GetCreatorPrefabCount("portal_wood".GetStableHashCode(), creatorId);
            int wardCount = GetCreatorPrefabCount("guard_stone".GetStableHashCode(), creatorId);

            return $"{portalCount},{wardCount}";
        }

        private static int GetCreatorPrefabCount(int prefabHash, long creatorId)
        {
            int prefab = 0;
            foreach (List<ZDO> zdoList in ZDOMan.instance.m_objectsBySector)
            {
                if (zdoList == null) continue;

                for (int index = 0; index < zdoList.Count; ++index)
                {
                    ZDO zdo2 = zdoList[index];
                    if (zdo2.GetPrefab() == prefabHash)
                    {
                        long prefabCreatorHash = zdo2.GetLong(CREATORHASH);
                        if (prefabCreatorHash == 0) continue;

                        if (prefabCreatorHash == creatorId) prefab++;
                    }
                }
            }
            return prefab;
        }

        private static byte[] ReadEmbeddedFileBytes(string name)
        {
            using MemoryStream stream = new();
            Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + "." + name)!.CopyTo(stream);
            return stream.ToArray();
        }

        private static Texture2D LoadTexture(string name)
        {
            Texture2D texture = new(0, 0);
            texture.LoadImage(ReadEmbeddedFileBytes("assets." + name));
            return texture;
        }

        public static Sprite LoadSprite(string name, int width, int height) => Sprite.Create(LoadTexture(name), new Rect(0, 0, width, height), Vector2.zero);
    }
}
