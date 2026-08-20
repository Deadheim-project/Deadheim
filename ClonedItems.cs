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
        private sealed class NativeItemDefinition
        {
            public string PrefabName;
            public string Name;
            public string Description;
            public int MaxStack;
            public string FirstMaterialPrefab;
            public string SecondMaterialPrefab;
            public string Icon;
        }

        private static readonly NativeItemDefinition[] NativeItems =
        {
            new NativeItemDefinition { PrefabName = "PortalToken", Name = "Portal Token", Description = "Me compre para o Detalhes poder manter seu vício.", MaxStack = 10 },
            new NativeItemDefinition { PrefabName = "SpawnerToken", Name = "Spawner Token", Description = "Token used to build protected vanilla spawners.", MaxStack = 10 },
            new NativeItemDefinition { PrefabName = "ArmorKit1", Name = "Basic Armor Kit I", Description = "Kit de itens utilizados para fabricar armaduras de menor qualidade pertencente a era do bronze.", MaxStack = 25, FirstMaterialPrefab = "Wood", SecondMaterialPrefab = "Guck", Icon = "armorkit1.png" },
            new NativeItemDefinition { PrefabName = "ArmorKit2", Name = "Good Armor Kit II", Description = "Kit de itens utilizados para fabricar armaduras de refinadas de qualidade pertencente a era do ferro.", MaxStack = 25, FirstMaterialPrefab = "Wood", SecondMaterialPrefab = "Blueberries", Icon = "armorkit2.png" },
            new NativeItemDefinition { PrefabName = "ArmorKit3", Name = "Great Armor Kit III", Description = "Kit de itens utilizados para fabricar armaduras reluzentes beirando a perfeição, sua qualidade pertence a era da prata.", MaxStack = 25, FirstMaterialPrefab = "Wood", SecondMaterialPrefab = "Amber", Icon = "armorkit3.png" },
            new NativeItemDefinition { PrefabName = "ArmorKit4", Name = "Superior Armor Kit IV", Description = "Kit de itens utilizados para fabricar armaduras de maior qualidade dentro os mortais beirando o divino pertencentes a era do linho.", MaxStack = 25, FirstMaterialPrefab = "Wood", SecondMaterialPrefab = "Ruby", Icon = "armorkit4.png" },
            new NativeItemDefinition { PrefabName = "WeaponKit1", Name = "Basic Weapon Kit I", Description = "Kit de itens utilizados para fabricar armas mais simples de qualidade duvidosa, muito utilizada na era do bronze.", MaxStack = 25, FirstMaterialPrefab = "FineWood", SecondMaterialPrefab = "Guck", Icon = "weaponkit1.png" },
            new NativeItemDefinition { PrefabName = "WeaponKit2", Name = "Good Weapon Kit II", Description = "Kit de itens utilizados para fabricar armas maior refinaria, muito utilizada na era do ferro.", MaxStack = 25, FirstMaterialPrefab = "FineWood", SecondMaterialPrefab = "Blueberries", Icon = "weaponkit2.png" },
            new NativeItemDefinition { PrefabName = "WeaponKit3", Name = "Great Weapon Kit III", Description = "Kit de itens utilizados para fabricar armas prateadas com brilhos que afligem os olhos, muito utilizada na era da prata.", MaxStack = 25, FirstMaterialPrefab = "FineWood", SecondMaterialPrefab = "Amber", Icon = "weaponkit3.png" },
            new NativeItemDefinition { PrefabName = "WeaponKit4", Name = "Superior Weapon Kit IV", Description = "Kit de itens utilizados para fabricar armas negras, extremamente laminadas capazes de perfurar a grossa pele de um Lox utilizada por aqueles que chegaram na era do metal negro.", MaxStack = 25, FirstMaterialPrefab = "FineWood", SecondMaterialPrefab = "Ruby", Icon = "weaponkit4.png" }
        };

        private static readonly Dictionary<string, GameObject> RegisteredNativeItems = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private static readonly string[] NativeItemBasePrefabs = { "Thunderstone", "Coins", "Amber", "Wood" };
        private static readonly MethodInfo UpdateObjectDbRegisters = AccessTools.Method(typeof(ObjectDB), "UpdateRegisters");
        private static readonly MethodInfo MemberwiseCloneMethod = AccessTools.Method(typeof(object), "MemberwiseClone");
        private static readonly FieldInfo NamedPrefabs = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");
        private static bool ReportedObjectDbNotReady;

        public static void LoadAssets()
        {
            PieceManager.OnPiecesRegistered += AddClonedPieces;
            CreatureManager.OnVanillaCreaturesAvailable += AddVanillaClonedCreatures;
        }

        private static void RegisterNativeItems(ObjectDB objectDb)
        {
            if (objectDb == null || objectDb.m_items == null) return;
            GameObject basePrefab = FindNativeItemBasePrefab(objectDb);
            if (basePrefab == null)
            {
                if (!ReportedObjectDbNotReady)
                {
                    Debug.LogWarning("[Deadheim] ObjectDB ainda não possui um item-base válido; registro dos itens adiado.");
                    ReportedObjectDbNotReady = true;
                }
                return;
            }

            ReportedObjectDbNotReady = false;

            foreach (NativeItemDefinition definition in NativeItems)
            {
                GameObject existing = objectDb.m_items.FirstOrDefault(item => item != null && item.name == definition.PrefabName);
                if (existing != null)
                {
                    RegisteredNativeItems[definition.PrefabName] = existing;
                    continue;
                }

                try
                {
                    GameObject item = CreateNativeItem(objectDb, basePrefab, definition);
                    objectDb.m_items.Add(item);
                    RegisteredNativeItems[definition.PrefabName] = item;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Deadheim] Falha ao criar o item nativo '{definition.PrefabName}': {ex}");
                }
            }

            UpdateObjectDbRegisters?.Invoke(objectDb, null);

            // Se o ZNetScene já terminou seu Awake, atualiza também o dicionário
            // de rede. No fluxo normal ele ainda não existe e o prefixo abaixo
            // inclui os prefabs antes de o próprio Valheim montar o dicionário.
            if (ZNetScene.instance != null)
                AddItemsToZNetScene(ZNetScene.instance, updateNamedDictionary: true);
        }

        private static GameObject FindNativeItemBasePrefab(ObjectDB objectDb)
        {
            // Durante a primeira inicialização do servidor, os registros internos do
            // ObjectDB podem ainda não estar montados. A lista m_items, porém, já pode
            // conter os prefabs; por isso ela é consultada antes de GetItemPrefab.
            foreach (string prefabName in NativeItemBasePrefabs)
            {
                GameObject prefab = objectDb.m_items.FirstOrDefault(item =>
                    item != null && string.Equals(item.name, prefabName, StringComparison.Ordinal));
                if (prefab == null)
                    prefab = objectDb.GetItemPrefab(prefabName);

                ItemDrop itemDrop = prefab?.GetComponent<ItemDrop>();
                if (itemDrop?.m_itemData?.m_shared != null)
                    return prefab;
            }

            return null;
        }

        private static GameObject CreateNativeItem(ObjectDB objectDb, GameObject basePrefab, NativeItemDefinition definition)
        {
            GameObject item = UnityEngine.Object.Instantiate(basePrefab);
            item.name = definition.PrefabName;
            item.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(item);

            ItemDrop itemDrop = item.GetComponent<ItemDrop>();
            ItemDrop sourceDrop = basePrefab.GetComponent<ItemDrop>();
            if (itemDrop == null || sourceDrop?.m_itemData?.m_shared == null)
                throw new InvalidOperationException("O prefab base não possui dados válidos de ItemDrop.");

            itemDrop.m_itemData = sourceDrop.m_itemData.Clone();
            itemDrop.m_itemData.m_shared = (ItemDrop.ItemData.SharedData)MemberwiseCloneMethod.Invoke(
                sourceDrop.m_itemData.m_shared, null);
            itemDrop.m_itemData.m_shared.m_name = definition.Name;
            itemDrop.m_itemData.m_shared.m_description = definition.Description;
            itemDrop.m_itemData.m_shared.m_maxStackSize = definition.MaxStack;
            if (!string.IsNullOrWhiteSpace(definition.Icon))
                itemDrop.m_itemData.m_shared.m_icons = new[] { Util.LoadSprite(definition.Icon, 64, 64) };
            itemDrop.m_itemData.m_dropPrefab = item;

            ApplyMaterials(objectDb, item, definition.FirstMaterialPrefab, definition.SecondMaterialPrefab);
            return item;
        }

        private static void ApplyMaterials(ObjectDB objectDb, GameObject item, string firstPrefab, string secondPrefab)
        {
            if (string.IsNullOrWhiteSpace(firstPrefab) || string.IsNullOrWhiteSpace(secondPrefab)) return;
            Material first = GetFirstMaterial(objectDb, firstPrefab);
            Material second = GetFirstMaterial(objectDb, secondPrefab);
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material selected = i == 0 ? first : second;
                if (selected != null) renderers[i].sharedMaterial = selected;
            }
        }

        private static Material GetFirstMaterial(ObjectDB objectDb, string prefabName)
        {
            Renderer renderer = objectDb.GetItemPrefab(prefabName)?.GetComponentInChildren<Renderer>(true);
            return renderer?.sharedMaterials?.FirstOrDefault(material => material != null);
        }

        private static void AddItemsToZNetScene(ZNetScene scene, bool updateNamedDictionary)
        {
            if (scene == null) return;
            var named = updateNamedDictionary
                ? NamedPrefabs?.GetValue(scene) as Dictionary<int, GameObject>
                : null;

            foreach (GameObject item in RegisteredNativeItems.Values)
            {
                if (item == null) continue;
                List<GameObject> target = item.GetComponent<ZNetView>() != null ? scene.m_prefabs : scene.m_nonNetViewPrefabs;
                if (!target.Contains(item)) target.Add(item);
                if (named != null) named[item.name.GetStableHashCode()] = item;
            }
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        private static class ObjectDbAwakePatch
        {
            private static void Postfix(ObjectDB __instance) => RegisterNativeItems(__instance);
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static class ObjectDbCopyPatch
        {
            private static void Postfix(ObjectDB __instance) => RegisterNativeItems(__instance);
        }

        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static class ZNetSceneAwakePatch
        {
            private static void Prefix(ZNetScene __instance)
            {
                RegisterNativeItems(ObjectDB.instance);
                AddItemsToZNetScene(__instance, updateNamedDictionary: false);
            }
        }

        private static void AddClonedPieces()
        {
            AddAesirChest();
            AddAdminWards();
            AddBuildableSpawners();
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

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.DropItem))]
        public static class DropItem
        {
            [HarmonyPriority(Priority.First)]
            private static void Prefix(ItemDrop __instance, ItemDrop.ItemData item)
            {
                if (item == null || item.m_dropPrefab != null || item.m_shared == null) return;
                string prefabName = null;
                string itemName = item.m_shared.m_name;
                if (string.Equals(itemName, "Basic Armor Kit I", StringComparison.OrdinalIgnoreCase)) prefabName = "ArmorKit1";
                else if (string.Equals(itemName, "Good Armor Kit II", StringComparison.OrdinalIgnoreCase)) prefabName = "ArmorKit2";
                else if (string.Equals(itemName, "Great Armor Kit III", StringComparison.OrdinalIgnoreCase)) prefabName = "ArmorKit3";
                else if (string.Equals(itemName, "Superior Armor Kit IV", StringComparison.OrdinalIgnoreCase)) prefabName = "ArmorKit4";
                else if (string.Equals(itemName, "Basic Weapon Kit I", StringComparison.OrdinalIgnoreCase)) prefabName = "WeaponKit1";
                else if (string.Equals(itemName, "Good Weapon Kit II", StringComparison.OrdinalIgnoreCase)) prefabName = "WeaponKit2";
                else if (string.Equals(itemName, "Great Weapon Kit III", StringComparison.OrdinalIgnoreCase)) prefabName = "WeaponKit3";
                else if (string.Equals(itemName, "Superior Weapon Kit IV", StringComparison.OrdinalIgnoreCase)) prefabName = "WeaponKit4";

                if (prefabName != null)
                    item.m_dropPrefab = ObjectDB.instance?.GetItemPrefab(prefabName);
            }
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

        private static void AddBuildableSpawners()
        {
            AddBuildableSpawner("BuildableGreydwarfNestSpawner", "Spawner_GreydwarfNest", "Black Forest Spawner", "Indestructible Black Forest monster spawner.");
            AddBuildableSpawner("BuildableDraugrPileSpawner", "Spawner_DraugrPile", "Swamp Spawner", "Indestructible Swamp monster spawner.");
        }

        private static void AddBuildableSpawner(string prefabName, string sourcePrefabName, string name, string description)
        {
            GameObject source = PrefabManager.Instance.GetPrefab(sourcePrefabName);
            if (source == null)
            {
                Jotunn.Logger.LogWarning($"Could not create {name}. Missing vanilla prefab: {sourcePrefabName}");
                return;
            }

            GameObject spawner = PrefabManager.Instance.CreateClonedPrefab(prefabName, sourcePrefabName);
            Piece piece = spawner.GetComponent<Piece>() ?? spawner.AddComponent<Piece>();

            piece.m_name = name;
            piece.m_description = description;
            piece.m_enabled = true;
            piece.m_icon = GetSpawnerIcon(sourcePrefabName) ?? piece.m_icon;
            piece.m_category = Piece.PieceCategory.Misc;
            piece.m_groundPiece = true;
            piece.m_groundOnly = true;
            piece.m_clipGround = true;
            piece.m_noInWater = true;
            piece.m_canRotate = true;
            piece.m_canBeRemoved = true;
            piece.m_repairPiece = false;
            piece.m_resources = new[]
            {
                new Piece.Requirement
                {
                    m_resItem = ObjectDB.instance?.GetItemPrefab("SpawnerToken")?.GetComponent<ItemDrop>(),
                    m_amount = 1,
                    m_recover = true
                }
            };

            WearNTear wearNTear = spawner.GetComponent<WearNTear>();
            if (wearNTear != null)
            {
                wearNTear.m_health = 999999f;
                wearNTear.m_noRoofWear = true;
                wearNTear.m_noSupportWear = true;
            }

            Destructible destructible = spawner.GetComponent<Destructible>();
            if (destructible != null)
                destructible.m_health = 999999f;

            PieceManager.Instance.RegisterPieceInPieceTable(spawner, "Hammer", "Misc");
        }

        private static Sprite GetSpawnerIcon(string sourcePrefabName)
        {
            string iconItem = sourcePrefabName == "Spawner_GreydwarfNest" ? "GreydwarfEye" : "WitheredBone";
            Sprite icon = PrefabManager.Instance.GetPrefab(iconItem)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons?.FirstOrDefault();

            if (icon != null) return icon;

            return ObjectDB.instance?.GetItemPrefab("SpawnerToken")?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons?.FirstOrDefault();
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
