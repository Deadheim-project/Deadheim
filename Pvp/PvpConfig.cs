using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Configuracao do modulo de PvP. Tudo sincronizado pelo servidor (ServerSync, via
    /// Plugin.Synced): o cfg que vale e o do servidor, igual as outras configs do Deadheim.
    /// </summary>
    internal static class PvpConfig
    {
        // ------------------------------------------------------------------ geral
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> ForcePvp;
        public static ConfigEntry<float> DamageMultiplier;
        public static ConfigEntry<float> WardDefenseMultiplier;
        public static ConfigEntry<bool> NoFriendlyFireClan;
        public static ConfigEntry<bool> NoFriendlyFireTerritory;
        public static ConfigEntry<float> ImmunityMinutes;
        public static ConfigEntry<float> CombatTagSeconds;
        public static ConfigEntry<float> KillCreditSeconds;
        public static ConfigEntry<bool> KillFeed;

        // --------------------------------------------------------------------- PK
        public static ConfigEntry<float> PkMinutes;
        public static ConfigEntry<float> PkSkillLossMultiplier;
        public static ConfigEntry<bool> PkClearsOnDeath;

        // ------------------------------------------------------------------ zonas
        public static ConfigEntry<string> StartIslandMode;
        public static ConfigEntry<int> StartIslandRadius;
        public static ConfigEntry<string> SafeZones;
        public static ConfigEntry<string> ArenaZones;
        public static ConfigEntry<bool> TransportsSafe;
        public static ConfigEntry<bool> TransportsInvulnerable;
        public static ConfigEntry<bool> ArenaNoSkillLoss;
        public static ConfigEntry<bool> ArenaFriendlyFire;
        public static ConfigEntry<bool> ArenaCountsLeaderboard;

        // ----------------------------------------------------------------- castelo
        public static ConfigEntry<bool> DefenseNoSkillLoss;
        public static ConfigEntry<string> DefenseRewardItem;
        public static ConfigEntry<string> DefenseRewardByBiome;
        public static ConfigEntry<float> DefenseRewardCooldownMinutes;

        // ----------------------------------------------------------------- desafio
        public static ConfigEntry<bool> ChallengeEnabled;
        public static ConfigEntry<int> ChallengeDelaySeconds;
        public static ConfigEntry<float> ChallengeDurationMinutes;
        public static ConfigEntry<float> ChallengeCooldownMinutes;
        public static ConfigEntry<string> ChallengeSurviveReward;
        public static ConfigEntry<string> ChallengeKillReward;
        public static ConfigEntry<bool> HuntedCanUsePortals;

        // --------------------------------------------------------------------- cla
        public static ConfigEntry<bool> ClanEnabled;
        public static ConfigEntry<int> ClanMaxMembers;
        public static ConfigEntry<bool> ClanShowOnMap;

        // ------------------------------------------------------------------- tumba
        public static ConfigEntry<bool> TombstoneOwnerOnly;
        public static ConfigEntry<bool> TombstoneClanAccess;

        // ----------------------------------------------------------------- retreat
        public static ConfigEntry<float> RetreatCooldownMinutes;

        // ------------------------------------------------------------------- mundo
        public static ConfigEntry<bool> DisableRandomEvents;
        public static ConfigEntry<bool> CoinsWeightless;
        public static ConfigEntry<int> CoinsMaxStack;

        // ------------------------------------------------------------------- ranking
        public static ConfigEntry<int> LeaderboardSize;

        public static void Bind(ConfigFile config)
        {
            const string general = "PvP";
            Enabled = Bind(config, general, "Enabled", true,
                "Liga o modulo de PvP inteiro. Desligado, nenhuma regra abaixo vale.");
            ForcePvp = Bind(config, general, "ForcePvp", true,
                "PvP ligado para todos, fora das zonas seguras. O jogador nao escolhe.");
            DamageMultiplier = Bind(config, general, "DamageMultiplier", 0.5f,
                "Multiplicador do dano de jogador em jogador. 0.5 = metade do dano normal.");
            WardDefenseMultiplier = Bind(config, general, "WardDefenseMultiplier", 0.5f,
                "Multiplicador extra do dano PvP recebido dentro de um ward abastecido onde voce tem permissao (seu territorio).");
            NoFriendlyFireClan = Bind(config, general, "NoFriendlyFireClan", true,
                "Membros do mesmo cla nao se ferem.");
            NoFriendlyFireTerritory = Bind(config, general, "NoFriendlyFireTerritory", true,
                "Donos do mesmo territorio (quem tem permissao no mesmo ward) nao se ferem dentro dele.");
            ImmunityMinutes = Bind(config, general, "ImmunityMinutes", 10f,
                "Minutos em que quem foi morto por jogador nao da nem recebe dano de jogador. PvE continua normal.");
            CombatTagSeconds = Bind(config, general, "CombatTagSeconds", 30f,
                "Segundos em combate depois de dar ou levar dano PvP. Em combate: zona segura nao protege e retreat nao funciona.");
            KillCreditSeconds = Bind(config, general, "KillCreditSeconds", 20f,
                "Morte ate estes segundos depois de levar dano PvP conta como morte por jogador (queda, afogamento, fogo, mob).");
            KillFeed = Bind(config, general, "KillFeed", true,
                "Anuncia no chat quem matou quem.");

            const string pk = "PvP - PK";
            PkMinutes = Bind(config, pk, "PkMinutes", 30f,
                "Minutos que quem mata outro jogador fica marcado como PK. Nao vale para arena, defesa de territorio, alvo PK ou alvo cacado.");
            PkSkillLossMultiplier = Bind(config, pk, "PkSkillLossMultiplier", 2f,
                "Multiplicador da perda de skill de quem morre marcado como PK.");
            PkClearsOnDeath = Bind(config, pk, "PkClearsOnDeath", true,
                "A marca de PK some quando o PK morre.");

            const string zones = "PvP - Zonas";
            StartIslandMode = Bind(config, zones, "StartIslandMode", "Island",
                "Zona segura inicial. Island = a massa de terra ligada ao templo inicial (limitada pelo raio); Radius = circulo em volta do templo; Off = sem zona inicial.");
            StartIslandRadius = Bind(config, zones, "StartIslandRadius", 1500,
                "Raio maximo em metros da zona segura inicial, medido do templo inicial.");
            SafeZones = Bind(config, zones, "SafeZones", "",
                "Zonas seguras extras: Nome,x,z,raio|Nome2,x,z,raio");
            ArenaZones = Bind(config, zones, "ArenaZones", "",
                "Arenas: Nome,x,z,raio|... Dentro da arena o PvP vale sempre, sem perda de skill, sem PK e sem imunidade.");
            TransportsSafe = Bind(config, zones, "TransportsSafe", true,
                "Quem esta num barco, carroca ou montaria fica em zona segura.");
            TransportsInvulnerable = Bind(config, zones, "TransportsInvulnerable", true,
                "Barcos e carrocas nao tomam dano de jogador.");
            ArenaNoSkillLoss = Bind(config, zones, "ArenaNoSkillLoss", true,
                "Morrer na arena nao tira skill.");
            ArenaFriendlyFire = Bind(config, zones, "ArenaFriendlyFire", true,
                "Na arena, membros do mesmo cla podem lutar entre si.");
            ArenaCountsLeaderboard = Bind(config, zones, "ArenaCountsLeaderboard", false,
                "Abates na arena contam para o ranking K/D.");

            const string castle = "PvP - Defesa de Castelo";
            DefenseNoSkillLoss = Bind(config, castle, "DefenseNoSkillLoss", true,
                "Quem morre para jogador dentro do proprio territorio (ward onde tem permissao) nao perde skill.");
            DefenseRewardItem = Bind(config, castle, "DefenseRewardItem", "Coins",
                "Item da recompensa por matar invasor dentro do seu territorio.");
            DefenseRewardByBiome = Bind(config, castle, "DefenseRewardByBiome",
                "Meadows:25,BlackForest:50,Swamp:75,Mountain:100,Plains:150,Mistlands:200,AshLands:250,DeepNorth:250,Ocean:50",
                "Quantidade da recompensa de defesa por bioma. Bioma:Quantidade,...");
            DefenseRewardCooldownMinutes = Bind(config, castle, "DefenseRewardCooldownMinutes", 30f,
                "Minutos ate o mesmo invasor render recompensa de novo para o mesmo defensor.");

            const string challenge = "PvP - Desafio";
            ChallengeEnabled = Bind(config, challenge, "ChallengeEnabled", true,
                "Libera o comando /desafio.");
            ChallengeDelaySeconds = Bind(config, challenge, "ChallengeDelaySeconds", 180,
                "Segundos entre aceitar o desafio e aparecer no mapa.");
            ChallengeDurationMinutes = Bind(config, challenge, "ChallengeDurationMinutes", 60f,
                "Minutos que o desafiante fica CACADO: visivel no mapa de todos e sem zona segura.");
            ChallengeCooldownMinutes = Bind(config, challenge, "ChallengeCooldownMinutes", 120f,
                "Minutos entre dois desafios do mesmo jogador.");
            ChallengeSurviveReward = Bind(config, challenge, "ChallengeSurviveReward", "Coins:1000",
                "Recompensa de quem sobrevive ao desafio. Item:Quantidade");
            ChallengeKillReward = Bind(config, challenge, "ChallengeKillReward", "Coins:500",
                "Recompensa de quem mata o cacado. Item:Quantidade");
            HuntedCanUsePortals = Bind(config, challenge, "HuntedCanUsePortals", false,
                "O cacado pode usar portal.");

            const string clan = "PvP - Cla";
            ClanEnabled = Bind(config, clan, "ClanEnabled", true,
                "Sistema de cla embutido (/cla). Sem fogo amigo, acesso aos wards do cla e membros no mapa.");
            ClanMaxMembers = Bind(config, clan, "ClanMaxMembers", 10,
                "Maximo de membros por cla.");
            ClanShowOnMap = Bind(config, clan, "ClanShowOnMap", true,
                "Membros do cla aparecem no mapa uns dos outros.");

            const string tomb = "PvP - Tumba";
            TombstoneOwnerOnly = Bind(config, tomb, "TombstoneOwnerOnly", true,
                "So o dono abre a propria tumba (admins tambem).");
            TombstoneClanAccess = Bind(config, tomb, "TombstoneClanAccess", false,
                "Membros do cla do dono tambem abrem a tumba.");

            RetreatCooldownMinutes = Bind(config, "PvP - Retreat", "RetreatCooldownMinutes", 30f,
                "Minutos entre dois usos do /retreat. Em combate ou cacado o retreat nao funciona.");

            const string world = "PvP - Mundo";
            DisableRandomEvents = Bind(config, world, "DisableRandomEvents", true,
                "Desliga os ataques aleatorios de monstros as bases (raids do jogo).");
            CoinsWeightless = Bind(config, world, "CoinsWeightless", true,
                "Moedas (Coins) nao pesam.");
            CoinsMaxStack = Bind(config, world, "CoinsMaxStack", 5000,
                "Tamanho da pilha de moedas.");

            LeaderboardSize = Bind(config, "PvP - Ranking", "LeaderboardSize", 10,
                "Quantas linhas o /rank mostra.");

            SafeZones.SettingChanged += (_, __) => PvpZones.Invalidate();
            ArenaZones.SettingChanged += (_, __) => PvpZones.Invalidate();
            StartIslandMode.SettingChanged += (_, __) => PvpZones.Invalidate();
            StartIslandRadius.SettingChanged += (_, __) => PvpZones.Invalidate();
        }

        private static ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, T value, string description)
            => Plugin.Synced(config.Bind(section, key, value, new ConfigDescription(description)));

        public static bool Active => Enabled != null && Enabled.Value;

        // ------------------------------------------------------------------ parsing

        public struct Reward
        {
            public string Prefab;
            public int Amount;
            public bool IsValid => !string.IsNullOrEmpty(Prefab) && Amount > 0;
        }

        /// <summary>"Coins:500" vira (Coins, 500). Formato invalido vira recompensa vazia.</summary>
        public static Reward ParseReward(string text)
        {
            Reward reward = default;
            if (string.IsNullOrWhiteSpace(text)) return reward;
            string[] parts = text.Split(':');
            if (parts.Length != 2) return reward;
            if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount)) return reward;
            reward.Prefab = parts[0].Trim();
            reward.Amount = amount;
            return reward;
        }

        /// <summary>Recompensa de defesa para o bioma, lida de DefenseRewardByBiome.</summary>
        public static int DefenseRewardFor(Heightmap.Biome biome)
        {
            string name = biome.ToString();
            foreach (string entry in DefenseRewardByBiome.Value.Split(','))
            {
                string[] parts = entry.Split(':');
                if (parts.Length != 2) continue;
                if (!string.Equals(parts[0].Trim(), name, StringComparison.OrdinalIgnoreCase)) continue;
                if (int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
                    return Mathf.Max(0, amount);
            }
            return 0;
        }

        public sealed class Zone
        {
            public string Name;
            public Vector2 Center;
            public float Radius;

            public bool Contains(Vector3 point)
                => Radius > 0f && (new Vector2(point.x, point.z) - Center).sqrMagnitude <= Radius * Radius;
        }

        /// <summary>"Nome,x,z,raio|Nome2,x,z,raio". Entradas quebradas sao ignoradas com aviso.</summary>
        public static List<Zone> ParseZones(string text, string what)
        {
            List<Zone> zones = new List<Zone>();
            if (string.IsNullOrWhiteSpace(text)) return zones;

            foreach (string raw in text.Split('|'))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string[] p = raw.Split(',');
                if (p.Length < 4
                    || !float.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                    || !float.TryParse(p[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float z)
                    || !float.TryParse(p[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float r))
                {
                    Debug.LogWarning($"[Deadheim PvP] {what}: entrada ignorada '{raw}'. Formato: Nome,x,z,raio");
                    continue;
                }
                zones.Add(new Zone { Name = p[0].Trim(), Center = new Vector2(x, z), Radius = r });
            }
            return zones;
        }
    }
}
