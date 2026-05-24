using System;
using UnityEngine;

namespace RaidSystem
{
    public static class CooldownManager
    {
        public static string GetTerritoryKey(Vector3 pos)
        {
            RaidZone zone = Util.GetRaidZoneAt(pos);
            if (zone != null) pos = zone.Position;

            int x = Mathf.RoundToInt(pos.x / 10f) * 10;
            int z = Mathf.RoundToInt(pos.z / 10f) * 10;
            return $"{x}_{z}";
        }

        public static bool IsOnCooldown(Vector3 pos)
        {
            string key = GetTerritoryKey(pos);
            var data = DataStore.Load();
            if (!data.Cooldowns.TryGetValue(key, out long end)) return false;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= end) { DataStore.Modify(d => d.Cooldowns.Remove(key)); return false; }
            return true;
        }

        public static int GetRemainingMinutes(Vector3 pos)
        {
            string key = GetTerritoryKey(pos);
            var data = DataStore.Load();
            if (!data.Cooldowns.TryGetValue(key, out long end)) return 0;
            long rem = end - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return rem > 0 ? (int)(rem / 60) + 1 : 0;
        }

        public static void StartCooldown(Vector3 pos)
        {
            int min = RaidSystemPlugin.RaidCooldownMinutes.Value;
            if (min <= 0) return;
            string key = GetTerritoryKey(pos);
            long end = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (min * 60);
            DataStore.Modify(d => d.Cooldowns[key] = end);
            Debug.Log($"[RaidSystem] Cooldown: {key} for {min}min");
        }

        public static void ClearCooldown(Vector3 pos) { DataStore.Modify(d => d.Cooldowns.Remove(GetTerritoryKey(pos))); }
        public static void ClearAllCooldowns() { DataStore.Modify(d => d.Cooldowns.Clear()); }
    }
}
