using BepInEx.Configuration;
using Deadheim.Vanilla;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim.Wards
{
    /// <summary>Como um prefab de ward se comporta. Um perfil por tipo de ward.</summary>
    public sealed class WardProfile
    {
        public string PrefabName;
        public bool Fuel;
        public bool CountsToLimit;
        public bool GuildAccess;
        public bool Protects;

        /// <summary>Raio fixo, ou -1 para seguir o cfg (RadiusConfig, ou Plugin.WardRadius).</summary>
        public float Radius = -1f;

        /// <summary>Config que da o raio deste tipo de ward; null = Plugin.WardRadius.</summary>
        public Func<int> RadiusConfig;

        public float ResolveRadius()
        {
            if (Radius > 0f) return Radius;
            return RadiusConfig != null ? RadiusConfig() : Plugin.WardRadius.Value;
        }
    }

    /// <summary>
    /// Registro unico de quais prefabs sao wards e como cada um se comporta.
    ///
    /// Antes esta informacao estava espalhada por Ward.cs, ItemService.SetWardFirePlace,
    /// CraftingStations.PrivateAreaAwake e PlayerWard.cs do RaidSystem, cada um com sua
    /// propria checagem de nome. Agora e so aqui.
    /// </summary>
    public static class WardProfiles
    {
        public const string VanillaWard = "guard_stone";
        public const string PlayerWard = "DeadheimWard";
        public const string AdminWard = "AdminWard";
        public const string AdminWardSmall = "AdminWardSmall";

        /// <summary>Raios dos wards de admin. Um lugar so: o clone (ClonedItems) e o Awake leem daqui.</summary>
        public const float AdminWardRadius = 50f;
        public const float AdminWardSmallRadius = 40f;

        /// <summary>
        /// O RaidWard territorial e do RaidSystem e nunca entra aqui. O RaidSystem tem
        /// suas proprias regras de horario, HP e conquista para ele.
        /// </summary>
        public const string RaidWard = "RaidWard";

        private static readonly List<WardProfile> _profiles = new List<WardProfile>
        {
            new WardProfile
            {
                PrefabName = VanillaWard,
                Fuel = true, CountsToLimit = true, GuildAccess = true, Protects = true,
            },
            new WardProfile
            {
                PrefabName = PlayerWard,
                Fuel = true, CountsToLimit = true, GuildAccess = true, Protects = true,
                // Sem isto o Awake aplicava o WardRadius (150) e o PlayerWardRadius nao valia.
                RadiusConfig = () => PlayerWardRadius.Value,
            },
            new WardProfile
            {
                PrefabName = AdminWard,
                Fuel = false, CountsToLimit = false, GuildAccess = false, Protects = true,
                // O clone punha 150 e o Awake trocava por 50 ao nascer; vale o que estava no ar.
                Radius = AdminWardRadius,
            },
            new WardProfile
            {
                // Sem perfil o AdminWardSmall so tinha a protecao do vanilla (construir e abrir bau):
                // nao segurava dano, terreno nem natureza.
                PrefabName = AdminWardSmall,
                Fuel = false, CountsToLimit = false, GuildAccess = false, Protects = true,
                Radius = AdminWardSmallRadius,
            },
        };

        // ------------------------------------------------------------------ config

        public static ConfigEntry<bool> PlayerWardEnabled;
        public static ConfigEntry<int> PlayerWardRadius;
        public static ConfigEntry<string> PlayerWardCost;
        public static ConfigEntry<float> DamagePercent;
        public static ConfigEntry<float> Spacing;
        public static ConfigEntry<string> FuelItem;
        public static ConfigEntry<int> MaxCharges;
        public static ConfigEntry<bool> GuildAccessEnabled;
        public static ConfigEntry<bool> ProtectTerrain;
        public static ConfigEntry<bool> ProtectPortals;
        public static ConfigEntry<bool> ProtectPlants;
        public static ConfigEntry<bool> ProtectNature;
        public static ConfigEntry<float> ProtectNatureRadius;
        public static ConfigEntry<bool> ProtectNatureOres;
        public static ConfigEntry<string> NatureOreDrops;

        private static GameObject _playerWardPrefab;
        private static bool _registeredInHammer;

        /// <summary>
        /// Configs novas so. WardRadius, WardLimit, WardLimitVip, WardChargeDurationInSec,
        /// SafeArea, DungeonPrefabs e Vip continuam onde sempre estiveram, em Plugin.cs,
        /// para nao resetar o que ja esta configurado no servidor.
        /// </summary>
        public static void BindConfigs(ConfigFile config)
        {
            const string section = "Wards";

            // Tudo sincronizado (ServerSync): e o servidor que decide o que o ward protege.
            PlayerWardEnabled = Plugin.Synced(config.Bind(section, "PlayerWardEnabled", true,
                "Habilita o ward de protecao proprio (DeadheimWard), separado do guard_stone."));
            PlayerWardRadius = Plugin.Synced(config.Bind(section, "PlayerWardRadius", 32,
                "Raio do DeadheimWard em metros."));
            PlayerWardCost = Plugin.Synced(config.Bind(section, "PlayerWardCost", "Stone:100,SurtlingCore:5",
                "Custo do DeadheimWard no formato Item:Quantidade,Item:Quantidade."));
            DamagePercent = Plugin.Synced(config.Bind(section, "DamagePercent", 25f,
                new ConfigDescription(
                    "Percentual de dano que o ward e tudo que ele cobre recebem FORA da zona segura (a base e raidavel). " +
                    "0 = invulneravel, 100 = dano normal. Na zona segura (SafeArea, ilha inicial, SafeZones) e sempre 0.",
                    new AcceptableValueRange<float>(0f, 100f))));
            Spacing = Plugin.Synced(config.Bind(section, "Spacing", 3f,
                "Distancia minima de um ward alheio, em multiplos do raio. 0 desliga a checagem."));
            FuelItem = Plugin.Synced(config.Bind(section, "FuelItem", "Resin",
                "Prefab do item usado para abastecer o ward."));
            MaxCharges = Plugin.Synced(config.Bind(section, "MaxCharges", 10,
                "Maximo de cargas de combustivel que um ward guarda."));
            GuildAccessEnabled = Plugin.Synced(config.Bind(section, "GuildAccess", true,
                "Membros da guild do dono tem acesso automatico ao ward."));
            ProtectTerrain = Plugin.Synced(config.Bind(section, "ProtectTerrain", true,
                "Bloqueia picareta, hoe e cultivador dentro do ward."));
            ProtectPortals = Plugin.Synced(config.Bind(section, "ProtectPortals", true,
                "Bloqueia renomear portais dentro do ward."));
            ProtectPlants = Plugin.Synced(config.Bind(section, "ProtectPlants", true,
                "Bloqueia colher e destruir plantacao dentro do ward."));
            ProtectNature = Plugin.Synced(config.Bind(section, "ProtectNature", true,
                "Bloqueia quebrar pedra, arvore, tronco e toco dentro do ward de outro jogador."));
            ProtectNatureRadius = Plugin.Synced(config.Bind(section, "ProtectNatureRadius", 0f,
                "Pedra e arvore so ficam protegidas ate esta distancia do ward, em metros. 0 = o ward inteiro. " +
                "Serve para proteger a base (arvore caindo, pedra no caminho) sem trancar o raio todo."));
            ProtectNatureOres = Plugin.Synced(config.Bind(section, "ProtectNatureOres", false,
                "Protege tambem os veios de minerio (os que soltam um item de NatureOreDrops). Desligado: " +
                "ward nao tranca minerio, senao uma guilda fecharia os veios do mapa com wards."));
            NatureOreDrops = Plugin.Synced(config.Bind(section, "NatureOreDrops",
                "CopperOre,TinOre,IronScrap,SilverOre,Obsidian,BlackMarble,FlametalOre,FlametalOreNew,CopperScrap,BronzeScrap,Grausten,SoftTissue",
                "Itens que definem um veio de minerio para ProtectNatureOres."));

            // O WardRadius e ligado depois, em Plugin.Awake, que assina o mesmo ApplyRadii.
            PlayerWardRadius.SettingChanged += (_, __) => ApplyRadii();
            PlayerWardCost.SettingChanged += (_, __) =>
            {
                Piece piece = _playerWardPrefab != null ? _playerWardPrefab.GetComponent<Piece>() : null;
                if (piece != null) ApplyRequirements(piece);
            };
        }

        /// <summary>Raio novo no cfg vale tambem para os wards ja construidos.</summary>
        public static void ApplyRadii()
        {
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                WardProfile profile = For(area);
                if (profile == null) continue;
                float radius = profile.ResolveRadius();
                area.m_radius = radius;
                if (area.m_areaMarker != null) area.m_areaMarker.m_radius = radius;
            }
        }

        // ------------------------------------------------------------- identificacao

        /// <summary>Nome do prefab sem o sufixo de instancia.</summary>
        public static string CleanName(GameObject go)
        {
            if (go == null) return string.Empty;
            string name = go.name;
            int clone = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return clone >= 0 ? name.Substring(0, clone) : name;
        }

        public static WardProfile For(GameObject go)
        {
            if (go == null) return null;

            string name = CleanName(go);
            // Precisa ser exato: "DeadheimWard" nao pode casar com "RaidWard" e vice-versa.
            for (int i = 0; i < _profiles.Count; i++)
                if (string.Equals(_profiles[i].PrefabName, name, StringComparison.Ordinal))
                    return _profiles[i];

            return null;
        }

        public static WardProfile For(PrivateArea area)
            => area != null ? For(area.gameObject) : null;

        public static bool IsWard(GameObject go) => For(go) != null;

        public static bool IsRaidWard(GameObject go)
            => go != null && CleanName(go).StartsWith(RaidWard, StringComparison.Ordinal);

        // ------------------------------------------------------------------ prefab

        public static void LoadAssets()
        {
            Prefabs.ZNetSceneReady += CreatePlayerWardPrefab;
            Prefabs.PiecesReady += RegisterPlayerWardPiece;
        }

        private static void CreatePlayerWardPrefab()
        {
            Prefabs.ZNetSceneReady -= CreatePlayerWardPrefab;

            if (!PlayerWardEnabled.Value || _playerWardPrefab != null) return;

            _playerWardPrefab = Prefabs.Clone(PlayerWard, VanillaWard);
            if (_playerWardPrefab == null)
            {
                Debug.LogError("[Wards] guard_stone nao encontrado para clonar o " + PlayerWard + ".");
                return;
            }

            PrivateArea area = _playerWardPrefab.GetComponent<PrivateArea>();
            if (area != null)
            {
                area.m_radius = PlayerWardRadius.Value;
                area.m_name = "Ward";
                area.m_enabledByDefault = true;
            }

            Piece piece = _playerWardPrefab.GetComponent<Piece>();
            if (piece != null)
            {
                piece.m_name = "Ward de Protecao";
                piece.m_description = "Protege a area contra dano, terraformacao e roubo de plantacao. Membros da guild tem acesso automatico.";
                ApplyRequirements(piece);
            }

            Debug.Log("[Wards] " + PlayerWard + " criado: raio=" + PlayerWardRadius.Value
                      + ", dano=" + DamagePercent.Value + "%.");
        }

        private static void RegisterPlayerWardPiece()
        {
            if (_registeredInHammer || !PlayerWardEnabled.Value) return;
            if (_playerWardPrefab == null) return;

            Pieces.AddToHammer(_playerWardPrefab, "Misc");
            _registeredInHammer = true;
            Prefabs.PiecesReady -= RegisterPlayerWardPiece;
            Debug.Log("[Wards] " + PlayerWard + " adicionado ao martelo.");
        }

        private static void ApplyRequirements(Piece piece)
        {
            List<Piece.Requirement> requirements = new List<Piece.Requirement>();
            foreach (string entry in PlayerWardCost.Value.Split(','))
            {
                string[] parts = entry.Split(':');
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[1].Trim(), out int amount) || amount <= 0) continue;

                string itemName = parts[0].Trim();
                GameObject prefab = Prefabs.Get(itemName);
                ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (item == null)
                {
                    Debug.LogWarning("[Wards] Item de custo nao encontrado: " + itemName);
                    continue;
                }
                requirements.Add(new Piece.Requirement { m_resItem = item, m_amount = amount, m_recover = true });
            }

            if (requirements.Count > 0) piece.m_resources = requirements.ToArray();
        }
    }
}
