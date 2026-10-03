using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    [BepInPlugin(PluginGUID, PluginGUID, Version)]
    [BepInDependency(VipList.VipListPlugin.PluginGuid)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Version = "7.4.0";
        public const string PluginGUID = "Detalhes.Deadheim";

        // No lugar do NetworkCompatibility(EveryoneMustHaveMod, Minor) e da config
        // IsAdminOnly do Jotunn: o servidor recusa quem nao tem o mod ou tem uma
        // versao abaixo da minima, e as configs de servidor valem as do servidor.
        // Minimo 7.4.0: as regras de PvP rodam no cliente de quem leva o golpe, entao um
        // cliente sem o modulo de PvP seria alvo sem zona segura, imunidade ou reducao. O
        // protocolo do PvP mudou em 7.3.1 (bounty com id de pedido e resposta, causa no pacote
        // de morte, hora do servidor no estado), e as correcoes de cliente (relogio, pedra,
        // canal so do servidor) so valem se todo mundo estiver nela.
        // 7.4.0 traz a Ward de Territorio (prefab novo) e a contagem dela no RPC de wards.
        private static readonly ConfigSync ServerConfigSync = new ConfigSync(PluginGUID)
        {
            DisplayName = PluginGUID,
            CurrentVersion = Version,
            MinimumRequiredVersion = "7.4.0",
            ModRequired = true,
            IsLocked = true
        };

        private bool _serverConfigApplied;
        public static ConfigEntry<string> OnlyAdminPieces;
        public static ConfigEntry<string> VipPortalNames;
        public static ConfigEntry<int> WardRadius;
        public static ConfigEntry<string> StaffMessage;
        public static ConfigEntry<string> DungeonPrefabs;
        public static ConfigEntry<float> SkillMultiplier;
        public static ConfigEntry<float> BoatWindSpeedmultiplier;
        public static ConfigEntry<int> SafeArea;        
        public static ConfigEntry<int> WardLimit;       
        public static ConfigEntry<int> WardLimitVip;
        public static ConfigEntry<int> WardChargeDurationInSec;  
        public static ConfigEntry<bool> ResetWorldDay;
        public static ConfigEntry<int> SkillCap;
        public static ConfigEntry<string> PortalMaterials;
        public static ConfigEntry<int> CartographyTableAmount;
        public static ConfigEntry<bool> EdgeOfWorldKills;
        public static ConfigEntry<bool> MonsterScalingAlliesOnly;

        public static string PlayerName = "";

        public static int PlayerWardCount = 999;
        public static int PlayerPortalCount = 999;
        /// <summary>Wards de Territorio do jogador local, contadas pelo servidor. 999 = ainda sem resposta.</summary>
        public static int PlayerTerritoryWardCount = 999;

        public static int maxPlayers = 50;
        public static List<ZRpc> validatedUsers = new List<ZRpc>();

        public static bool hasSpawned = false;
        Harmony _harmony = new Harmony("Detalhes.deadheim");

        internal static ConfigEntry<T> Synced<T>(ConfigEntry<T> entry)
        {
            ServerConfigSync.AddConfigEntry(entry).SynchronizedConfig = true;
            return entry;
        }

        /// <summary>
        /// Roda uma vez por conexao, quando os valores do servidor ja estao aplicados.
        /// O SourceOfTruthChanged do ServerSync dispara ANTES de aplicar os valores,
        /// entao ele deixaria isto rodar com a config local; InitialSyncDone so vira
        /// true depois. No servidor o InitialSyncDone tambem e true, mas ele continua
        /// fonte da verdade: por isso a segunda condicao -- e so cliente, como era no Jotunn.
        /// </summary>
        private void ApplyServerConfigOnce()
        {
            bool received = ServerConfigSync.InitialSyncDone && !ServerConfigSync.IsSourceOfTruth;
            if (!received)
            {
                _serverConfigApplied = false;
                return;
            }
            if (_serverConfigApplied) return;
            _serverConfigApplied = true;

            ItemService.ModifyItemsCost();
            ItemService.StubNoLife();
            ItemService.OnlyAdminPieces();
            Pvp.PvpModule.ApplyItemTweaks();

            Logger.LogInfo("Config do servidor recebida e aplicada.");
        }

        private void Update()
        {
            ApplyServerConfigOnce();
            Wards.WardCore.Update();
            Pvp.PvpModule.Update();
            Ajustes.AjustesMenu.Update();

            Player localPlayer = Player.m_localPlayer;
            bool flag = Player.m_localPlayer == null;
            if (!flag)
            {
                bool flag2 = localPlayer.m_hovering;
                if (flag2)
                {
                    Interactable componentInParent = localPlayer.m_hovering.GetComponentInParent<Interactable>();
                    bool flag3 = componentInParent != null;
                    if (flag3)
                    {
                        bool flag4 = componentInParent is Bed;
                        if (flag4)
                        {
                            Bed bed = (Bed)componentInParent;
                            bool flag5 = bed.IsMine();
                            if (flag5)
                            {
                                bool keyDown = Input.GetKeyDown(KeyCode.P);
                                if (keyDown)
                                {
                                    Retreat.SetHearthStonePosition();
                                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Seu novo ponto de Retreat", 0, null);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void Awake()
        {
            Config.SaveOnConfigSet = true;

            Wards.WardProfiles.BindConfigs(Config);
            Wards.WardProfiles.LoadAssets();

            OnlyAdminPieces = Synced(Config.Bind("Server config", "OnlyAdminPieces", "SHGateHouse,SHWallMusteringHall,SHTowerSquareTwoFloorCenter,SHTowerSquareTwoFloorCorner,SHTowerSquareTwoFloorJunction,SHWallOpenTwoFloorCapped,SHWallOpenTwoFloorWithNest,SHWallOpenTwoFloorWithNestCapped,SHWallOpenTwoFloor,SHEnclosedTower,SHBunkhouse,SHWell,SHOuterWallCovered,SHOuterWallOpenCapped,SHOuterWallOpen,SHOuterWallTowerSquareCenter,SHOuterWallTowerTransition,SHOuterWallTowerRound,SHOuterWallGate,SHWatchtower,SHTowerRoundWallEnd,SHOuterWallCoverdCapped,SHWallInnerArch,SHWallInnerPillar,SHWallInnerPlain,SHWallInnerPosh,SHHouseSmall,SHHouseMedium,SHHouseLarge,SHHayBarn,SHOldBarn,SHStorageBarn,SHMainHall",
new ConfigDescription("OnlyAdminPieces")));

            VipPortalNames = Synced(Config.Bind("Server config", "VipPortalNames", "cavalinho,eguinha",
new ConfigDescription("VipPortalNames")));

            SkillCap = Synced(Config.Bind("Server config", "SkillCap", 100,
new ConfigDescription("SkillCap")));



            StaffMessage = Synced(Config.Bind("Server config", "StaffMessage", "",
new ConfigDescription("StaffMessage")));

            DungeonPrefabs = Synced(Config.Bind("Server config", "DungeonPrefabs", "dungeon_forestcrypt_door,dungeon_sunkencrypt_irongate",
new ConfigDescription("DungeonPrefabs")));

            SafeArea = Synced(Config.Bind("Server config", "SafeArea", 1500,
new ConfigDescription("SafeArea")));

            WardChargeDurationInSec = Synced(Config.Bind("Server config", "WardChargeDurationInSec", 86400,
    new ConfigDescription("WardChargeDurationInSec")));


            WardLimit = Synced(Config.Bind("Server config", "WardLimit", 0,
    new ConfigDescription("Wards por jogador. 0 = sem limite (base e raidavel, entao nao precisa limitar).")));

            WardLimitVip = Synced(Config.Bind("Server config", "WardLimitVip", 0,
    new ConfigDescription("Wards por VIP. 0 = sem limite.")));

            WardRadius = Synced(Config.Bind("Server config", "WardRadius", 150,
new ConfigDescription("WardRadius")));
            WardRadius.SettingChanged += (_, __) => Wards.WardProfiles.ApplyRadii();

            BoatWindSpeedmultiplier = Synced(Config.Bind("Server config", "boatWindSpeedmultiplier", 1f,
new ConfigDescription("boatWindSpeedmultiplier")));

            SkillMultiplier = Synced(Config.Bind("Server config", "SkillMultiplier", 0.75f,
            new ConfigDescription("Multiplicador do ganho de skill. 1 = vanilla.")));

            ResetWorldDay = Synced(Config.Bind("Server config", "ResetWorldDay", false,
            new ConfigDescription("ResetWorldDay")));

            PortalMaterials = Synced(Config.Bind("Portal Mats", "PortalMaterials", "PortalToken:1,FineWood:50,GreydwarfEye:30,SurtlingCore:5",
    new ConfigDescription("Dynamic materials for the portal. Format: PrefabName:Amount,PrefabName:Amount")));

            CartographyTableAmount = Synced(Config.Bind("Server config", "CartographyTableAmount", 0,
    new ConfigDescription("Quantidade de cada material da mesa de cartografia. 0 = receita do jogo.")));

            EdgeOfWorldKills = Synced(Config.Bind("Server config", "EdgeOfWorldKills", true,
    new ConfigDescription("A borda do mundo mata, como no jogo. Desligado, quem cai da borda nao morre.")));

            MonsterScalingAlliesOnly = Synced(Config.Bind("Server config", "MonsterScalingAlliesOnly", true,
    new ConfigDescription("O bonus de vida e dano do monstro por jogador perto (vanilla e CreatureLevelControl) so conta quem luta e o grupo e a guilda dele. Desligado, conta todo mundo no raio, como no jogo.")));

            Pvp.PvpModule.Init(Config);
            Montarias.Bind(Config);
            Forja.Bind(Config);

            _harmony.PatchAll();
            Ajustes.AjustesMenu.Init(_harmony);
            DirectJoinFlow.Initialize(Logger);
            ClonedItems.LoadAssets();

            // Salvar o cfg com o servidor ligado ja vale: o arquivo e relido, o ServerSync
            // repassa aos clientes, e la o que e aplicado uma vez (custos, itens) roda de novo.
            Config.SettingChanged += (_, __) => _serverConfigApplied = false;
            Shared.ConfigWatcher.Watch(Config, "Deadheim");
        }        
    }
}
