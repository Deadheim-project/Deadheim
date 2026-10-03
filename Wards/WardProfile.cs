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

        /// <summary>
        /// Ward de Territorio: fora da area segura do spawn, com combustivel e depois da ativacao, nada no
        /// raio toma dano de quem nao tem acesso (DamagePercent nao vale). So o dono remove. Tem limite e
        /// espacamento proprios.
        /// </summary>
        public bool Indestructible;

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

        /// <summary>Ward de Territorio (indestrutivel fora do spawn), construida com o TerritoryToken (doacao).</summary>
        public const string TerritoryWard = "DeadheimTerritoryWard";
        public const string TerritoryToken = "TerritoryToken";

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
                PrefabName = TerritoryWard,
                Fuel = true, CountsToLimit = false, GuildAccess = true, Protects = true, Indestructible = true,
                RadiusConfig = () => TerritoryWardRadius.Value,
            },
            new WardProfile
            {
                PrefabName = AdminWard,
                Fuel = false, CountsToLimit = false, GuildAccess = false, Protects = true,
                Radius = 50f,
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

        public static ConfigEntry<bool> TerritoryWardEnabled;
        public static ConfigEntry<int> TerritoryWardRadius;
        public static ConfigEntry<string> TerritoryWardCost;
        public static ConfigEntry<bool> TerritoryTokenRecover;
        public static ConfigEntry<int> TerritoryWardLimit;
        public static ConfigEntry<float> TerritoryWardActivationMinutes;
        public static ConfigEntry<float> TerritoryWardSpacing;

        private static GameObject _playerWardPrefab;
        private static GameObject _territoryWardPrefab;
        private static bool _registeredInHammer;
        private static bool _territoryRegisteredInHammer;

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

            // Dentro do SafeArea ([Server config], 1500 m do spawn) a ward comum ja protege 100%:
            // a Ward de Territorio e o que protege 100% fora dele.
            const string territory = "Wards - Territorio";
            TerritoryWardEnabled = Plugin.Synced(config.Bind(territory, "TerritoryWardEnabled", true,
                "Ward de Territorio no martelo. Desligado, as que ja existem viram ward comum (DamagePercent)."));
            TerritoryWardRadius = Plugin.Synced(config.Bind(territory, "TerritoryWardRadius", 20,
                "Raio da Ward de Territorio em metros. Tudo dentro dele fica indestrutivel."));
            TerritoryWardCost = Plugin.Synced(config.Bind(territory, "TerritoryWardCost", TerritoryToken + ":1,Stone:100,SurtlingCore:5",
                "Custo no formato Item:Quantidade. O " + TerritoryToken + " e vendido por doacao (Loja Deadcoins)."));
            TerritoryTokenRecover = Plugin.Synced(config.Bind(territory, "TerritoryTokenRecover", true,
                "O dono recebe o " + TerritoryToken + " de volta ao remover a ward. Desligado, mudar de lugar custa outro token."));
            TerritoryWardLimit = Plugin.Synced(config.Bind(territory, "TerritoryWardLimit", 1,
                "Wards de Territorio por jogador. 0 = sem limite."));
            TerritoryWardActivationMinutes = Plugin.Synced(config.Bind(territory, "TerritoryWardActivationMinutes", 60f,
                "Minutos (com o servidor ligado) ate a ward nova ficar indestrutivel. Ate la ela vale como ward comum. " +
                "E a recarga de mudar de lugar: impede plantar a ward no meio de um raid."));
            TerritoryWardSpacing = Plugin.Synced(config.Bind(territory, "TerritoryWardSpacing", 2f,
                "Distancia minima entre duas Wards de Territorio, de qualquer dono (guilda inclusive), em multiplos " +
                "do raio. 2 = os raios nao se sobrepoem. 0 desliga."));

            // O WardRadius e ligado depois, em Plugin.Awake, que assina o mesmo ApplyRadii.
            PlayerWardRadius.SettingChanged += (_, __) => ApplyRadii();
            TerritoryWardRadius.SettingChanged += (_, __) => ApplyRadii();
            PlayerWardCost.SettingChanged += (_, __) => ApplyRequirements(_playerWardPrefab, PlayerWardCost.Value);
            TerritoryWardCost.SettingChanged += (_, __) => ApplyRequirements(_territoryWardPrefab, TerritoryWardCost.Value);
            TerritoryTokenRecover.SettingChanged += (_, __) => ApplyRequirements(_territoryWardPrefab, TerritoryWardCost.Value);
        }

        public static bool IsTerritoryWard(GameObject go)
        {
            WardProfile profile = For(go);
            return profile != null && profile.Indestructible;
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
            Prefabs.ZNetSceneReady += CreateTerritoryWardPrefab;
            Prefabs.PiecesReady += RegisterTerritoryWardPiece;
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
                ApplyRequirements(_playerWardPrefab, PlayerWardCost.Value);
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

        /// <summary>
        /// Sempre criada, mesmo com TerritoryWardEnabled desligado: as que ja estao no mundo precisam
        /// do prefab para carregar. Desligado so tira do martelo e da indestrutibilidade.
        /// </summary>
        private static void CreateTerritoryWardPrefab()
        {
            Prefabs.ZNetSceneReady -= CreateTerritoryWardPrefab;
            if (_territoryWardPrefab != null) return;

            _territoryWardPrefab = Prefabs.Clone(TerritoryWard, VanillaWard);
            if (_territoryWardPrefab == null)
            {
                Debug.LogError("[Wards] guard_stone nao encontrado para clonar o " + TerritoryWard + ".");
                return;
            }

            PrivateArea area = _territoryWardPrefab.GetComponent<PrivateArea>();
            if (area != null)
            {
                area.m_radius = TerritoryWardRadius.Value;
                area.m_name = "Territorio";
                area.m_enabledByDefault = true;
            }

            Piece piece = _territoryWardPrefab.GetComponent<Piece>();
            if (piece != null)
            {
                piece.m_name = "Ward de Territorio";
                piece.m_description = "Fora da area segura do spawn, depois de ativada, nada no raio toma dano de quem nao tem " +
                                      "acesso. Uma por jogador; so o dono remove. Usa combustivel como as outras.";
                ApplyRequirements(_territoryWardPrefab, TerritoryWardCost.Value);
            }

            Debug.Log("[Wards] " + TerritoryWard + " criado: raio=" + TerritoryWardRadius.Value
                      + ", ativa em " + TerritoryWardActivationMinutes.Value + " min, limite=" + TerritoryWardLimit.Value + ".");
        }

        private static void RegisterTerritoryWardPiece()
        {
            if (_territoryRegisteredInHammer || !TerritoryWardEnabled.Value) return;
            if (_territoryWardPrefab == null) return;

            // O TerritoryToken nasce no ObjectDB (ClonedItems); aqui ele ja existe com certeza.
            ApplyRequirements(_territoryWardPrefab, TerritoryWardCost.Value);
            Pieces.AddToHammer(_territoryWardPrefab, "Misc");
            _territoryRegisteredInHammer = true;
            Prefabs.PiecesReady -= RegisterTerritoryWardPiece;
            Debug.Log("[Wards] " + TerritoryWard + " adicionado ao martelo.");
        }

        private static void ApplyRequirements(GameObject wardPrefab, string cost)
        {
            Piece piece = wardPrefab != null ? wardPrefab.GetComponent<Piece>() : null;
            if (piece == null || cost == null) return;

            List<Piece.Requirement> requirements = new List<Piece.Requirement>();
            foreach (string entry in cost.Split(','))
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
                // O token da base pode nao voltar ao remover (TerritoryTokenRecover): mudar a base custa outro.
                bool recover = itemName != TerritoryToken || TerritoryTokenRecover.Value;
                requirements.Add(new Piece.Requirement { m_resItem = item, m_amount = amount, m_recover = recover });
            }

            if (requirements.Count > 0) piece.m_resources = requirements.ToArray();
        }
    }
}
