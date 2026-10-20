using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Drop dos itens Pet_* (cosmeticos, negociaveis). Postfix em CharacterDrop.GenerateDropList:
    /// a chance e lida da config a cada morte, sem mexer nos prefabs. So conta morte causada por
    /// jogador (ou por criatura domada). Roda no dono do monstro; a config e sincronizada.
    /// </summary>
    public static class PetDrops
    {
        public static ConfigEntry<bool> DropEnabled;
        public static ConfigEntry<string> DropTable;
        public static ConfigEntry<float> DropMultiplier;

        // monstro (prefab) -> lista de (pet id, chance em %)
        private static Dictionary<string, List<KeyValuePair<string, float>>> _byMonster;

        internal static void BindConfigs(ConfigFile config)
        {
            DropEnabled = Plugin.Synced(config.Bind("Pets", "PetDropEnabled", true,
                new ConfigDescription("Liga o drop de pets (itens Pet_*) ao matar monstros.")));

            DropTable = Plugin.Synced(config.Bind("Pets", "PetDropTable",
                "galinha=Boar:1;Neck:1," +
                "pintinho=Neck:1;Boar:1," +
                "leitao=Boar:1.5," +
                "wolf_cub=Wolf:1," +
                "lox_cub=Lox:1," +
                "corvo=Fenring:0.5",
                new ConfigDescription("Tabela de drops. Formato: petId=Monstro:chance%;Monstro:chance%, separados por virgula. " +
                                      "Chance em porcentagem (0 a 100). Pet sem entrada ou vazio = nao cai.")));
            DropTable.SettingChanged += (s, e) => Load();

            DropMultiplier = Plugin.Synced(config.Bind("Pets", "PetDropGlobalMultiplier", 1f,
                new ConfigDescription("Multiplicador global das chances de drop de pets (1 = normal, 0 = nenhum).")));
        }

        internal static void Init() => Load();

        private static void Load()
        {
            _byMonster = new Dictionary<string, List<KeyValuePair<string, float>>>(StringComparer.OrdinalIgnoreCase);
            string raw = DropTable?.Value;
            if (string.IsNullOrWhiteSpace(raw)) return;

            foreach (string entry in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = entry.Split(new[] { '=' }, 2);
                if (kv.Length != 2) continue;
                string pet = kv[0].Trim();
                if (pet.Length == 0) continue;

                foreach (string src in kv[1].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] mc = src.Split(new[] { ':' }, 2);
                    if (mc.Length != 2) continue;
                    string monster = mc[0].Trim();
                    if (monster.Length == 0) continue;
                    if (!float.TryParse(mc[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float chance) || chance <= 0f) continue;

                    if (!_byMonster.TryGetValue(monster, out var list))
                        _byMonster[monster] = list = new List<KeyValuePair<string, float>>();
                    list.Add(new KeyValuePair<string, float>(pet, Mathf.Clamp(chance, 0f, 100f)));
                }
            }
        }

        private static bool KilledByPlayerSide(Character victim)
        {
            Character attacker = victim.m_lastHit?.GetAttacker();
            if (!attacker) return false;
            return attacker.IsPlayer() || attacker.IsTamed();
        }

        [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
        private static class GenerateDropListPatch
        {
            private static void Postfix(CharacterDrop __instance, ref List<KeyValuePair<GameObject, int>> __result)
            {
                try
                {
                    if (DropEnabled == null || !DropEnabled.Value || _byMonster == null || __result == null) return;
                    float mult = DropMultiplier.Value;
                    if (mult <= 0f) return;

                    Character victim = __instance.GetComponent<Character>();
                    if (!victim || victim.IsPlayer() || victim.IsBoss()) return;

                    string monster = Utils.GetPrefabName(__instance.gameObject);
                    if (!_byMonster.TryGetValue(monster, out var pets)) return;
                    if (!KilledByPlayerSide(victim)) return;

                    foreach (var p in pets)
                    {
                        if (UnityEngine.Random.value * 100f >= p.Value * mult) continue;
                        GameObject prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab("Pet_" + p.Key) : null;
                        if (!prefab) continue;
                        __result.Add(new KeyValuePair<GameObject, int>(prefab, 1));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Deadheim PetDrops] " + e.Message);
                }
            }
        }
    }
}
