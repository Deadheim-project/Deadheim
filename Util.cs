using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deadheim
{
    public static class Util
    {
        private static int CREATORHASH = "creator".GetStableHashCode();

        public static string GetCreatorWardAndPortalCount(int creatorId)
        {
            int portalCount = GetCreatorPrefabCount("portal_wood".GetStableHashCode(), creatorId);
            int wardCount = GetCreatorPrefabCount("guard_stone".GetStableHashCode(), creatorId);

            return $"{portalCount},{wardCount}";
        }

        private static int GetPrefabCount(int prefabHash)
        {
            int prefabCount = 0;
            foreach (List<ZDO> zdoList in ZDOMan.instance.m_objectsBySector)
            {
                if (zdoList == null) continue;

                for (int index = 0; index < zdoList.Count; ++index)
                {
                    ZDO zdo2 = zdoList[index];
                    if (zdo2.GetPrefab() == prefabHash)
                    {
                        prefabCount++;
                    }
                }
            }
            return prefabCount;
        }

        private static int GetCreatorPrefabCount(int prefabHash, int creatorId)
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
    }
}
