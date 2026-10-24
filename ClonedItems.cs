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
            // Modelo visual original do Valheim usado para renderizar o ícone do item.
            public string IconPrefabName;
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
            // Tipo do item; null = o do item-base (Trophy/Misc, nao da "Usar" no inventario).
            // Consumable: o "Usar" chama Humanoid.UseItem, que o Cosmeticos.cs e o Lugares.cs
            // interceptam para as fichas que se trocam sozinhas (sem passar por um NPC).
            public ItemDrop.ItemData.ItemType? ItemType;
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
            // Fichas vendidas na Loja Deadcoins (NpcValheim): compradas com Deadcoins, por isso
            // Value = 0 (nunca viram moeda no mercador) e, por serem itens nativos (IsNativeItem),
            // nunca caem como carga de PvP (PvpCargoKeep); vao para a tumba como qualquer item na
            // morte normal, igual a Garantia de Refino. FichaFundador e FichaBatizar sao
            // Consumable: "Usar" no inventario as troca sozinhas (Cosmeticos.cs/Lugares.cs). As
            // outras tres ainda nao tem para onde ir (Barbeiro e o pacote de Halloween/temporada
            // entram nas proximas ondas) e ficam so como item ate la.
            new NativeItemDefinition { PrefabName = "FichaFundador", Name = "Ficha do Fundador", Description = "Use para liberar o título permanente [Fundador], de quem jogou desde a abertura do servidor.", MaxStack = 10, Icon = "fichafundador.png", Value = 0, Coin = true, ItemType = ItemDrop.ItemData.ItemType.Consumable },
            new NativeItemDefinition { PrefabName = "FichaBarbeiro", Name = "Ficha do Barbeiro", Description = "Troque com o Barbeiro para mudar cabelo, barba e cores.", MaxStack = 10, Icon = "fichabarbeiro.png", Value = 0, Coin = true },
            new NativeItemDefinition { PrefabName = "FichaHalloween", Name = "Ficha de Halloween", Description = "Troque pelo pacote de Halloween: título, vela, chama e lápide do evento.", MaxStack = 10, Icon = "fichahalloween.png", Value = 0, Coin = true },
            new NativeItemDefinition { PrefabName = "FichaTemporada", Name = "Ficha de Temporada", Description = "Troque pelo passe de temporada: trilha de recompensas cosméticas da temporada atual.", MaxStack = 10, Icon = "fichatemporada.png", Value = 0, Coin = true },
            new NativeItemDefinition { PrefabName = "FichaBatizar", Name = "Ficha de Batizar Lugar", Description = "Use para dar um nome a um lugar do mapa: fica marcado para todos ali onde você estiver.", MaxStack = 10, Icon = "fichabatizar.png", Value = 0, Coin = true, ItemType = ItemDrop.ItemData.ItemType.Consumable },
            new NativeItemDefinition { PrefabName = "FichaBrasao", Name = "Ficha de Vela/Brasão", Description = "Troque com um admin por uma vela ou brasão de guilda sob encomenda.", MaxStack = 10, Icon = "fichabrasao.png", Value = 0, Coin = true },
            new NativeItemDefinition { PrefabName = "FichaPet", Name = "Ficha antiga de Pet", Description = "Ficha descontinuada. Os pets agora são itens individuais negociáveis.", MaxStack = 10, Icon = "fichapet.png", Value = 0, Coin = true, ItemType = ItemDrop.ItemData.ItemType.Consumable },
            // Cada pet é um item único, permanente enquanto o jogador o mantiver e negociável.
            // O patch de Pets intercepta a ação de usar e não consome o item.
            new NativeItemDefinition { PrefabName = "Pet_galinha", Name = "Pet: Galinha", Description = "Use para invocar ou guardar sua galinha. Item permanente e negociável.", MaxStack = 1, Icon = "pet-galinha.png", IconPrefabName = "Hen", Value = 0, ItemType = ItemDrop.ItemData.ItemType.Consumable },
            new NativeItemDefinition { PrefabName = "Pet_pintinho", Name = "Pet: Pintinho", Description = "Use para invocar ou guardar seu pintinho. Item permanente e negociável.", MaxStack = 1, Icon = "pet-pintinho.png", IconPrefabName = "Chicken", Value = 0, ItemType = ItemDrop.ItemData.ItemType.Consumable },
            new NativeItemDefinition { PrefabName = "Pet_leitao", Name = "Pet: Leitão", Description = "Use para invocar ou guardar seu leitão. Item permanente e negociável.", MaxStack = 1, Icon = "pet-leitao.png", IconPrefabName = "Boar_piggy", Value = 0, ItemType = ItemDrop.ItemData.ItemType.Consumable },
            new NativeItemDefinition { PrefabName = "Pet_wolf_cub", Name = "Pet: Filhote de Lobo", Description = "Use para invocar ou guardar seu filhote de lobo. Item permanente e negociável.", MaxStack = 1, Icon = "pet-wolf_cub.png", IconPrefabName = "Wolf_cub", Value = 0, ItemType = ItemDrop.ItemData.ItemType.Consumable },
            new NativeItemDefinition { PrefabName = "Pet_lox_cub", Name = "Pet: Bezerro de Lox", Description = "Use para invocar ou guardar seu bezerro de lox. Item permanente e negociável.", MaxStack = 1, Icon = "pet-lox_cub.png", IconPrefabName = "Lox_Calf", Value = 0, ItemType = ItemDrop.ItemData.ItemType.Consumable },
            new NativeItemDefinition { PrefabName = "Pet_corvo", Name = "Pet: Corvo", Description = "Use para invocar ou guardar seu corvo. Item permanente e negociável.", MaxStack = 1, Icon = "pet-corvo.png", IconPrefabName = "Crow", Value = 0, ItemType = ItemDrop.ItemData.ItemType.Consumable },
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
        private static readonly HashSet<string> PrefabIconApplied = new HashSet<string>(StringComparer.Ordinal);

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
            ApplyPetPrefabIcons();

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
            if (definition.ItemType.HasValue)
                itemDrop.m_itemData.m_shared.m_itemType = definition.ItemType.Value;
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
            ApplyPetPrefabIcons();
            Prefabs.ZNetSceneReady -= AddVanillaClonedCreatures;
        }

        /// <summary>Renderiza os modelos originais do Valheim como ícones, mantendo o PNG como fallback.</summary>
        private static void ApplyPetPrefabIcons()
        {
            if (ZNetScene.instance == null) return;
            foreach (NativeItemDefinition definition in NativeItems)
            {
                if (string.IsNullOrEmpty(definition.IconPrefabName) ||
                    PrefabIconApplied.Contains(definition.PrefabName) ||
                    !RegisteredNativeItems.TryGetValue(definition.PrefabName, out GameObject item) ||
                    item == null)
                    continue;
                try
                {
                    Sprite sprite = RenderPrefabIcon(definition.IconPrefabName);
                    ItemDrop drop = item.GetComponent<ItemDrop>();
                    if (sprite == null || drop?.m_itemData?.m_shared == null) continue;
                    drop.m_itemData.m_shared.m_icons = new[] { sprite };
                    PrefabIconApplied.Add(definition.PrefabName);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Deadheim] Ícone 3D de '{definition.PrefabName}' indisponível; usando o ícone ilustrado: {ex.Message}");
                }
            }
        }

        private static Sprite RenderPrefabIcon(string prefabName)
        {
            GameObject source = ZNetScene.instance?.GetPrefab(prefabName);
            if (source == null) return null;

            const int layer = 30;
            const int size = 256;
            GameObject stage = new GameObject("Deadheim_PetIconStage");
            stage.hideFlags = HideFlags.HideAndDontSave;
            stage.SetActive(false);
            // Sem isto o ZNetView/Character do prefab acordam de verdade (ver a ativação do palco abaixo).
            bool previousForce = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            GameObject model;
            try { model = UnityEngine.Object.Instantiate(source, stage.transform); }
            finally { ZNetView.m_forceDisableInit = previousForce; }
            model.name = "PetIconModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            SetLayerRecursively(model.transform, layer);

            // O Awake roda mesmo com o componente desligado, e o do RandomFlyingBird (corvo) lê o
            // ZNetView que o flag acima não deixou iniciar: NullReferenceException a cada boot.
            foreach (RandomFlyingBird bird in model.GetComponentsInChildren<RandomFlyingBird>(true))
                UnityEngine.Object.DestroyImmediate(bird);

            foreach (Behaviour behaviour in model.GetComponentsInChildren<Behaviour>(true))
            {
                if (!(behaviour is Animator)) behaviour.enabled = false;
            }
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;

            GameObject cameraObject = new GameObject("Deadheim_PetIconCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.aspect = 1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.cullingMask = 1 << layer;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;

            GameObject keyObject = new GameObject("Deadheim_PetIconKeyLight");
            keyObject.hideFlags = HideFlags.HideAndDontSave;
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.25f;
            key.color = new Color(1f, 0.88f, 0.7f);
            key.cullingMask = 1 << layer;
            key.shadows = LightShadows.None;
            key.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

            RenderTexture target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 2;
            camera.targetTexture = target;

            Texture2D texture = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                // O Awake do ZNetView roda aqui (Instantiate sob pai inativo não acorda nada). Sem o
                // flag ele cria um ZDO persistente com o hash de "PetIconModel", que vai para o mundo
                // e faz todo cliente spamar "Missing prefab hash: 1461255765".
                ZNetView.m_forceDisableInit = true;
                try { stage.SetActive(true); }
                finally { ZNetView.m_forceDisableInit = previousForce; }
                Animator animator = model.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    animator.Rebind();
                    animator.Update(0f);
                }

                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer != null && renderer.enabled).ToArray();
                if (renderers.Length == 0) return null;
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                float distance = Mathf.Max(3f, bounds.extents.magnitude * 4f);
                Vector3 center = bounds.center;
                camera.transform.position = center + new Vector3(0.55f, bounds.extents.y * 0.3f, -distance);
                camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, Vector3.up);
                camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.18f;

                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                texture.Apply();
                return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(keyObject);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        // Antes da 7.8.1 o palco do ícone criava um ZDO "PetIconModel" (perto da origem) a cada vez que
        // um cliente entrava; o servidor gravou todos no mundo e eles seguem fazendo quem passa perto
        // spamar "Missing prefab hash: 1461255765". O servidor apaga esses órfãos ao carregar o mundo.
        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.Load))]
        private static class PurgePetIconZdosPatch
        {
            private static readonly int PetIconModelHash = "PetIconModel".GetStableHashCode();
            // m_objectsByID é privado no assembly original: acesso direto pelo publicizado dá FieldAccessException.
            private static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> ObjectsById =
                AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");

            private static void Postfix(ZDOMan __instance)
            {
                List<ZDO> orphans = ObjectsById(__instance).Values
                    .Where(zdo => zdo != null && zdo.GetPrefab() == PetIconModelHash)
                    .ToList();
                if (orphans.Count == 0) return;

                long sessionId = ZDOMan.GetSessionID();
                foreach (ZDO zdo in orphans)
                {
                    zdo.SetOwner(sessionId);
                    __instance.DestroyZDO(zdo);
                }
                Debug.Log($"[Deadheim] {orphans.Count} ZDO(s) órfão(s) de 'PetIconModel' removido(s) do mundo.");
            }
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
