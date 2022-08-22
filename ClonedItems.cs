using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Deadheim
{
    [HarmonyPatch]
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
            AddItemKits();
            PrefabManager.OnPrefabsRegistered -= AddClonedItems;
        }


        private static void AddClonedPieces()
        {
            AddAesirChest();
            AddAdminWards();
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

        private static void AddAdminWards()
        {
            AddBigdminWard();
            AddSmallAdminWard();
            AddRaidWard();
        }

        private static void AddItemKits()
        {
            AddArmorKit("ArmorKit1", "piece_chest_wood", "Basic Armor Kit I", "Kit de itens utilizados para fabricar armaduras de menor qualidade pertencente a era do bronze.", "Wood", "Guck", "armorkit1.png");
            AddArmorKit("ArmorKit2", "piece_chest_wood", "Good Armor Kit II", "Kit de itens utilizados para fabricar armaduras de refinadas de qualidade pertencente a era do ferro.", "Wood", "Blueberries", "armorkit2.png");
            AddArmorKit("ArmorKit3", "piece_chest_wood", "Great  Armor Kit III", "Kit de itens utilizados para fabricar armaduras reluzentes beirando a perfeição, sua qualidade pertence a era da prata.", "Wood", "Amber", "armorkit3.png");
            AddArmorKit("ArmorKit4", "piece_chest_wood", "Superior Armor Kit IV", "Kit de itens utilizados para fabricar armaduras de maior qualidade dentro os mortais beirando o divino pertencentes a era do linho.", "Wood", "Ruby", "armorkit4.png");
            AddArmorKit("WeaponKit1", "piece_chest_wood", "Basic Weapon Kit I", "Kit de itens utilizados para fabricar armas mais simples de qualidade duvidosa, muito utilizada na era do bronze.", "FineWood", "Guck", "weaponkit1.png");
            AddArmorKit("WeaponKit2", "piece_chest_wood", "Good Weapon Kit II", "Kit de itens utilizados para fabricar armas maior refinaria, muito utilizada na era do ferro.", "FineWood", "Blueberries", "weaponkit2.png");
            AddArmorKit("WeaponKit3", "piece_chest_wood", "Great Weapon Kit III", "Kit de itens utilizados para fabricar armas prateadas com brilhos que afligem os olhos, muito utilizada na era da prata.", "FineWood", "Amber", "weaponkit3.png");
            AddArmorKit("WeaponKit4", "piece_chest_wood", "Superior Weapon Kit IV", "Kit de itens utilizados para fabricar armas negras, extremamente laminadas capazes de perfurar a grossa pele de um Lox utilizada por aqueles que chegaram na era do metal negro.", "FineWood", "Ruby", "weaponkit4.png");

        }

        static T CopyComponent<T>(T original, GameObject destination) where T : Component
        {
            System.Type type = original.GetType();
            Component copy = destination.AddComponent(type);
            System.Reflection.FieldInfo[] fields = type.GetFields();
            foreach (System.Reflection.FieldInfo field in fields)
            {
                field.SetValue(copy, field.GetValue(original));
            }
            return copy as T;
        }

        private static void AddArmorKit(string prefabName, string prefabToCopy, string name, string description, string firstShaderPrefabName, string secondShaderPrefabName, string icon)
        {
            ItemDrop resin = PrefabManager.Instance.GetPrefab("Resin").GetComponent<ItemDrop>();

            GameObject clonedPrefab = PrefabManager.Instance.CreateClonedPrefab(prefabName, prefabToCopy);
            UnityEngine.Object.Destroy(clonedPrefab.GetComponent<Piece>());
            UnityEngine.Object.Destroy(clonedPrefab.GetComponent<Container>());
            UnityEngine.Object.Destroy(clonedPrefab.GetComponent<WearNTear>());
            UnityEngine.Object.Destroy(clonedPrefab.GetComponent<ZNetView>());

            clonedPrefab.AddComponent<ZNetView>();

            ItemDrop itemDrop = clonedPrefab.AddComponent<ItemDrop>();
            itemDrop.m_floating = clonedPrefab.AddComponent<Floating>();
            itemDrop.m_body = clonedPrefab.AddComponent<Rigidbody>();

            itemDrop.m_itemData.m_shared = new ItemDrop.ItemData.SharedData();
            itemDrop.m_itemData.m_shared.m_icons = new[] { Util.LoadSprite(icon, 64, 64) };
            itemDrop.m_itemData.m_shared.m_name = name;
            itemDrop.m_itemData.m_shared.m_description = description;
            itemDrop.m_itemData.m_shared.m_maxStackSize = 25;
            Vector3 newScale = clonedPrefab.transform.localScale;
            newScale.x *= 0.3f;
            newScale.y *= 0.3f;
            newScale.z *= 0.3f;
            clonedPrefab.transform.localScale = newScale;

            var comps = clonedPrefab.GetComponentsInChildren<MeshRenderer>();
            
            foreach (MeshRenderer comp in comps)
            {
                var materials = new List<Material>();

                if (comp == comps[0])
                {
                    materials.Add(PrefabManager.Instance.GetPrefab(firstShaderPrefabName).GetComponentInChildren<MeshRenderer>().materials[0]);
                } else
                {
                    materials.Add(PrefabManager.Instance.GetPrefab(secondShaderPrefabName).GetComponentInChildren<MeshRenderer>().materials[0]);
                }

                comp.materials = materials.ToArray();
            }

            clonedPrefab.GetComponent<ItemDrop>().m_itemData.m_dropPrefab = clonedPrefab;

            ItemManager.Instance.RegisterItemInObjectDB(clonedPrefab);                        
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.DropItem))]
        public static class DropItem
        {//shitcode to fix the shit shit that saying that de prefab is null
            [HarmonyPriority(Priority.First)]
            private static void Prefix(ItemDrop __instance, ItemDrop.ItemData item)
            {
                if (item.m_dropPrefab != null) return;
                if (item !=null  && item.m_shared != null && item.m_shared.m_name.Equals(" Basic Armor Kit I", StringComparison.CurrentCultureIgnoreCase)) item.m_dropPrefab = PrefabManager.Instance.GetPrefab("ArmorKit1");                
                if (item !=null  && item.m_shared != null && item.m_shared.m_name.Equals("Good Armor Kit II", StringComparison.CurrentCultureIgnoreCase)) item.m_dropPrefab = PrefabManager.Instance.GetPrefab("ArmorKit2");                
                if (item !=null  && item.m_shared != null && item.m_shared.m_name.Equals("Great  Armor Kit III", StringComparison.CurrentCultureIgnoreCase)) item.m_dropPrefab = PrefabManager.Instance.GetPrefab("ArmorKit3");                
                if (item !=null  && item.m_shared != null && item.m_shared.m_name.Equals("Superior Armor Kit IV", StringComparison.CurrentCultureIgnoreCase)) item.m_dropPrefab = PrefabManager.Instance.GetPrefab("ArmorKit4");                
                if (item !=null  && item.m_shared != null && item.m_shared.m_name.Equals("Basic Weapon Kit I", StringComparison.CurrentCultureIgnoreCase)) item.m_dropPrefab = PrefabManager.Instance.GetPrefab("WeaponKit1");                
                if (item !=null  && item.m_shared != null && item.m_shared.m_name.Equals("Good Weapon Kit II", StringComparison.CurrentCultureIgnoreCase)) item.m_dropPrefab = PrefabManager.Instance.GetPrefab("WeaponKit2");                
                if (item !=null  && item.m_shared != null && item.m_shared.m_name.Equals("Great Weapon Kit III", StringComparison.CurrentCultureIgnoreCase)) item.m_dropPrefab = PrefabManager.Instance.GetPrefab("WeaponKit3");                
                if (item !=null  && item.m_shared != null && item.m_shared.m_name.Equals("Superior Weapon Kit IV", StringComparison.CurrentCultureIgnoreCase)) item.m_dropPrefab = PrefabManager.Instance.GetPrefab("WeaponKit4");                
            }
        }

        private static void AddRaidWard()
        {
            var adminWard = PrefabManager.Instance.CreateClonedPrefab("RaidWard", "guard_stone");
            Piece piece = adminWard.GetComponent<Piece>();
            piece.m_resources[0].m_resItem = PrefabManager.Instance.GetPrefab("SwordCheat").GetComponent<ItemDrop>();
            piece.m_resources[0].m_recover = false;

            piece.m_description = "Raid Ward";
            piece.m_name = "RaidWard";

            PrivateArea area = piece.GetComponent<PrivateArea>();
            area.m_radius = 80;
            area.m_name = "RaidWard";

            var comp = adminWard.GetComponentInChildren<MeshRenderer>();

            var materials = new List<Material>();
            materials.Add(PrefabManager.Instance.GetPrefab("Stone").GetComponentInChildren<MeshRenderer>().materials[0]);
            materials.Add(PrefabManager.Instance.GetPrefab("Stone").GetComponentInChildren<MeshRenderer>().materials[0]);

            comp.materials = materials.ToArray();

            PieceManager.Instance.RegisterPieceInPieceTable(adminWard, "Hammer", "Misc");
        }

        private static void AddSmallAdminWard()
        {
            var adminWard = PrefabManager.Instance.CreateClonedPrefab("AdminWardSmall", "guard_stone");
            Piece piece = adminWard.GetComponent<Piece>();
            piece.m_resources[0].m_resItem = PrefabManager.Instance.GetPrefab("SwordCheat").GetComponent<ItemDrop>();
            piece.m_resources[0].m_recover = false;

            piece.m_description = "Admin Ward small";
            piece.m_name = "Admin Ward small";

            PrivateArea area = piece.GetComponent<PrivateArea>();
            area.m_radius = 40;
            area.m_name = "AdminWardSmall";

            var comp = adminWard.GetComponentInChildren<MeshRenderer>();

            var materials = new List<Material>();
            materials.Add(PrefabManager.Instance.GetPrefab("FreezeGland").GetComponentInChildren<MeshRenderer>().materials[0]);
            materials.Add(PrefabManager.Instance.GetPrefab("FreezeGland").GetComponentInChildren<MeshRenderer>().materials[0]);

            comp.materials = materials.ToArray();

            PieceManager.Instance.RegisterPieceInPieceTable(adminWard, "Hammer", "Misc");
        }

        private static void AddBigdminWard()
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

            UnityEngine.Object.Destroy(batzao.Prefab.GetComponent<Tameable>());
            UnityEngine.Object.Destroy(batzao.Prefab.GetComponent<Procreation>());
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
