using BepInEx.Configuration;
using HarmonyLib;

namespace Deadheim
{
    /// <summary>
    /// TEMPORARIO, so enquanto o servidor esta em teste: todo jogador nasce com 100 em
    /// todas as skills. Remover (este arquivo e o Bind no Plugin) antes do lancamento.
    /// </summary>
    public static class SkillsTeste
    {
        public static ConfigEntry<bool> Ligado;

        public static void Bind(ConfigFile config)
        {
            Ligado = Plugin.Synced(config.Bind("Server config", "TesteSkills100", true,
                new ConfigDescription("TEMPORARIO (servidor em teste): todos os jogadores ficam com 100 em todas as skills ao nascer. Desligar/remover no lancamento.")));
            Ligado.SettingChanged += (_, __) => Aplicar(Player.m_localPlayer);
        }

        private static void Aplicar(Player player)
        {
            if (Ligado == null || !Ligado.Value || player == null || player != Player.m_localPlayer) return;
            Skills skills = player.GetSkills();
            if (skills == null) return;

            foreach (Skills.SkillDef def in skills.m_skills)
            {
                Skills.Skill skill = skills.GetSkill(def.m_skill);
                skill.m_level = 100f;
                skill.m_accumulator = 0f;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class NoSpawn
        {
            private static void Postfix(Player __instance) => Aplicar(__instance);
        }
    }
}
