using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace RaidSystem
{
    [Serializable]
    public class TributeEntry
    {
        public string prefab;
        public int min;
        public int max;
    }

    public static class TributeManager
    {
        private static float _nextTick;
        private static bool _validated;
        private static Dictionary<int, List<TributeEntry>> _tables;

        private static string TablePath =>
            Path.Combine(RaidSystemPlugin.FileDirectory, "Tribute.json");

        // ---------- tabela ----------

        public static void LoadTables()
        {
            _tables = new Dictionary<int, List<TributeEntry>>();
            try
            {
                if (!Directory.Exists(RaidSystemPlugin.FileDirectory))
                    Directory.CreateDirectory(RaidSystemPlugin.FileDirectory);
                if (!File.Exists(TablePath)) WriteDefaultTable();

                var raw = JsonConvert.DeserializeObject<Dictionary<string, List<TributeEntry>>>(
                    File.ReadAllText(TablePath));
                if (raw == null) return;

                foreach (var kv in raw)
                    if (int.TryParse(kv.Key, out int tier) && kv.Value != null)
                        _tables[tier] = kv.Value;
            }
            catch (Exception ex)
            {
                Debug.LogError("[RaidSystem] Tribute.json invalido: " + ex.Message);
            }
        }

        /// <summary>Chamar depois que o ObjectDB existir. Nome errado no JSON tem que avisar.</summary>
        public static void ValidateTables()
        {
            if (_tables == null) LoadTables();
            // Se o ObjectDB ainda nao subiu, nao marca como validado: o tick reexecuta.
            if (ObjectDB.instance == null) return;
            _validated = true;

            foreach (var kv in _tables)
                foreach (TributeEntry e in kv.Value)
                {
                    if (string.IsNullOrEmpty(e.prefab)) continue;
                    if (ObjectDB.instance.GetItemPrefab(e.prefab) == null)
                        Debug.LogWarning($"[RaidSystem] Tribute.json: prefab '{e.prefab}' " +
                                         $"(tier {kv.Key}) nao existe no ObjectDB.");
                }
        }

        // ---------- acumulo ----------

        public static void Update()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 60f;

            if (!_validated) ValidateTables();

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long interval = Math.Max(1, RaidSystemPlugin.TributeIntervalMinutes.Value) * 60L;
            int cap = Math.Max(1, RaidSystemPlugin.TributeMaxCharges.Value);
            bool changed = false;

            DataStore.Modify(data =>
            {
                foreach (TerritoryInfo t in data.Territories)
                {
                    if (string.IsNullOrEmpty(t.OwnerTeamId)) continue;

                    if (t.LastTributeUtc == 0L) { t.LastTributeUtc = now; changed = true; continue; }

                    long due = (now - t.LastTributeUtc) / interval;
                    if (due <= 0) continue;

                    int before = t.PendingTribute;
                    t.PendingTribute = Mathf.Min(t.PendingTribute + (int)due, cap);
                    t.LastTributeUtc += due * interval;
                    if (t.PendingTribute != before) changed = true;
                }
            });

            if (changed) RPCManager.BroadcastFullSync();
        }

        // ---------- sorteio ----------

        /// <summary>Soma o sorteio de N cargas do tier. Chave = prefab, valor = quantidade.</summary>
        public static Dictionary<string, int> Roll(int tier, int charges)
        {
            var result = new Dictionary<string, int>();
            if (_tables == null) LoadTables();
            if (charges <= 0) return result;
            if (!_tables.TryGetValue(tier, out List<TributeEntry> entries) || entries == null)
            {
                Debug.LogWarning($"[RaidSystem] Sem tabela de tributo para o tier {tier}.");
                return result;
            }

            for (int c = 0; c < charges; c++)
                foreach (TributeEntry e in entries)
                {
                    if (string.IsNullOrEmpty(e.prefab)) continue;
                    int amount = UnityEngine.Random.Range(e.min, e.max + 1);
                    if (amount <= 0) continue;
                    result[e.prefab] = result.TryGetValue(e.prefab, out int cur)
                        ? cur + amount : amount;
                }

            return result;
        }

        // ---------- default ----------

        private static void WriteDefaultTable()
        {
            var def = new Dictionary<string, List<TributeEntry>>
            {
                ["1"] = new List<TributeEntry> {
                    new TributeEntry { prefab = "SurtlingCore", min = 1,  max = 2  },
                    new TributeEntry { prefab = "Coal",         min = 8,  max = 15 },
                    new TributeEntry { prefab = "Resin",        min = 10, max = 20 },
                },
                ["2"] = new List<TributeEntry> {
                    new TributeEntry { prefab = "IronScrap",    min = 5,  max = 10 },
                    new TributeEntry { prefab = "ElderBark",    min = 10, max = 20 },
                    new TributeEntry { prefab = "Guck",         min = 2,  max = 5  },
                },
                ["3"] = new List<TributeEntry> {
                    new TributeEntry { prefab = "SilverOre",    min = 4,  max = 8  },
                    new TributeEntry { prefab = "Obsidian",     min = 6,  max = 12 },
                    new TributeEntry { prefab = "FreezeGland",  min = 2,  max = 4  },
                },
                ["4"] = new List<TributeEntry> {
                    new TributeEntry { prefab = "BlackMetalScrap", min = 4, max = 8 },
                    new TributeEntry { prefab = "Tar",             min = 5, max = 10 },
                    new TributeEntry { prefab = "Needle",          min = 3, max = 6 },
                },
                ["5"] = new List<TributeEntry> {
                    new TributeEntry { prefab = "BlackCore",     min = 1, max = 2 },
                    new TributeEntry { prefab = "Sap",           min = 3, max = 6 },
                    new TributeEntry { prefab = "YggdrasilWood", min = 8, max = 15 },
                    new TributeEntry { prefab = "Carapace",      min = 4, max = 8 },
                },
                ["6"] = new List<TributeEntry> {
                    new TributeEntry { prefab = "FlametalOre",  min = 3, max = 6 },
                    new TributeEntry { prefab = "CharredBone",  min = 5, max = 10 },
                    new TributeEntry { prefab = "AskHide",      min = 2, max = 5 },
                    new TributeEntry { prefab = "MorgenSinew",  min = 1, max = 3 },
                },
            };

            File.WriteAllText(TablePath,
                JsonConvert.SerializeObject(def, Formatting.Indented),
                System.Text.Encoding.UTF8);
            Debug.Log("[RaidSystem] Tribute.json default escrito em " + TablePath);
        }
    }
}
