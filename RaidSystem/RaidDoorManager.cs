using System;
using HarmonyLib;
using UnityEngine;

namespace RaidSystem
{
    public static class RaidDoorManager
    {
        private static float _nextMaintenance;

        public static void Update()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            if (Time.time < _nextMaintenance) return;
            _nextMaintenance = Time.time + 10f;

            foreach (Door door in UnityEngine.Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
            {
                if (door == null || !Util.IsRaidEnabledHere(door.transform.position)) continue;
                if (!Util.IsRaidDisabledThisTime(door.transform.position)) continue;
                CloseAndRepair(door);
            }
        }

        public static void Breach(WearNTear wearNTear)
        {
            if (wearNTear == null) return;

            Repair(wearNTear);

            Door door = FindDoor(wearNTear);
            if (door != null) SetDoorState(door, true);
        }

        public static void CloseAndRepair(Door door)
        {
            if (door == null) return;

            WearNTear wearNTear = door.GetComponent<WearNTear>() ?? door.GetComponentInParent<WearNTear>();
            if (wearNTear != null) Repair(wearNTear);
            SetDoorState(door, false);
        }

        public static void CloseAndRepair(WearNTear wearNTear)
        {
            if (wearNTear == null) return;

            Repair(wearNTear);
            Door door = FindDoor(wearNTear);
            if (door != null) SetDoorState(door, false);
        }

        public static float GetHealth(WearNTear wearNTear)
        {
            if (wearNTear == null) return 0f;

            ZNetView nview = FindNetView(wearNTear);
            return nview?.GetZDO()?.GetFloat(ZDOVars.s_health, wearNTear.m_health) ?? wearNTear.m_health;
        }

        private static Door FindDoor(WearNTear wearNTear)
        {
            return wearNTear.GetComponent<Door>()
                   ?? wearNTear.GetComponentInParent<Door>()
                   ?? wearNTear.GetComponentInChildren<Door>();
        }

        private static ZNetView FindNetView(WearNTear wearNTear)
        {
            return wearNTear.GetComponent<ZNetView>()
                   ?? wearNTear.GetComponentInParent<ZNetView>()
                   ?? wearNTear.GetComponentInChildren<ZNetView>();
        }

        private static void Repair(WearNTear wearNTear)
        {
            try
            {
                ZNetView nview = FindNetView(wearNTear);
                if (nview?.GetZDO() == null) return;

                float health = wearNTear.m_health;
                nview.GetZDO().Set(ZDOVars.s_health, health);
                nview.GetZDO().Set("health", health);
                nview.InvokeRPC(ZNetView.Everybody, "RPC_HealthChanged", health);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RaidSystem] Door repair failed: " + ex.Message);
            }
        }

        private static void SetDoorState(Door door, bool open)
        {
            try
            {
                var nview = door.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid())
                {
                    nview.GetZDO().Set(ZDOVars.s_state, open ? 1 : 0);
                    door.UpdateState();
                    return;
                }

                var method = AccessTools.Method(typeof(Door), "SetState", new[] { typeof(int) });
                if (method != null)
                    method.Invoke(door, new object[] { open ? 1 : 0 });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RaidSystem] Door state change failed: " + ex.Message);
            }
        }
    }
}
