using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Drop dos itens Montaria_* (consumíveis do mod Montarias, negociáveis), de criaturas e de
    /// chefes. Mesmo esquema do PetDrops: postfix em CharacterDrop.GenerateDropList, chance lida
    /// da config a cada morte; só vale morte causada por jogador (ou criatura domada).
    /// </summary>
    public static class MountDrops
    {
        public static ConfigEntry<bool> DropEnabled;
        public static ConfigEntry<string> DropTable;
        public static ConfigEntry<float> DropMultiplier;

        // monstro (prefab) -> lista de (id da montaria, chance em %)
        private static Dictionary<string, List<KeyValuePair<string, float>>> _byMonster;

        internal static void BindConfigs(ConfigFile config)
        {
            DropEnabled = Plugin.Synced(config.Bind("Montarias", "MontariaDropEnabled", true,
                new ConfigDescription("Liga o drop dos itens Montaria_* ao matar criaturas e chefes.")));

            DropTable = Plugin.Synced(config.Bind("Montarias", "MontariaDropTable",
                "neck=Neck:1.5," +
                "deer=Deer:1.5," +
                "boar=Boar:1.5," +
                "eikthyr=Eikthyr:30," +
                "moder=Dragon:30",
                new ConfigDescription("Tabela de drops. Formato: idMontaria=Monstro:chance%;Monstro:chance%, separados por virgula. " +
                                      "Chance em porcentagem (0 a 100). Vale para chefes tambem. Montaria sem entrada = nao cai.")));
            DropTable.SettingChanged += (s, e) => Load();

            DropMultiplier = Plugin.Synced(config.Bind("Montarias", "MontariaDropGlobalMultiplier", 1f,
                new ConfigDescription("Multiplicador global das chances de drop de montarias (1 = normal, 0 = nenhum).")));
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
                string mount = kv[0].Trim();
                if (mount.Length == 0) continue;

                foreach (string src in kv[1].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] mc = src.Split(new[] { ':' }, 2);
                    if (mc.Length != 2) continue;
                    string monster = mc[0].Trim();
                    if (monster.Length == 0) continue;
                    if (!float.TryParse(mc[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float chance) || chance <= 0f) continue;

                    if (!_byMonster.TryGetValue(monster, out var list))
                        _byMonster[monster] = list = new List<KeyValuePair<string, float>>();
                    list.Add(new KeyValuePair<string, float>(mount, Mathf.Clamp(chance, 0f, 100f)));
                }
            }
        }

        private static bool KilledByPlayerSide(Character victim)
        {
            Character attacker = Priv.LastHit(victim)?.GetAttacker();
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
                    if (!victim || victim.IsPlayer()) return;

                    string monster = Utils.GetPrefabName(__instance.gameObject);
                    if (!_byMonster.TryGetValue(monster, out var mounts)) return;
                    if (!KilledByPlayerSide(victim)) return;

                    foreach (var m in mounts)
                    {
                        if (UnityEngine.Random.value * 100f >= m.Value * mult) continue;
                        GameObject prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab("Montaria_" + m.Key) : null;
                        if (!prefab) continue;
                        __result.Add(new KeyValuePair<GameObject, int>(prefab, 1));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Deadheim MountDrops] " + e.Message);
                }
            }
        }
    }
}
