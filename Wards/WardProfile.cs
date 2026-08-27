using BepInEx.Configuration;
using Jotunn.Managers;
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

        /// <summary>Raio fixo, ou -1 para seguir Plugin.WardRadius.</summary>
        public float Radius = -1f;

        public float ResolveRadius()
            => Radius > 0f ? Radius : Plugin.WardRadius.Value;
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

            PlayerWardEnabled = config.Bind(section, "PlayerWardEnabled", true,
                "Habilita o ward de protecao proprio (DeadheimWard), separado do guard_stone.");
            PlayerWardRadius = config.Bind(section, "PlayerWardRadius", 32,
                "Raio do DeadheimWard em metros.");
            PlayerWardCost = config.Bind(section, "PlayerWardCost", "Stone:100,SurtlingCore:5",
                "Custo do DeadheimWard no formato Item:Quantidade,Item:Quantidade.");
            DamagePercent = config.Bind(section, "DamagePercent", 0f,
                new ConfigDescription(
                    "Percentual de dano que o ward e tudo que ele cobre recebem. 0 = invulneravel, 100 = dano normal.",
                    new AcceptableValueRange<float>(0f, 100f)));
            Spacing = config.Bind(section, "Spacing", 3f,
                "Distancia minima de um ward alheio, em multiplos do raio. 0 desliga a checagem.");
            FuelItem = config.Bind(section, "FuelItem", "GreydwarfEye",
                "Prefab do item usado para abastecer o ward.");
            MaxCharges = config.Bind(section, "MaxCharges", 10,
                "Maximo de cargas de combustivel que um ward guarda.");
            GuildAccessEnabled = config.Bind(section, "GuildAccess", true,
                "Membros da guild do dono tem acesso automatico ao ward.");
            ProtectTerrain = config.Bind(section, "ProtectTerrain", true,
                "Bloqueia picareta, hoe e cultivador dentro do ward.");
            ProtectPortals = config.Bind(section, "ProtectPortals", true,
                "Bloqueia renomear portais dentro do ward.");
            ProtectPlants = config.Bind(section, "ProtectPlants", true,
                "Bloqueia colher e destruir plantacao dentro do ward.");
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
            PrefabManager.OnPrefabsRegistered += CreatePlayerWardPrefab;
            PieceManager.OnPiecesRegistered += RegisterPlayerWardPiece;
        }

        private static void CreatePlayerWardPrefab()
        {
            PrefabManager.OnPrefabsRegistered -= CreatePlayerWardPrefab;

            if (!PlayerWardEnabled.Value || _playerWardPrefab != null) return;

            _playerWardPrefab = PrefabManager.Instance.CreateClonedPrefab(PlayerWard, VanillaWard);
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

            PieceManager.Instance.RegisterPieceInPieceTable(_playerWardPrefab, "Hammer", "Misc");
            _registeredInHammer = true;
            PieceManager.OnPiecesRegistered -= RegisterPlayerWardPiece;
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
                GameObject prefab = PrefabManager.Instance.GetPrefab(itemName);
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
