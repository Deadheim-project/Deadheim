using HarmonyLib;
using Deadheim.Vanilla;
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
            // Valor de venda no mercador; null = o do item-base.
            public int? Value;
            // No chao vira uma moeda com a arte do Icon, em vez do Thunderstone de onde o item e clonado.
            public bool Coin;
            // Moeda grossa, com borda de ouro, em vez do disco chato.
            public bool ThickCoin;
            // Item-base proprio; null (ou ausente no jogo) = o comum, NativeItemBasePrefabs.
            public string BasePrefab;
            // Peso; null = o do item-base.
            public float? Weight;
        }

        private static readonly NativeItemDefinition[] NativeItems =
        {
            new NativeItemDefinition { PrefabName = "PortalToken", Name = "Portal Token", Description = "Me compre para o Detalhes poder manter seu vício.", MaxStack = 10, Icon = "portaltoken.png", Coin = true },
            new NativeItemDefinition { PrefabName = "SpawnerToken", Name = "Spawner Token", Description = "Token used to build protected vanilla spawners.", MaxStack = 10, Icon = "spawnertoken.png", Coin = true },
            // Vendido por doacao (Loja Deadcoins): nao pode virar moeda no mercador.
            new NativeItemDefinition { PrefabName = Wards.WardProfiles.TerritoryToken, Name = "Territory Token", Description = "Constrói a Ward de Território. Fora da área segura do spawn, depois de ativada, nada no raio dela toma dano de quem não tem acesso. Uma por jogador.", MaxStack = 10, Value = 0, Icon = "territorytoken.png", Coin = true },
            // Ainda sem uso: nenhuma construcao custa Dead Token. Nao vira moeda no mercador. Clonado
            // das moedas, nao do Thunderstone: no chao e uma moeda grossa de verdade.
            new NativeItemDefinition { PrefabName = DeadToken.Prefab, Name = DeadToken.Nome, Description = "Moeda do Deadheim.", MaxStack = 100, Value = 0, Weight = 0.1f, Icon = "deadtoken.png", Coin = true, ThickCoin = true, BasePrefab = "Coins" },
            // Comprada com Deadcoins: nao pode virar moeda no mercador.
            new NativeItemDefinition { PrefabName = Forja.GarantiaPrefab, Name = Forja.GarantiaNome, Description = "Na Forja de Potencial, garante o sucesso do refino: o item sobe de nível em vez de quebrar. Gasta junto com o ídolo, a cada tentativa.", MaxStack = 50, Icon = "garantiarefino.png", Value = 0, Coin = true },
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

        private static readonly HashSet<string> NativeItemNames =
            new HashSet<string>(NativeItems.Select(definition => definition.PrefabName), StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Item criado aqui (Dead Token, tokens antigos, Garantia de Refino, kits). Sao clones de um
        /// item do jogo (Thunderstone; o Dead Token das moedas), e a maioria e comprada: a carga da
        /// morte por jogador nunca os leva.
        /// </summary>
        internal static bool IsNativeItem(string prefabName)
            => !string.IsNullOrEmpty(prefabName) && NativeItemNames.Contains(prefabName);
        private static readonly string[] NativeItemBasePrefabs = { "Thunderstone", "Coins", "Amber", "Wood" };
        private static readonly MethodInfo UpdateObjectDbRegisters = AccessTools.Method(typeof(ObjectDB), "UpdateRegisters");
        private static readonly MethodInfo MemberwiseCloneMethod = AccessTools.Method(typeof(object), "MemberwiseClone");
        private static readonly FieldInfo NamedPrefabs = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");
        private static bool ReportedObjectDbNotReady;

        public static void LoadAssets()
        {
            Prefabs.PiecesReady += AddClonedPieces;
            Prefabs.ZNetSceneReady += AddVanillaClonedCreatures;
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
                    GameObject source = (definition.BasePrefab != null ? ValidBasePrefab(objectDb, definition.BasePrefab) : null) ?? basePrefab;
                    GameObject item = CreateNativeItem(objectDb, source, definition);
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
                GameObject prefab = ValidBasePrefab(objectDb, prefabName);
                if (prefab != null) return prefab;
            }

            return null;
        }

        private static GameObject ValidBasePrefab(ObjectDB objectDb, string prefabName)
        {
            GameObject prefab = objectDb.m_items.FirstOrDefault(item =>
                item != null && string.Equals(item.name, prefabName, StringComparison.Ordinal));
            if (prefab == null)
                prefab = objectDb.GetItemPrefab(prefabName);

            ItemDrop itemDrop = prefab?.GetComponent<ItemDrop>();
            return itemDrop?.m_itemData?.m_shared != null ? prefab : null;
        }

        private static GameObject _templates;

        /// <summary>
        /// Pai inativo dos itens nativos: o Awake nao roda no modelo guardado, mas o modelo em si fica
        /// ativo, entao o que o jogo instancia dele (item no chao, item vindo da rede) nasce ligado.
        /// Antes o proprio modelo era SetActive(false): todo item instanciado dele nascia desligado, sem
        /// Awake nem ZDO, invisivel e fora da rede. Soltar um Portal Token, uma Garantia ou um kit no
        /// chao (ou o kit cair como carga na morte) sumia com o item.
        /// </summary>
        private static Transform Templates
        {
            get
            {
                if (_templates) return _templates.transform;
                _templates = new GameObject("DeadheimNativeItems");
                _templates.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_templates);
                return _templates.transform;
            }
        }

        private static GameObject CreateNativeItem(ObjectDB objectDb, GameObject basePrefab, NativeItemDefinition definition)
        {
            GameObject item = UnityEngine.Object.Instantiate(basePrefab, Templates, false);
            item.name = definition.PrefabName;

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
            Texture2D art = string.IsNullOrWhiteSpace(definition.Icon) ? null : Util.LoadTexture(definition.Icon);
            if (art != null)
                itemDrop.m_itemData.m_shared.m_icons = new[] { Util.SpriteOf(art) };
            if (definition.Value.HasValue)
                itemDrop.m_itemData.m_shared.m_value = definition.Value.Value;
            if (definition.Weight.HasValue)
                itemDrop.m_itemData.m_shared.m_weight = definition.Weight.Value;
            itemDrop.m_itemData.m_dropPrefab = item;

            ApplyMaterials(objectDb, item, definition.FirstMaterialPrefab, definition.SecondMaterialPrefab);
            if (definition.Coin && art != null) ApplyCoinVisual(item, art, definition.ThickCoin);
            return item;
        }

        /// <summary>Diametro da moeda no chao, em metros.</summary>
        private const float CoinSize = 0.3f;

        /// <summary>Espessura da moeda grossa, em metros.</summary>
        private const float ThickCoinHeight = 0.03f;

        /// <summary>Discos da borda da moeda grossa: com poucos, de perto ela parece uma pilha.</summary>
        private const int ThickCoinLayers = 10;

        /// <summary>Cor da borda embaixo; sobe ate o ouro cheio no alto (a luz vem de cima).</summary>
        private static readonly Color ThickCoinEdgeDark = new Color(0.45f, 0.4f, 0.36f);

        private static Texture2D _coinEdge;

        /// <summary>
        /// Disco de ouro liso do tamanho da moeda desenhada no icone (raio 240 de 256 no
        /// assets/gerar-tokens.py): as camadas dele fazem a borda da moeda grossa.
        /// </summary>
        private static Texture2D CoinEdge
        {
            get
            {
                if (_coinEdge) return _coinEdge;
                const int size = 64;
                float c = (size - 1) / 2f, r = size / 2f * 240f / 256f;
                Color gold = new Color32(214, 160, 66, 255);
                Color[] pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        Color p = gold;
                        p.a = Mathf.Clamp01(r - Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) + 0.5f);
                        pixels[y * size + x] = p;
                    }
                _coinEdge = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                _coinEdge.SetPixels(pixels);
                _coinEdge.Apply();
                return _coinEdge;
            }
        }

        /// <summary>
        /// No chao o item vira uma moeda chata com a arte do icone, em vez do Thunderstone de onde foi
        /// clonado. O colisor de pedra sai junto: com ele a moeda pousava de pe ou meio enterrada.
        /// O sprite e sem luz e visivel dos dois lados, entao aparece de qualquer angulo e brilha um
        /// pouco no escuro, como item magico.
        ///
        /// A moeda grossa tem a arte em cima e embaixo e, entre elas, discos de ouro que fazem a
        /// borda. Sao todos sprites: o jogo os desenha do mais longe para o mais perto da camera,
        /// entao vista de cima a face cobre os discos e so a borda aparece.
        /// </summary>
        private static void ApplyCoinVisual(GameObject item, Texture2D art, bool thick)
        {
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (Collider collider in item.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            // A moeda grossa vem das moedas do jogo: a escala delas mudaria o tamanho do disco.
            if (thick) item.transform.localScale = Vector3.one;

            BoxCollider box = item.AddComponent<BoxCollider>();
            box.size = new Vector3(CoinSize, thick ? ThickCoinHeight : 0.04f, CoinSize);

            art.wrapMode = TextureWrapMode.Clamp;
            Sprite face = CoinSprite(art);
            if (!thick)
            {
                AddCoinLayer(item, "coin", face, 0.021f, Color.white);
                return;
            }

            float half = ThickCoinHeight / 2f;
            AddCoinLayer(item, "coin-baixo", face, -half, Color.white);
            Sprite edge = CoinSprite(CoinEdge);
            for (int i = 1; i < ThickCoinLayers; i++)
            {
                float t = i / (float)ThickCoinLayers;
                AddCoinLayer(item, "coin-borda", edge, Mathf.Lerp(-half, half, t), Color.Lerp(ThickCoinEdgeDark, Color.white, t));
            }
            AddCoinLayer(item, "coin", face, half, Color.white);
        }

        private static Sprite CoinSprite(Texture2D texture)
            => Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width / CoinSize);

        private static void AddCoinLayer(GameObject item, string name, Sprite sprite, float height, Color color)
        {
            GameObject layer = new GameObject(name);
            layer.transform.SetParent(item.transform, false);
            layer.transform.localPosition = new Vector3(0f, height, 0f);
            // O sprite nasce de pe (plano XY); 90 graus em X deita ele virado para cima.
            layer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
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
            Prefabs.PiecesReady -= AddClonedPieces;
        }

        private static void AddVanillaClonedCreatures()
        {
            AddBatzao();
            AddNomTameableWolf();
            AddPorcoLox();
            AddSkeletao();
            Prefabs.ZNetSceneReady -= AddVanillaClonedCreatures;
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
            var adminWard = Prefabs.Clone("AdminWardSmall", "guard_stone");
            Piece piece = adminWard.GetComponent<Piece>();
            piece.m_resources[0].m_resItem = Prefabs.Get("SwordCheat").GetComponent<ItemDrop>();
            piece.m_resources[0].m_recover = false;

            piece.m_description = "Admin Ward small";
            piece.m_name = "Admin Ward small";

            PrivateArea area = piece.GetComponent<PrivateArea>();
            area.m_radius = Wards.WardProfiles.AdminWardSmallRadius;
            area.m_name = "AdminWardSmall";

            var comp = adminWard.GetComponentInChildren<MeshRenderer>();

            var materials = new List<Material>();
            materials.Add(Prefabs.Get("FreezeGland").GetComponentInChildren<MeshRenderer>().materials[0]);
            materials.Add(Prefabs.Get("FreezeGland").GetComponentInChildren<MeshRenderer>().materials[0]);

            comp.materials = materials.ToArray();

            Pieces.AddToHammer(adminWard, "Misc");
        }

        private static void AddBigdminWard()
        {
            var adminWard = Prefabs.Clone("AdminWard", "guard_stone");
            Piece piece = adminWard.GetComponent<Piece>();
            piece.m_resources[0].m_resItem = Prefabs.Get("SwordCheat").GetComponent<ItemDrop>();
            piece.m_resources[0].m_recover = false;

            piece.m_description = "Admin Ward";
            piece.m_name = "Admin Ward";

            PrivateArea area = piece.GetComponent<PrivateArea>();
            area.m_radius = Wards.WardProfiles.AdminWardRadius;
            area.m_name = "AdminWard";

            var comp = adminWard.GetComponentInChildren<MeshRenderer>();

            var materials = new List<Material>();
            materials.Add(Prefabs.Get("Tar").GetComponentInChildren<MeshRenderer>().materials[0]);
            materials.Add(Prefabs.Get("SurtlingCore").GetComponentInChildren<MeshRenderer>().materials[0]);

            comp.materials = materials.ToArray();

            Pieces.AddToHammer(adminWard, "Misc");
        }


        private static void AddAesirChest()
        {
            var aesirChest = Prefabs.Clone("AesirChest", "piece_chest_private");
            Piece piece = aesirChest.GetComponent<Piece>();
            piece.m_resources[0].m_resItem = Prefabs.Get("Bronze").GetComponent<ItemDrop>();
            piece.m_resources[1].m_resItem = Prefabs.Get("Wood").GetComponent<ItemDrop>();

            piece.m_description = "Aesir Chest";
            piece.m_name = "Aesir Chest";

            Pieces.AddToHammer(aesirChest, "Furniture");
        }

        private static void AddBuildableSpawners()
        {
            AddBuildableSpawner("BuildableGreydwarfNestSpawner", "Spawner_GreydwarfNest", "Black Forest Spawner", "Indestructible Black Forest monster spawner.");
            AddBuildableSpawner("BuildableDraugrPileSpawner", "Spawner_DraugrPile", "Swamp Spawner", "Indestructible Swamp monster spawner.");
        }

        private static void AddBuildableSpawner(string prefabName, string sourcePrefabName, string name, string description)
        {
            GameObject source = Prefabs.Get(sourcePrefabName);
            if (source == null)
            {
                Debug.LogWarning($"Could not create {name}. Missing vanilla prefab: {sourcePrefabName}");
                return;
            }

            GameObject spawner = Prefabs.Clone(prefabName, sourcePrefabName);
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

            Pieces.AddToHammer(spawner, "Misc");
        }

        private static Sprite GetSpawnerIcon(string sourcePrefabName)
        {
            string iconItem = sourcePrefabName == "Spawner_GreydwarfNest" ? "GreydwarfEye" : "WitheredBone";
            Sprite icon = Prefabs.Get(iconItem)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons?.FirstOrDefault();

            if (icon != null) return icon;

            return ObjectDB.instance?.GetItemPrefab("SpawnerToken")?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons?.FirstOrDefault();
        }

        private static void AddNomTameableWolf()
        {
            GameObject wolf = Creatures.Clone("LoboNaoDomavel", "Wolf");
            if (!wolf) return;

            var humanoid = wolf.GetComponent<Humanoid>();
            humanoid.m_name = "Lobo nao domavel";

            UnityEngine.Object.Destroy(wolf.GetComponent<Tameable>());
            UnityEngine.Object.Destroy(wolf.GetComponent<Procreation>());
        }

        private static void AddSkeletao()
        {
            GameObject skeletao = Creatures.Clone("Skeletao", "Skeleton", Character.Faction.Undead);
            if (!skeletao) return;

            var humanoid = skeletao.GetComponent<Humanoid>();

            humanoid.m_name = "Esqueletão";
            humanoid.m_boss = true;
            humanoid.m_health = 500;
            var renderers = skeletao.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var renderer in renderers)
            {
                renderer.material.color = Color.black;
                renderer.sharedMaterial.color = Color.black;
            }

            Vector3 newScale = skeletao.transform.localScale;
            newScale.x *= 1.3f;
            newScale.y *= 1.3f;
            newScale.z *= 1.3f;
            skeletao.transform.localScale = newScale;

            UnityEngine.Object.Destroy(skeletao.GetComponent<Tameable>());
            UnityEngine.Object.Destroy(skeletao.GetComponent<Procreation>());
        }

        private static void AddPorcoLox()
        {
            GameObject porcoLox = Creatures.Clone("PorcoLox", "Lox", Character.Faction.ForestMonsters);
            if (!porcoLox) return;

            var humanoid = porcoLox.GetComponent<Humanoid>();

            var ragdoll = Prefabs.Clone("PorcoLoxRagDoll", "lox_ragdoll");

            humanoid.m_name = "PorcoLox";
            humanoid.m_boss = true;
            humanoid.m_health = 300;

            ColorRenderers(porcoLox, Color.black);

            Vector3 newScale = porcoLox.transform.localScale;
            newScale.x *= 0.5f;
            newScale.y *= 0.5f;
            newScale.z *= 0.5f;
            ragdoll.transform.localScale = newScale;
            int idx = humanoid.m_deathEffects.m_effectPrefabs.ToList().FindIndex(x => x.m_prefab.name == "lox_ragdoll");

            porcoLox.GetComponent<Humanoid>().m_deathEffects.m_effectPrefabs.ToList()[idx].m_prefab = ragdoll;

            ColorRenderers(ragdoll, Color.black);

            porcoLox.transform.localScale = newScale;

            UnityEngine.Object.Destroy(porcoLox.GetComponent<Tameable>());
            UnityEngine.Object.Destroy(porcoLox.GetComponent<Procreation>());
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
            GameObject batzao = Creatures.Clone("Morcegao", "Bat", Character.Faction.Undead,
                new Creatures.Drop
                {
                    Item = "Coins",
                    Chance = 100,
                    MinAmount = 50,
                    MaxAmount = 100,
                    OnePerPlayer = false,
                    LevelMultiplier = false
                });
            if (!batzao) return;

            Vector3 newScale = batzao.transform.localScale;
            newScale.x *= 3;
            newScale.y *= 3;
            newScale.z *= 3;
            batzao.transform.localScale = newScale;

            var humanoid = batzao.GetComponent<Humanoid>();
            humanoid.m_name = "Morcegão";
            humanoid.m_health = 500;
            humanoid.m_boss = true;
        }
    }
}
