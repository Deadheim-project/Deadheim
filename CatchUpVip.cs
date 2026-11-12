using BepInEx.Configuration;

// Catch-up de armas para VIP (Aesir), no estilo do SmartSkills (Smoothbrain): arma nova, abaixo da
// maior skill de arma do jogador, ganha XP extra ate alcancar. So VIP; nao-VIP fica como antes.
// Aplicado no prefix de Skills.RaiseSkill (Patches.cs), depois do SkillCap e dos multiplicadores gerais.
namespace Deadheim
{
    internal static class CatchUpVip
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> Bonus;

        private static readonly Skills.SkillType[] Armas =
        {
            Skills.SkillType.Swords, Skills.SkillType.Knives, Skills.SkillType.Clubs, Skills.SkillType.Polearms,
            Skills.SkillType.Spears, Skills.SkillType.Axes, Skills.SkillType.Bows, Skills.SkillType.Crossbows,
            Skills.SkillType.Unarmed, Skills.SkillType.ElementalMagic, Skills.SkillType.BloodMagic,
        };

        public static void Bind(ConfigFile config)
        {
            const string secao = "CatchUpVip";
            Enabled = Plugin.Synced(config.Bind(secao, "Enabled", true,
                "Aesir: arma abaixo da maior skill de arma ganha XP extra ate alcancar."));
            Bonus = Plugin.Synced(config.Bind(secao, "Bonus", 50f,
                new ConfigDescription("% de XP extra (50 = x1,5) para o VIP em arma abaixo da maior skill de arma.",
                    new AcceptableValueRange<float>(0f, 200f))));
        }

        /// <summary>Multiplicador (>= 1) para o ganho de XP de <paramref name="tipo"/>.</summary>
        public static float Multiplicador(Skills skills, Skills.SkillType tipo)
        {
            if (Enabled == null || !Enabled.Value || Bonus.Value <= 0f) return 1f;
            if (System.Array.IndexOf(Armas, tipo) < 0) return 1f;
            // Skills e do jogador local (RaiseSkill so roda para quem ganha o XP); VIP checado no cliente.
            if (Player.m_localPlayer == null || Player.m_localPlayer.GetSkills() != skills) return 1f;
            if (!VipList.VipListApi.IsLocalPlayerVip()) return 1f;

            float nivel = skills.GetSkillLevel(tipo);
            float maior = 0f;
            foreach (Skills.SkillType a in Armas)
            {
                float n = skills.GetSkillLevel(a);
                if (n > maior) maior = n;
            }
            return nivel < maior ? 1f + Bonus.Value / 100f : 1f;
        }
    }
}
