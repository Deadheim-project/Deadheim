using System;
using System.Collections.Generic;
using UnityEngine;

namespace RaidSystem
{
    [Serializable]
    public class PlayerInfo
    {
        public string Nick { get; set; }
        public string SteamId { get; set; }
        public string PlayerId { get; set; }
        public string Description { get; set; }
        public string TeamId { get; set; }
    }

    [Serializable]
    public class PlayerScore
    {
        public string PlayerId { get; set; }
        public string Nick { get; set; }
        public string TeamId { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Conquests { get; set; }
        public int Defenses { get; set; }

        public int TotalPoints =>
            (Kills * RaidSystemPlugin.PointsPerKill.Value) +
            (Conquests * RaidSystemPlugin.PointsPerConquest.Value) +
            (Defenses * RaidSystemPlugin.PointsPerDefense.Value) -
            (Deaths * RaidSystemPlugin.PointsLostPerDeath.Value);
    }

    [Serializable]
    public class TerritoryInfo
    {
        public string Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string OwnerTeamId { get; set; }
        public long LastConquestTimestamp { get; set; }
    }

    [Serializable]
    public class RaidData
    {
        public List<PlayerInfo> Players { get; set; } = new();
        public List<PlayerScore> Scores { get; set; } = new();
        public List<TerritoryInfo> Territories { get; set; } = new();
        public Dictionary<string, long> Cooldowns { get; set; } = new();
    }

    public class RaidZone
    {
        public string Name { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public float WardRadius { get; set; }
        public float PvpRadius { get; set; }
        public List<int> AllowedHoursUtc { get; set; } = new();

        public Vector3 Position => new Vector3(X, 0f, Z);
        public bool IsInWardArea(Vector3 p) => Utils.DistanceXZ(p, Position) < WardRadius;
        public bool IsInPvpArea(Vector3 p) => Utils.DistanceXZ(p, Position) < PvpRadius;
    }

    public enum Toggle { On = 1, Off = 0 }
}
