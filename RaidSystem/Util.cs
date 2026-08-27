using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RaidSystem
{
    public static class Util
    {
        private static List<RaidZone> _cachedZones;
        private static string _cachedZoneString;

        // As zonas ja tinham cache; os horarios globais nao. IsRaidDisabledThisTime esta no
        // caminho de RPC_Damage e refazia split, HashSet e OrderBy a cada golpe levado.
        private static List<int> _cachedGlobalHours;
        private static string _cachedGlobalHoursString;

        public static List<RaidZone> GetRaidZones()
        {
            string cfg = RaidSystemPlugin.RaidEnabledPositions.Value;
            if (_cachedZones != null && _cachedZoneString == cfg) return _cachedZones;
            _cachedZoneString = cfg;
            _cachedZones = ParseZones(cfg);
            return _cachedZones;
        }

        private static List<RaidZone> ParseZones(string cfg)
        {
            var zones = new List<RaidZone>();
            if (string.IsNullOrWhiteSpace(cfg)) return zones;
            foreach (string entry in cfg.Split('|'))
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                string[] p = entry.Split(',');
                if (p.Length < 3) continue;
                if (!float.TryParse(p[1].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float x)) continue;
                if (!float.TryParse(p[2].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float z)) continue;
                float wr = p.Length > 3 && float.TryParse(p[3].Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float wrv)
                    ? wrv : RaidSystemPlugin.AreaRadius.Value;
                float pr = p.Length > 4 && float.TryParse(p[4].Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float prv)
                    ? prv : wr;
                int tier = 1;
                if (p.Length > 6 && int.TryParse(p[6].Trim(), out int tierValue))
                    tier = Mathf.Max(1, tierValue);

                int minToolTier = 0;
                if (p.Length > 7 && int.TryParse(p[7].Trim(), out int toolValue))
                    minToolTier = Mathf.Max(0, toolValue);

                zones.Add(new RaidZone
                {
                    Name = p[0].Trim(),
                    X = x,
                    Z = z,
                    WardRadius = wr,
                    PvpRadius = pr,
                    AllowedHoursUtc = p.Length > 5 ? ParseHours(p[5], false) : new List<int>(),
                    Tier = tier,
                    MinToolTier = minToolTier
                });
            }
            return zones;
        }

        private static List<int> ParseHours(string cfg, bool allowComma)
        {
            var hours = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(cfg)) return hours.ToList();

            if (cfg.Trim().Equals("*", StringComparison.OrdinalIgnoreCase)
                || cfg.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
                return Enumerable.Range(0, 24).ToList();

            char[] separators = allowComma ? new[] { ',', ';' } : new[] { ';' };
            foreach (string rawPart in cfg.Split(separators))
            {
                string part = rawPart.Trim();
                if (string.IsNullOrWhiteSpace(part)) continue;

                string[] range = part.Split('-');
                if (range.Length == 2
                    && int.TryParse(range[0].Trim(), out int start)
                    && int.TryParse(range[1].Trim(), out int end))
                {
                    int normalizedStart = NormalizeHour(start);
                    int normalizedEnd = NormalizeHour(end);

                    if (normalizedStart == normalizedEnd && start != end)
                    {
                        foreach (int h in Enumerable.Range(0, 24))
                            hours.Add(h);
                        continue;
                    }

                    int hour = normalizedStart;
                    while (hour != normalizedEnd)
                    {
                        hours.Add(hour);
                        hour = NormalizeHour(hour + 1);
                    }
                    continue;
                }

                if (int.TryParse(part, out int single))
                    hours.Add(NormalizeHour(single));
            }

            return hours.OrderBy(h => h).ToList();
        }

        private static int NormalizeHour(int hour)
        {
            hour %= 24;
            return hour < 0 ? hour + 24 : hour;
        }

        public static string GetZoneNameAt(Vector3 pos)
        {
            if (string.IsNullOrWhiteSpace(RaidSystemPlugin.RaidEnabledPositions.Value)) return null;
            return GetRaidZones().FirstOrDefault(z => z.IsInWardArea(pos))?.Name;
        }

        public static RaidZone GetRaidZoneAt(Vector3 pos)
        {
            if (string.IsNullOrWhiteSpace(RaidSystemPlugin.RaidEnabledPositions.Value)) return null;
            return GetRaidZones().FirstOrDefault(z => z.IsInWardArea(pos));
        }

        public static int GetTierAt(Vector3 pos) => GetRaidZoneAt(pos)?.Tier ?? 0;

        /// <summary>Remove o sufixo "(Clone)" do nome de um GameObject instanciado.</summary>
        public static string CleanPrefabName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            int i = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return i >= 0 ? name.Substring(0, i) : name;
        }

        /// <summary>
        /// playerId do personagem de um peer conectado, ou 0.
        /// Existe para o servidor NAO confiar no playerId que o cliente manda no pacote:
        /// sem isso um cliente se passa por membro da guild dominante e resgata o tributo dela.
        /// </summary>
        public static long ResolvePlayerId(long peerId)
        {
            if (ZNet.instance == null || ZDOMan.instance == null) return 0L;
            ZNetPeer peer = ZNet.instance.GetPeer(peerId);
            if (peer == null || peer.m_characterID.IsNone()) return 0L;
            ZDO zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
            return zdo != null ? zdo.GetLong(ZDOVars.s_playerID, 0L) : 0L;
        }

        public static TerritoryInfo GetTerritoryAt(Vector3 pos)
        {
            RaidZone zone = GetRaidZoneAt(pos);
            RaidData data = DataStore.Load();
            if (zone != null)
            {
                TerritoryInfo byName = data.Territories.FirstOrDefault(t =>
                    string.Equals(t.Name, zone.Name, StringComparison.OrdinalIgnoreCase));
                if (byName != null) return byName;
            }

            return data.Territories.FirstOrDefault(t =>
                Utils.DistanceXZ(pos, new Vector3(t.X, t.Y, t.Z)) < RaidSystemPlugin.AreaRadius.Value);
        }

        public static string GetTerritoryOwner(Vector3 pos)
        {
            return GetTerritoryAt(pos)?.OwnerTeamId;
        }

        public static bool PlayerOwnsTerritory(Player player, Vector3 pos)
        {
            if (player == null) return false;
            string owner = GetTerritoryOwner(pos);
            if (string.IsNullOrEmpty(owner)) return true;

            string team = GuildsIntegration.GetPlayerTeam(player);
            return !string.IsNullOrEmpty(team) &&
                   string.Equals(team, owner, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRaidDoorOrGate(GameObject gameObject)
        {
            if (gameObject == null) return false;
            if (gameObject.GetComponent<Door>() != null
                || gameObject.GetComponentInParent<Door>() != null
                || gameObject.GetComponentInChildren<Door>() != null)
                return true;

            string name = gameObject.name.ToLowerInvariant();
            return name.Contains("door")
                   || name.Contains("gate")
                   || name.Contains("portao");
        }

        /// <summary>
        /// Ponto dentro do pvpRadius de alguma zona. So tem efeito com Force PvP In Zones ligado.
        /// </summary>
        public static bool IsInPvpZone(Vector3 position)
        {
            if (RaidSystemPlugin.ForcePvpInZones.Value != Toggle.On) return false;
            if (string.IsNullOrWhiteSpace(RaidSystemPlugin.RaidEnabledPositions.Value)) return false;
            return GetRaidZones().Any(z => z.IsInPvpArea(position));
        }

        public static bool IsRaidEnabledHere(Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(RaidSystemPlugin.RaidEnabledPositions.Value)) return false;
            return GetRaidZones().Any(z => z.IsInWardArea(position));
        }

        private static List<int> GetGlobalHours()
        {
            string cfg = RaidSystemPlugin.RaidTimeToAllowUtc.Value;
            if (_cachedGlobalHours != null && _cachedGlobalHoursString == cfg) return _cachedGlobalHours;
            _cachedGlobalHoursString = cfg;
            _cachedGlobalHours = ParseHours(cfg, true);
            return _cachedGlobalHours;
        }

        public static bool IsRaidDisabledThisTime()
        {
            return !GetGlobalHours().Contains(DateTime.UtcNow.Hour);
        }

        public static bool IsRaidDisabledThisTime(Vector3 position)
        {
            RaidZone zone = GetRaidZoneAt(position);
            if (zone?.AllowedHoursUtc != null && zone.AllowedHoursUtc.Count > 0)
                return !zone.AllowedHoursUtc.Contains(DateTime.UtcNow.Hour);

            return IsRaidDisabledThisTime();
        }

        public static void RespawnWard(Vector3 position, Quaternion rotation)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                Debug.LogWarning("[RaidSystem] RaidWard respawn requested outside server context.");
                return;
            }

            if (RaidSystemPlugin.Instance == null)
            {
                Debug.LogWarning("[RaidSystem] RaidWard respawn requested before plugin instance was ready.");
                return;
            }

            Debug.Log($"[RaidSystem] RaidWard respawn scheduled at X:{position.x:F0} Z:{position.z:F0} in {RaidSystemPlugin.SpawnDelayMS.Value}ms.");
            RaidSystemPlugin.Instance.StartCoroutine(RespawnWardRoutine(position, rotation));
        }

        private static IEnumerator RespawnWardRoutine(Vector3 position, Quaternion rotation)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, RaidSystemPlugin.SpawnDelayMS.Value / 1000f));

            GameObject prefab = ZNetScene.instance?.GetPrefab("RaidWard");
            if (prefab == null)
            {
                Debug.LogWarning("[RaidSystem] RaidWard prefab not found for respawn.");
                yield break;
            }

            GameObject ward = UnityEngine.Object.Instantiate(prefab, position, rotation);
            WearNTear wearNTear = ward.GetComponent<WearNTear>();
            ZNetView nview = ward.GetComponent<ZNetView>();
            if (wearNTear != null && nview?.GetZDO() != null)
                nview.GetZDO().Set(ZDOVars.s_health, wearNTear.m_health);

            Debug.Log($"[RaidSystem] RaidWard respawned at X:{position.x:F0} Z:{position.z:F0}.");
        }

    }
}
