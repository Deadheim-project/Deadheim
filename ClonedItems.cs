using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim
{
    public class ClonedItems
    {
        public static void LoadAssets()
        {
            PrefabManager.OnPrefabsRegistered += AddClonedItems;
            PieceManager.OnPiecesRegistered += AddClonedPieces;
            CreatureManager.OnVanillaCreaturesAvailable += AddVanillaClonedCreatures;
        }

        private static void AddClonedItems()
        {
            AddPortalToken();
            AddEsqueletaoItems();
            PrefabManager.OnPrefabsRegistered -= AddClonedItems;
        }

        private static void AddClonedPieces()
        {
            AddAesirChest();
            AddAdminWard();
            PieceManager.OnPiecesRegistered -= AddClonedPieces;
        }

        private static void AddVanillaClonedCreatures()
        {
            AddBatzao();
            AddNomTameableWolf();
            AddPorcoLox();
            AddSkeletao();
            CreatureManager.OnVanillaCreaturesAvailable -= AddVanillaClonedCreatures;
        }

        private static void AddAdminWard()
        {
            var adminWard = PrefabManager.Instance.CreateClonedPrefab("AdminWard", "guard_stone");
            Piece piece = adminWard.GetComponent<Piece>();
            piece.m_resources[0].m_resItem = PrefabManager.Instance.GetPrefab("SwordCheat").GetComponent<ItemDrop>();
            piece.m_resources[0].m_recover = false;

            piece.m_description = "Admin Ward";
            piece.m_name = "Admin Ward";

            PrivateArea area = piece.GetComponent<PrivateArea>();
            area.m_radius = 150;
            area.m_name = "AdminWard";


            var comp = adminWard.GetComponentInChildren<MeshRenderer>();

            var materials = new List<Material>();
            materials.Add(PrefabManager.Instance.GetPrefab("Tar").GetComponentInChildren<MeshRenderer>().materials[0]);
            materials.Add(PrefabManager.Instance.GetPrefab("SurtlingCore").GetComponentInChildren<MeshRenderer>().materials[0]);

            comp.materials = materials.ToArray();

            PieceManager.Instance.RegisterPieceInPieceTable(adminWard, "Hammer", "Misc");
        }


        private static void AddAesirChest()
        {
            var aesirChest = PrefabManager.Instance.CreateClonedPrefab("AesirChest", "piece_chest_private");
            Piece piece = aesirChest.GetComponent<Piece>();
            piece.m_resources[0].m_resItem = PrefabManager.Instance.GetPrefab("Bronze").GetComponent<ItemDrop>();
            piece.m_resources[1].m_resItem = PrefabManager.Instance.GetPrefab("Wood").GetComponent<ItemDrop>();

            piece.m_description = "Aesir Chest";
            piece.m_name = "Aesir Chest";

            PieceManager.Instance.RegisterPieceInPieceTable(aesirChest, "Hammer", "Furniture");
        }

        private static void AddEsqueletaoItems()
        {
            CustomItem skeletaoSword = new CustomItem("SkeletaoSword", "SwordBronze", new ItemConfig
            {
                Name = "Espadada do esqueletão.",
                Description = "Espadada do esqueletão.",
                Icons = new[] { Util.LoadSprite("esqueletaosword.png", 64, 64) },
                RepairStation = "forge",
                MinStationLevel = 1,
                CraftingStation = "forge"       
            });

            skeletaoSword.ItemDrop.m_itemData.m_shared.m_damages.m_slash = 56;
            skeletaoSword.ItemPrefab.GetComponentInChildren<MeshRenderer>().materials = PrefabManager.Instance.GetPrefab("Tar").GetComponentInChildren<MeshRenderer>().materials;

            CustomItem skeletaoShield = new CustomItem("SkeletaoShield", "ShieldBoneTower", new ItemConfig
            {
                Name = "Escudo do esqueletão.",
                Description = "Escudo do esqueletão.",
                Icons = new[] { Util.LoadSprite("esqueletaoshield.png", 64, 64) },
                RepairStation = "forge",
                MinStationLevel = 1,
                CraftingStation = "forge"
            });

            skeletaoShield.ItemDrop.m_itemData.m_shared.m_blockPower = 49;
            skeletaoShield.ItemPrefab.GetComponentInChildren<MeshRenderer>().materials = PrefabManager.Instance.GetPrefab("Tar").GetComponentInChildren<MeshRenderer>().materials;

            ItemManager.Instance.AddItem(skeletaoSword);
            ItemManager.Instance.AddItem(skeletaoShield);
        }

        private static void AddPortalToken()
        {
            CustomItem CI = new CustomItem("PortalToken", "Thunderstone");
            ItemDrop itemDrop = CI.ItemDrop;
            itemDrop.m_itemData.m_shared.m_name = "Portal Token";
            itemDrop.m_itemData.m_shared.m_description = "Use to help deadheim keep going";
            itemDrop.m_itemData.m_shared.m_maxStackSize = 10;
            ItemManager.Instance.AddItem(CI);
        }

        private static void AddNomTameableWolf()
        {
            var batzao = new CustomCreature("LoboNaoDomavel", "Wolf",
                new Jotunn.Configs.CreatureConfig
                {

                });

            var humanoid = batzao.Prefab.GetComponent<Humanoid>();
            humanoid.m_name = "Lobo nao domavel";
            CreatureManager.Instance.AddCreature(batzao);

            Object.Destroy(batzao.Prefab.GetComponent<Tameable>());
            Object.Destroy(batzao.Prefab.GetComponent<Procreation>());
        }

        private static void AddSkeletao()
        {
            var skeletao = new CustomCreature("Skeletao", "Skeleton",
            new Jotunn.Configs.CreatureConfig
            {
                Faction = Character.Faction.Undead
            });

            var humanoid = skeletao.Prefab.GetComponent<Humanoid>();

            humanoid.m_name = "Esqueletão";
            humanoid.m_boss = true;
            humanoid.m_health = 500;
            var renderers = skeletao.Prefab.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var renderer in renderers)
            {
                renderer.material.color = Color.black;
                renderer.sharedMaterial.color = Color.black;
            }

            Vector3 newScale = skeletao.Prefab.transform.localScale;
            newScale.x *= 1.3f;
            newScale.y *= 1.3f;
            newScale.z *= 1.3f;
            skeletao.Prefab.transform.localScale = newScale;

            CreatureManager.Instance.AddCreature(skeletao);

            UnityEngine.Object.Destroy(skeletao.Prefab.GetComponent<Tameable>());
            UnityEngine.Object.Destroy(skeletao.Prefab.GetComponent<Procreation>());
        }

        private static void AddPorcoLox()
        {
            var porcoLox = new CustomCreature("PorcoLox", "Lox",
            new Jotunn.Configs.CreatureConfig
            {
                Faction = Character.Faction.ForestMonsters
            });

            var humanoid = porcoLox.Prefab.GetComponent<Humanoid>();

            var ragdoll = PrefabManager.Instance.CreateClonedPrefab("PorcoLoxRagDoll", "lox_ragdoll");

            humanoid.m_name = "PorcoLox";
            humanoid.m_boss = true;
            humanoid.m_health = 300;

            ColorRenderers(porcoLox.Prefab, Color.black);

            Vector3 newScale = porcoLox.Prefab.transform.localScale;
            newScale.x *= 0.5f;
            newScale.y *= 0.5f;
            newScale.z *= 0.5f;
            ragdoll.transform.localScale = newScale;
            int idx = humanoid.m_deathEffects.m_effectPrefabs.ToList().FindIndex(x => x.m_prefab.name == "lox_ragdoll");

            porcoLox.Prefab.GetComponent<Humanoid>().m_deathEffects.m_effectPrefabs.ToList()[idx].m_prefab = ragdoll;

            ColorRenderers(ragdoll, Color.black);

            porcoLox.Prefab.transform.localScale = newScale;

            CreatureManager.Instance.AddCreature(porcoLox);

            UnityEngine.Object.Destroy(porcoLox.Prefab.GetComponent<Tameable>());
            UnityEngine.Object.Destroy(porcoLox.Prefab.GetComponent<Procreation>());
        }

        public static void ColorRenderers(GameObject gameObject, Color color)
        {
            var renderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var renderer in renderers)
            {
                renderer.material.color = color;
                renderer.sharedMaterial.color = color;
            }

        }

        private static void AddBatzao()
        {
            var batzao = new CustomCreature("Morcegao", "Bat",
                new Jotunn.Configs.CreatureConfig
                {
                    DropConfigs = new[]
                    {
                        new DropConfig
                        {
                            Item = "Coins",
                            Chance = 100,
                            MinAmount = 50,
                            MaxAmount = 100,
                            OnePerPlayer = false,
                            LevelMultiplier = false
                        }
                    },
                    Faction = Character.Faction.Undead
                });

            Vector3 newScale = batzao.Prefab.transform.localScale;
            newScale.x *= 3;
            newScale.y *= 3;
            newScale.z *= 3;
            batzao.Prefab.transform.localScale = newScale;

            var humanoid = batzao.Prefab.GetComponent<Humanoid>();
            humanoid.m_name = "Morcegão";
            humanoid.m_health = 500;
            humanoid.m_boss = true;


            CreatureManager.Instance.AddCreature(batzao);
        }
    }
}
