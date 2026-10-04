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
        public static ConfigEntry<float> StaggerMultiplier;
        public static ConfigEntry<float> WardDefenseMultiplier;
        public static ConfigEntry<bool> NoFriendlyFireGuild;
        public static ConfigEntry<bool> NoFriendlyFireGroup;
        public static ConfigEntry<bool> NoFriendlyFireTerritory;
        public static ConfigEntry<float> ImmunityMinutes;
        public static ConfigEntry<float> CombatTagSeconds;
        public static ConfigEntry<float> KillCreditSeconds;
        public static ConfigEntry<bool> KillFeed;
        public static ConfigEntry<bool> KillerMustBeOnline;
        public static ConfigEntry<float> KillerMaxDistance;

        // --------------------------------------------------------------------- PK
        public static ConfigEntry<string> PkTiers;
        public static ConfigEntry<float> PkSkillLossMultiplier;
        public static ConfigEntry<bool> PkClearsOnDeath;
        public static ConfigEntry<bool> PkPenaltyOnPveDeath;
        public static ConfigEntry<bool> PkPermanentOnMap;
        public static ConfigEntry<bool> PkTimeOnlineOnly;
        public static ConfigEntry<bool> PkNoSafeZone;
        public static ConfigEntry<bool> PkClearsOnPveDeath;
        public static ConfigEntry<bool> AggressorRule;
        public static ConfigEntry<float> AggressorSeconds;
        public static ConfigEntry<bool> AggressorPausesInCombat;

        // ------------------------------------------------------------------ zonas
        public static ConfigEntry<string> StartIslandMode;
        public static ConfigEntry<int> StartIslandRadius;
        public static ConfigEntry<string> StartIslandBiomes;
        public static ConfigEntry<float> ShipSafeMinSpeed;
        public static ConfigEntry<float> MountSafeMinSpeed;
        public static ConfigEntry<string> SafeZones;
        public static ConfigEntry<string> ArenaZones;
        public static ConfigEntry<bool> TransportsSafe;
        public static ConfigEntry<bool> TransportsInvulnerable;
        public static ConfigEntry<bool> ShipsSafe;
        public static ConfigEntry<bool> ShipsInvulnerable;
        public static ConfigEntry<bool> ArenaNoSkillLoss;
        public static ConfigEntry<bool> ArenaFriendlyFire;
        public static ConfigEntry<bool> ArenaCountsLeaderboard;

        // ------------------------------------------------------------------ chefes
        public static ConfigEntry<int> BossGapMax;
        public static ConfigEntry<string> BossOrder;

        // ------------------------------------------------------- castelo (RaidSystem)
        public static ConfigEntry<bool> CastleNoSkillLoss;
        public static ConfigEntry<bool> CastleIgnoresImmunity;
        public static ConfigEntry<string> CastleRewardItem;
        public static ConfigEntry<string> CastleRewardByBiome;
        public static ConfigEntry<float> CastleRewardCooldownMinutes;
        public static ConfigEntry<int> CastleRewardDailyCap;

        // ------------------------------------------------------------------ bounty
        public static ConfigEntry<bool> BountyEnabled;
        public static ConfigEntry<int> BountyMinimum;
        public static ConfigEntry<float> BountyMinutesPer1000;
        public static ConfigEntry<int> BountyUntilDeathAt;
        public static ConfigEntry<float> BountyKillerSharePercent;
        public static ConfigEntry<float> BountyBuyoutMultiplier;
        public static ConfigEntry<float> BountyExpiredRefundPercent;
        public static ConfigEntry<int> BountyDelaySeconds;
        public static ConfigEntry<float> BountyCooldownMinutes;
        public static ConfigEntry<bool> HuntedCanUsePortals;
        public static ConfigEntry<bool> BountyPausesInOwnWard;
        public static ConfigEntry<bool> HuntedNoWardDefense;
        public static ConfigEntry<int> BountyMinPlayers;
        public static ConfigEntry<bool> BountyArenaKillsCount;
        public static ConfigEntry<int> BountyDailyCapPerPlayer;
        public static ConfigEntry<float> BountyAllyHours;

        // PvE permanente
        public static ConfigEntry<bool> PveEnabled;
        public static ConfigEntry<string> PveTitle;
        public static ConfigEntry<float> PveSkillMultiplier;
        public static ConfigEntry<float> PveResourceRate;
        public static ConfigEntry<float> PvpResourceRate;

        // ------------------------------------------------------------------- fuga
        public static ConfigEntry<bool> CombatBlocksTeleport;
        public static ConfigEntry<bool> CombatStatusIcon;
        public static ConfigEntry<bool> StateBuffs;
        public static ConfigEntry<bool> CombatFromPve;
        public static ConfigEntry<string> CombatLogout;

        // ------------------------------------------------------------------ saque
        public static ConfigEntry<float> PvpCoinDropPercent;
        public static ConfigEntry<float> PvpCargoDropPercent;
        public static ConfigEntry<string> PvpCargoTypes;
        public static ConfigEntry<string> PvpCargoKeep;

        // ------------------------------------------------------------------- tumba
        public static ConfigEntry<bool> TombstoneOwnerOnly;
        public static ConfigEntry<bool> TombstoneGuildAccess;

        // ----------------------------------------------------------------- retreat
        public static ConfigEntry<float> RetreatCooldownMinutes;
        public static ConfigEntry<bool> RetreatBlockedByPveCombat;

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
            StaggerMultiplier = Bind(config, general, "StaggerMultiplier", 1f,
                "Multiplicador do stagger (cambalear) de golpe de jogador em jogador. 1 = vanilla, 0.5 = metade, 0 = sem stagger no PvP.");
            WardDefenseMultiplier = Bind(config, general, "WardDefenseMultiplier", 0.5f,
                "Multiplicador extra do dano PvP recebido dentro de um ward abastecido onde voce tem permissao (seu territorio).");
            NoFriendlyFireGuild = Bind(config, general, "NoFriendlyFireGuild", true,
                "Membros da mesma guilda (mod Guilds) nao se ferem.");
            NoFriendlyFireGroup = Bind(config, general, "NoFriendlyFireGroup", true,
                "Membros do mesmo grupo (mod Groups, a party) nao se ferem.");
            NoFriendlyFireTerritory = Bind(config, general, "NoFriendlyFireTerritory", true,
                "Donos do mesmo territorio (quem tem permissao no mesmo ward) nao se ferem dentro dele.");
            ImmunityMinutes = Bind(config, general, "ImmunityMinutes", 10f,
                "Minutos (de jogo aberto) em que quem foi morto por jogador nao da nem recebe dano de jogador. PvE continua normal.");
            CombatTagSeconds = Bind(config, general, "CombatTagSeconds", 30f,
                "Segundos em combate depois de dar ou levar dano PvP. Em combate: zona segura nao protege e retreat nao funciona.");
            KillCreditSeconds = Bind(config, general, "KillCreditSeconds", 20f,
                "Morte ate estes segundos depois de levar dano PvP conta como morte por jogador (queda, afogamento, fogo, mob).");
            KillFeed = Bind(config, general, "KillFeed", true,
                "Anuncia no chat quem matou quem.");
            KillerMustBeOnline = Bind(config, general, "KillerMustBeOnline", true,
                "O servidor so aceita morte por jogador se o matador for um jogador conectado. Desligue so no teste " +
                "solo (o boneco Dummy nao e um jogador conectado).");
            KillerMaxDistance = Bind(config, general, "KillerMaxDistance", 300f,
                "Distancia maxima (m) entre o matador e a vitima para o servidor aceitar uma morte por jogador. " +
                "Mais longe, a morte conta como PvE. 0 = sem limite.");

            const string pk = "PvP - PK";
            PkTiers = Bind(config, pk, "PkTiers", "1:60:Skills,2:120:Skills,3:1440:Unequipped,5:-1:All",
                "Niveis de PK pela quantidade de abates que deram PK seguidos: Abates:Minutos:Perda,... " +
                "Minutos -1 = PK permanente ate ser morto por jogador (fica no mapa de todos). " +
                "Perda ao morrer marcado: Skills = so skill (x PkSkillLossMultiplier); Unequipped = skill e tudo que nao esta " +
                "equipado cai no chao; All = skill e o inventario inteiro cai no chao. A contagem zera quando a marca some. " +
                "Nao gera PK: arena, castelo em raid, alvo PK, alvo cacado ou alvo agressor (AggressorRule).");
            PkSkillLossMultiplier = Bind(config, pk, "PkSkillLossMultiplier", 2f,
                "Multiplicador da perda de skill de quem morre marcado como PK.");
            PkClearsOnDeath = Bind(config, pk, "PkClearsOnDeath", true,
                "A marca de PK (nao permanente) some quando o PK e morto por jogador. O PK permanente sempre sai assim.");
            PkClearsOnPveDeath = Bind(config, pk, "PkClearsOnPveDeath", false,
                "Morrer de PvE (monstro, queda, afogamento) tambem tira a marca de PK nao permanente. Desligado, " +
                "a marca fica: morrer de proposito em casa nao limpa o PK.");
            PkPenaltyOnPveDeath = Bind(config, pk, "PkPenaltyOnPveDeath", true,
                "A perda do nivel de PK vale em qualquer morte. Desligado, so quando o PK e morto por jogador " +
                "(morte PvE perde skill normal).");
            PkTimeOnlineOnly = Bind(config, pk, "PkTimeOnlineOnly", true,
                "O tempo de PK so corre com o jogador online. Desligado, corre tambem offline (deslogar limpa a marca).");
            PkNoSafeZone = Bind(config, pk, "PkNoSafeZone", true,
                "PK nao tem zona segura nem protecao de transporte, como o cacado. Quem esta na zona segura pode atacar " +
                "um PK (ou um cacado) e, ao bater, entra em combate e perde a protecao.");
            PkPermanentOnMap = Bind(config, pk, "PkPermanentOnMap", true,
                "PK permanente aparece no mapa de todos, como o cacado.");
            AggressorRule = Bind(config, pk, "AggressorRule", true,
                "Legitima defesa: quem bate primeiro em um jogador sem marca vira AGRESSOR. Matar um agressor " +
                "(inclusive quem te atacou) nao faz de ninguem PK.");
            AggressorSeconds = Bind(config, pk, "AggressorSeconds", 600f,
                "Segundos que a marca de agressor dura depois do ultimo golpe dado. Morrer como agressor nao tem perda extra.");
            AggressorPausesInCombat = Bind(config, pk, "AggressorPausesInCombat", true,
                "O tempo de agressor para enquanto ele esta em combate com jogador, para a marca nao acabar no meio da luta.");

            const string zones = "PvP - Zonas";
            StartIslandMode = Bind(config, zones, "StartIslandMode", "Island",
                "Zona segura inicial. Island = a massa de terra ligada ao templo inicial (limitada pelo raio); Radius = circulo em volta do templo; Off = sem zona inicial.");
            StartIslandRadius = Bind(config, zones, "StartIslandRadius", 1500,
                "Raio maximo em metros da zona segura inicial, medido do templo inicial.");
            StartIslandBiomes = Bind(config, zones, "StartIslandBiomes", "",
                "Biomas que a zona segura inicial pode cobrir, separados por virgula (ex.: Meadows). " +
                "Vazio = qualquer bioma ligado ao templo (no mundo de teste: Campina e Floresta Negra).");
            SafeZones = Bind(config, zones, "SafeZones", "",
                "Zonas seguras extras: Nome,x,z,raio|Nome2,x,z,raio");
            ArenaZones = Bind(config, zones, "ArenaZones", "",
                "Arenas: Nome,x,z,raio|... Dentro da arena o PvP vale sempre, sem perda de skill, sem PK e sem imunidade.");
            TransportsSafe = Bind(config, zones, "TransportsSafe", false,
                "Quem esta numa carroca ou montaria andando fica em zona segura. Desligado, transporte nao protege " +
                "ninguem (a carga em transito e alvo). Barco e ShipsSafe.");
            TransportsInvulnerable = Bind(config, zones, "TransportsInvulnerable", false,
                "Carrocas e montarias com sela nao tomam dano de jogador. Desligado, da para quebrar carroca e matar " +
                "montaria. Barco e ShipsInvulnerable.");
            ShipsSafe = Bind(config, zones, "ShipsSafe", false,
                "Quem esta num barco andando fica em zona segura. Desligado, barco e alvo (pirataria).");
            ShipsInvulnerable = Bind(config, zones, "ShipsInvulnerable", false,
                "Barcos nao tomam dano de jogador. Desligado, da para afundar barco.");
            ShipSafeMinSpeed = Bind(config, zones, "ShipSafeMinSpeed", 1f,
                "O barco so protege quem esta nele se estiver andando acima desta velocidade (m/s). " +
                "Barco parado ou encalhado nao e abrigo. 0 = protege parado tambem.");
            MountSafeMinSpeed = Bind(config, zones, "MountSafeMinSpeed", 1f,
                "A montaria so protege quem esta montado se estiver andando acima desta velocidade (m/s). " +
                "Montaria parada ao lado de uma luta nao e abrigo. 0 = protege parada tambem.");
            ArenaNoSkillLoss = Bind(config, zones, "ArenaNoSkillLoss", true,
                "Morrer na arena nao tira skill.");
            ArenaFriendlyFire = Bind(config, zones, "ArenaFriendlyFire", true,
                "Na arena, membros da mesma guilda podem lutar entre si.");
            ArenaCountsLeaderboard = Bind(config, zones, "ArenaCountsLeaderboard", false,
                "Abates na arena contam para o ranking K/D.");

            const string bosses = "PvP - Chefes";
            BossGapMax = Bind(config, bosses, "BossGapMax", 1,
                "Faixa de PvP por chefes derrotados: dois jogadores so se ferem se a diferenca de chefes entre eles for " +
                "no maximo esta. 1 = quem derrotou a Massa Ossea (3) luta com quem esta entre o Anciao (2) e a Moder (4). " +
                "Conta o chefe mais avancado que o personagem ajudou a matar (deu dano). Nao vale na arena. -1 = desligado.");
            BossOrder = Bind(config, bosses, "BossOrder",
                "$enemy_eikthyr,$enemy_gdking,$enemy_bonemass,$enemy_dragon,$enemy_goblinking,$enemy_seekerqueen,$enemy_fader",
                "Chefes em ordem de progressao (nome interno do inimigo). O nivel do jogador e a posicao do mais avancado " +
                "que ele ajudou a matar: 0 = nenhum, 1 = Eikthyr, 2 = Anciao, 3 = Massa Ossea, 4 = Moder...");

            // Castelo e zona de guerra do RaidSystem (Raid Zones). Sem o RaidSystem, nao ha castelo.
            const string castle = "PvP - Castelo";
            CastleNoSkillLoss = Bind(config, castle, "CastleNoSkillLoss", true,
                "Quem morre para jogador dentro de um castelo do RaidSystem nao perde skill, atacante ou defensor.");
            CastleIgnoresImmunity = Bind(config, castle, "CastleIgnoresImmunity", true,
                "No castelo a imunidade pos-morte nao vale, e morrer la nao a concede: e zona de guerra.");
            CastleRewardItem = Bind(config, castle, "CastleRewardItem", "Coins",
                "Item da recompensa de quem, sendo da guilda dona do castelo, mata invasor la dentro.");
            CastleRewardByBiome = Bind(config, castle, "CastleRewardByBiome",
                "Meadows:25,BlackForest:50,Swamp:75,Mountain:100,Plains:150,Mistlands:200,AshLands:250,DeepNorth:250,Ocean:50",
                "Quantidade da recompensa de defesa por bioma do castelo. Bioma:Quantidade,...");
            CastleRewardCooldownMinutes = Bind(config, castle, "CastleRewardCooldownMinutes", 1440f,
                "Minutos ate o mesmo invasor render recompensa de novo para o mesmo defensor. Fica salvo: o restart nao zera.");
            CastleRewardDailyCap = Bind(config, castle, "CastleRewardDailyCap", 500,
                "Teto de recompensa de defesa (em unidades de CastleRewardItem) que cada defensor recebe por dia (UTC do " +
                "servidor). A recompensa e criada do nada: o teto limita duas contas combinando 'defesas'. 0 = sem teto.");

            const string bounty = "PvP - Bounty";
            BountyEnabled = Bind(config, bounty, "BountyEnabled", true,
                "Libera o /bounty: um jogador paga moedas para outro ser cacado. Quem matar o alvo fica com parte do pote.");
            BountyMinimum = Bind(config, bounty, "BountyMinimum", 1000,
                "Menor valor (Coins) de uma bounty.");
            BountyMinutesPer1000 = Bind(config, bounty, "BountyMinutesPer1000", 60f,
                "Minutos de cacada por 1000 moedas no pote. O tempo so corre com o alvo online.");
            BountyUntilDeathAt = Bind(config, bounty, "BountyUntilDeathAt", 5000,
                "Com o pote a partir deste valor a bounty nao expira: so acaba quando o alvo morrer para jogador. 0 = nunca.");
            BountyKillerSharePercent = Bind(config, bounty, "BountyKillerSharePercent", 75f,
                "Porcentagem do pote que vai para quem matar o alvo. O resto e da casa (sai do jogo).");
            BountyBuyoutMultiplier = Bind(config, bounty, "BountyBuyoutMultiplier", 1.5f,
                "O alvo pode pagar o pote vezes este valor para a casa e encerrar a bounty (/bounty pagar). 0 = desligado.");
            BountyExpiredRefundPercent = Bind(config, bounty, "BountyExpiredRefundPercent", 0f,
                "Porcentagem do pote devolvida a quem pagou quando a bounty expira sem ninguem matar o alvo. O resto e da casa.");
            BountyDelaySeconds = Bind(config, bounty, "BountyDelaySeconds", 600,
                "Segundos entre a bounty ser colocada e o alvo virar CACADO. Nesse tempo ele e avisado e ainda pode teleportar.");
            BountyCooldownMinutes = Bind(config, bounty, "BountyCooldownMinutes", 120f,
                "Minutos depois de uma bounty acabar ate o mesmo jogador poder receber outra.");
            HuntedCanUsePortals = Bind(config, bounty, "HuntedCanUsePortals", false,
                "O cacado pode usar portal, NPC teleportador e pedra de retorno.");
            BountyPausesInOwnWard = Bind(config, bounty, "BountyPausesInOwnWard", true,
                "O tempo da bounty para enquanto o cacado esta dentro de um ward onde tem permissao.");
            HuntedNoWardDefense = Bind(config, bounty, "HuntedNoWardDefense", true,
                "O cacado nao tem a reducao de dano do proprio ward (WardDefenseMultiplier).");
            BountyMinPlayers = Bind(config, bounty, "BountyMinPlayers", 3,
                "Jogadores online (contando o alvo) para o tempo da bounty correr. Abaixo disso ele para.");
            BountyArenaKillsCount = Bind(config, bounty, "BountyArenaKillsCount", false,
                "Matar o cacado dentro de uma arena paga a bounty.");
            BountyDailyCapPerPlayer = Bind(config, bounty, "BountyDailyCapPerPlayer", 10000,
                "Teto de moedas que cada jogador pode colocar em bounties por dia (dia UTC do servidor). O inventario e do " +
                "cliente: o servidor confia no valor que ele diz ter pago, entao o teto limita o que um cliente modificado " +
                "consegue por num pote sem pagar. Bounty de admin (paga pela casa) nao conta. 0 = sem teto.");
            BountyAllyHours = Bind(config, bounty, "BountyAllyHours", 24f,
                "A bounty nao paga quem e da guilda do alvo, nem quem foi da mesma guilda que ele nestas ultimas horas: " +
                "o alvo nao entrega o pote a um amigo. So paga golpe final de jogador (queda ou fogo depois de um golpe " +
                "nao). Nesses casos a bounty acaba e o pote fica com a casa. 0 = so a guilda de agora.");

            const string pve = "PvP - PvE permanente";
            PveEnabled = Bind(config, pve, "PveEnabled", true,
                "Libera o /pve: o jogador sai do PvP para sempre (so admin desfaz com /pvpadmin pve <jogador>).");
            PveTitle = Bind(config, pve, "PveTitle", "Mercador",
                "Titulo de quem e PvE permanente, no nome sobre a cabeca e no HUD.");
            PveSkillMultiplier = Bind(config, pve, "PveSkillMultiplier", 0.5f,
                "Ganho de skill do PvE, multiplicado em cima do SkillMultiplier do servidor. 0.5 = metade.");
            PveResourceRate = Bind(config, pve, "PveResourceRate", 1f,
                "Taxa de coleta de quem e PvE permanente (arvore, pedra, minerio, colheita, drop de monstro). " +
                "1 = normal. 0 = a do mundo.");

            const string escape = "PvP - Fuga";
            CombatBlocksTeleport = Bind(config, escape, "CombatBlocksTeleport", true,
                "Em combate nenhum teleporte longo funciona: portal, NPC teleportador, pedra de retorno, retreat.");
            CombatStatusIcon = Bind(config, escape, "CombatStatusIcon", true,
                "Mostra o buff \"Em combate\" com contagem na barra de efeitos (o que o mod Combat fazia), tambem na luta " +
                "com monstro quando ela bloqueia algo (CombatFromPve ou RetreatBlockedByPveCombat).");
            StateBuffs = Bind(config, escape, "StateBuffs", true,
                "Mostra os outros estados do PvP como buffs na barra de efeitos, com contagem: imune a PvP, PK, agressor, " +
                "cacado, bounty, zona segura e PvE permanente.");
            CombatFromPve = Bind(config, escape, "CombatFromPve", false,
                "Dano de/em monstro tambem conta como combate, mas so para bloquear teleporte, retreat e pedra " +
                "(zona segura continua valendo contra jogador). O mod Combat antigo fazia isso por padrao.");
            CombatLogout = Bind(config, escape, "CombatLogout", "Rank",
                "Deslogar em combate. Off = nada; Rank = conta morte para quem saiu e abate para quem bateu (nao mata: " +
                "quem caiu de verdade nao perde nada); Death = alem disso, ao voltar o jogador morre onde saiu.");

            const string loot = "PvP - Saque";
            PvpCoinDropPercent = Bind(config, loot, "PvpCoinDropPercent", 100f,
                "Porcentagem das moedas do inventario que quem morre para jogador deixa no chao, fora da tumba, " +
                "seja PK ou nao. 100 = todas. 0 = desligado. Nao vale na arena.");
            PvpCargoDropPercent = Bind(config, loot, "PvpCargoDropPercent", 50f,
                "Porcentagem da carga (o que nao esta equipado e e de um tipo de PvpCargoTypes: minerio, metal, " +
                "comida, trofeu...) que cai no chao na morte por jogador. Equipamento fica na tumba. 0 = desligado. Nao vale na arena.");
            PvpCargoTypes = Bind(config, loot, "PvpCargoTypes", "Material,Consumable,Trophy,Fish",
                "Tipos de item que contam como carga (ItemType do jogo): Material (minerio, metal, madeira...), " +
                "Consumable (comida, hidromel), Trophy, Fish, Misc, Ammo...");
            PvpCargoKeep = Bind(config, loot, "PvpCargoKeep", "PortalToken,ResetToken",
                "Itens (nome do prefab) que nunca caem como carga, mesmo sendo de um tipo de PvpCargoTypes. Os itens do " +
                "proprio Deadheim (DeadToken, os tokens antigos, GarantiaRefino, kits) ja ficam de fora sem estar aqui.");

            const string tomb = "PvP - Tumba";
            TombstoneOwnerOnly = Bind(config, tomb, "TombstoneOwnerOnly", true,
                "So o dono abre a propria tumba (admins tambem).");
            TombstoneGuildAccess = Bind(config, tomb, "TombstoneGuildAccess", false,
                "Membros da guilda do dono tambem abrem a tumba (o dono precisa estar por perto).");

            RetreatCooldownMinutes = Bind(config, "PvP - Retreat", "RetreatCooldownMinutes", 30f,
                "Minutos (de jogo aberto) entre dois usos do /retreat. Em combate ou cacado o retreat nao funciona.");
            RetreatBlockedByPveCombat = Bind(config, "PvP - Retreat", "RetreatBlockedByPveCombat", true,
                "Luta com monstro (dar ou levar dano) tambem bloqueia o /retreat e a pedra de retorno por CombatTagSeconds. " +
                "Portal continua seguindo CombatFromPve.");

            const string world = "PvP - Mundo";
            DisableRandomEvents = Bind(config, world, "DisableRandomEvents", true,
                "Desliga os ataques aleatorios de monstros as bases (raids do jogo).");
            CoinsWeightless = Bind(config, world, "CoinsWeightless", true,
                "Moedas (Coins) nao pesam.");
            CoinsMaxStack = Bind(config, world, "CoinsMaxStack", 5000,
                "Tamanho da pilha de moedas.");
            PvpResourceRate = Bind(config, world, "PvpResourceRate", 2f,
                "Taxa de coleta de quem joga PvP (todo mundo que nao e PvE permanente): arvore, pedra, minerio, " +
                "colheita, drop de monstro. 2 = dobro. 0 = a do mundo. O PvE usa PveResourceRate.");

            LeaderboardSize = Bind(config, "PvP - Ranking", "LeaderboardSize", 10,
                "Quantas linhas o /rank mostra.");

            SafeZones.SettingChanged += (_, __) => PvpZones.Invalidate();
            ArenaZones.SettingChanged += (_, __) => PvpZones.Invalidate();
            StartIslandMode.SettingChanged += (_, __) => PvpZones.Invalidate();
            StartIslandRadius.SettingChanged += (_, __) => PvpZones.Invalidate();
            StartIslandBiomes.SettingChanged += (_, __) => PvpZones.Invalidate();
            PveResourceRate.SettingChanged += (_, __) => PvpPve.RefreshResourceRate();
            PvpResourceRate.SettingChanged += (_, __) => PvpPve.RefreshResourceRate();
            Enabled.SettingChanged += (_, __) => PvpPve.RefreshResourceRate();
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

        public enum PkPenalty
        {
            Skills,
            Unequipped,
            All,
        }

        public struct PkTier
        {
            public int Kills;
            /// <summary>Minutos de PK; 0 ou menos = permanente.</summary>
            public float Minutes;
            public PkPenalty Penalty;
            public bool Permanent => Minutes <= 0f;
        }

        private static string _tiersText;
        private static List<PkTier> _tiers;

        /// <summary>Niveis de PK em ordem de abates, lidos de PkTiers ("1:60:Skills,...").</summary>
        public static List<PkTier> Tiers
        {
            get
            {
                string text = PkTiers.Value ?? string.Empty;
                if (_tiers != null && _tiersText == text) return _tiers;
                _tiersText = text;
                _tiers = new List<PkTier>();
                foreach (string raw in text.Split(','))
                {
                    string[] p = raw.Split(':');
                    if (p.Length < 2
                        || !int.TryParse(p[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int kills)
                        || !float.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float minutes))
                    {
                        if (!string.IsNullOrWhiteSpace(raw))
                            Debug.LogWarning($"[Deadheim PvP] PkTiers: entrada ignorada '{raw}'. Formato: Abates:Minutos:Perda");
                        continue;
                    }
                    PkPenalty penalty = PkPenalty.Skills;
                    if (p.Length >= 3 && !Enum.TryParse(p[2].Trim(), true, out penalty)) penalty = PkPenalty.Skills;
                    _tiers.Add(new PkTier { Kills = Math.Max(1, kills), Minutes = minutes, Penalty = penalty });
                }
                _tiers.Sort((a, b) => a.Kills.CompareTo(b.Kills));
                return _tiers;
            }
        }

        /// <summary>Nivel de PK de quem acumula <paramref name="streak"/> abates que deram PK.</summary>
        public static bool TryGetTier(int streak, out PkTier tier)
        {
            tier = default;
            bool found = false;
            foreach (PkTier candidate in Tiers)
            {
                if (candidate.Kills > streak) break;
                tier = candidate;
                found = true;
            }
            return found;
        }

        private static string _cargoTypesText;
        private static HashSet<ItemDrop.ItemData.ItemType> _cargoTypes;
        private static string _cargoKeepText;
        private static HashSet<string> _cargoKeep;

        /// <summary>Tipos de item que contam como carga (PvpCargoTypes).</summary>
        public static HashSet<ItemDrop.ItemData.ItemType> CargoTypes
        {
            get
            {
                string text = PvpCargoTypes.Value ?? string.Empty;
                if (_cargoTypes != null && _cargoTypesText == text) return _cargoTypes;
                _cargoTypesText = text;
                _cargoTypes = new HashSet<ItemDrop.ItemData.ItemType>();
                foreach (string raw in text.Split(','))
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    try { _cargoTypes.Add((ItemDrop.ItemData.ItemType)Enum.Parse(typeof(ItemDrop.ItemData.ItemType), raw.Trim(), true)); }
                    catch (Exception) { Debug.LogWarning($"[Deadheim PvP] PvpCargoTypes: tipo de item desconhecido '{raw.Trim()}'."); }
                }
                return _cargoTypes;
            }
        }

        /// <summary>Prefabs que nunca caem como carga (PvpCargoKeep).</summary>
        public static HashSet<string> CargoKeep
        {
            get
            {
                string text = PvpCargoKeep.Value ?? string.Empty;
                if (_cargoKeep != null && _cargoKeepText == text) return _cargoKeep;
                _cargoKeepText = text;
                _cargoKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string raw in text.Split(','))
                    if (!string.IsNullOrWhiteSpace(raw)) _cargoKeep.Add(raw.Trim());
                return _cargoKeep;
            }
        }

        /// <summary>Recompensa de defesa de castelo para o bioma, lida de CastleRewardByBiome.</summary>
        public static int CastleRewardFor(Heightmap.Biome biome)
        {
            string name = biome.ToString();
            foreach (string entry in CastleRewardByBiome.Value.Split(','))
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
