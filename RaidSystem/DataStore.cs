using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace RaidSystem
{
    public static class DataStore
    {
        private static readonly object _lock = new();
        private static RaidData _cache;
        private static string FilePath => Path.Combine(RaidSystemPlugin.FileDirectory, "RaidData.json");

        public static RaidData Load()
        {
            lock (_lock)
            {
                if (_cache != null) return _cache;
                if (!Directory.Exists(RaidSystemPlugin.FileDirectory))
                    Directory.CreateDirectory(RaidSystemPlugin.FileDirectory);
                if (!File.Exists(FilePath)) { _cache = new RaidData(); Save(); return _cache; }
                try
                {
                    string json = File.ReadAllText(FilePath);
                    _cache = string.IsNullOrEmpty(json) ? new RaidData()
                        : JsonConvert.DeserializeObject<RaidDataWrapper>(json)?.ToRaidData() ?? new RaidData();
                }
                catch (Exception ex) { Debug.LogError($"[RaidSystem] Load error: {ex.Message}"); _cache = new RaidData(); }
                return _cache;
            }
        }

        public static void Save()
        {
            lock (_lock)
            {
                try
                {
                    if (_cache == null) return;
                    if (!Directory.Exists(RaidSystemPlugin.FileDirectory))
                        Directory.CreateDirectory(RaidSystemPlugin.FileDirectory);
                    var wrapper = RaidDataWrapper.FromRaidData(_cache);
                    string json = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
                    // Grava num .tmp e copia por cima: o arquivo nunca fica faltando (o Delete + Move de
                    // antes deixava essa janela). Copy + Delete e nao File.Replace, igual ao PvpStore do
                    // Deadheim: o servidor da DatHost roda Mono em Linux (B11).
                    string tmp = FilePath + ".tmp";
                    File.WriteAllText(tmp, json);
                    File.Copy(tmp, FilePath, true);
                    File.Delete(tmp);
                }
                catch (Exception ex) { Debug.LogError($"[RaidSystem] Save error: {ex.Message}"); }
            }
        }

        public static void Modify(Action<RaidData> action)
        {
            lock (_lock) { if (_cache == null) Load(); action(_cache); Save(); }
        }

        public static string Serialize()
        {
            lock (_lock) { if (_cache == null) Load(); return JsonConvert.SerializeObject(RaidDataWrapper.FromRaidData(_cache)); }
        }

        public static void Deserialize(string json)
        {
            lock (_lock)
            {
                try { _cache = JsonConvert.DeserializeObject<RaidDataWrapper>(json)?.ToRaidData() ?? new RaidData(); }
                catch { _cache = new RaidData(); }
            }
        }
    }

    [Serializable]
    public class RaidDataWrapper
    {
        public PlayerInfo[] players;
        public PlayerScore[] scores;
        public TerritoryInfo[] territories;

        public RaidData ToRaidData()
        {
            var d = new RaidData();
            if (players != null) d.Players.AddRange(players);
            if (scores != null) d.Scores.AddRange(scores);
            if (territories != null) d.Territories.AddRange(territories);
            return d;
        }

        public static RaidDataWrapper FromRaidData(RaidData d)
        {
            return new RaidDataWrapper { players = d.Players.ToArray(), scores = d.Scores.ToArray(), territories = d.Territories.ToArray() };
        }
    }
}
