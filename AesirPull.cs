using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Aesir 10: animais domesticados e fogueiras do VIP puxam do bau proximo.
    ///
    /// Roda no CLIENTE do proprio VIP, como todo Aesir deste repo (6, 7, 8, 9, 12, 14, 15, 16
    /// usam IsLocalPlayerVip). O briefing pedia "so no servidor", mas o servidor do Deadheim
    /// nao tem lista VIP confiavel: VipListApi.IsVip(string) so responde certo para o jogador
    /// local, e nenhum codigo do repo resolve peer -> conta -> VIP no servidor. Fazer isso
    /// seria construir uma tabela de identidade do zero (e duplicar o que o ValheimMontarias
    /// ja tem em RiderServer.AccountIdOf).
    ///
    /// O consumo e seguro sem servidor proprio porque o Container e objeto de rede: o cliente
    /// reivindica a posse (como o Fireplace.Interact do jogo ja faz), tira o item e o ZDO
    /// propaga. Quem pode puxar e decidido por PrivateArea.CheckAccess (ward) e
    /// Container.CheckAccess (bau privado), exatamente como o /depositar faz.
    ///
    /// Dois comportamentos:
    ///  (a) fogueira/braseiro: quando o combustivel cai abaixo do limite, repoe do bau proximo.
    ///  (b) animal domesticado: quando o MonsterAI nao achou comida no chao, procura no bau.
    /// </summary>
    internal static class AesirPullConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> Radius;
        public static ConfigEntry<bool> OnlyOwnerVip;
        public static ConfigEntry<float> PullIntervalSeconds;
        public static ConfigEntry<float> FireplaceThreshold;
        public static ConfigEntry<string> AllowedFuel;

        public static void Bind(ConfigFile config)
        {
            const string secao = "AesirContainers";

            Enabled = Plugin.Synced(config.Bind(secao, "Enabled", true,
                "Aesir 10: fogueiras e animais domesticados do VIP puxam combustivel/comida de baus proximos."));

            Radius = Plugin.Synced(config.Bind(secao, "Radius", 8f,
                new ConfigDescription("Raio da busca por baus, em metros.", new AcceptableValueRange<float>(1f, 30f))));

            OnlyOwnerVip = Plugin.Synced(config.Bind(secao, "OnlyOwnerVip", true,
                "So puxa de bau cujo dono (criador da peca) tambem e VIP. Desligue para permitir o bau de qualquer um que o jogador possa abrir."));

            PullIntervalSeconds = Plugin.Synced(config.Bind(secao, "PullIntervalSeconds", 5f,
                new ConfigDescription("Intervalo minimo entre duas buscas de bau no mesmo objeto, em segundos.",
                    new AcceptableValueRange<float>(1f, 60f))));

            FireplaceThreshold = Plugin.Synced(config.Bind(secao, "FireplaceThreshold", 0.5f,
                new ConfigDescription("Fracao do combustivel maximo abaixo da qual a fogueira puxa do bau. 0.5 = metade.",
                    new AcceptableValueRange<float>(0.05f, 0.95f))));

            AllowedFuel = Plugin.Synced(config.Bind(secao, "AllowedFuel", "",
                "Nomes (m_shared.m_name) de itens que podem abastecer fogueira, separados por virgula. " +
                "Vazio = usa o proprio m_fuelItem do prefab (o certo na maioria dos casos)."));
        }
    }

    /// <summary>Busca e consumo de baus, compartilhado com o item 13.</summary>
    /// <summary>Acesso por reflexao aos membros privados do jogo (o Mono recusa alguns em runtime).</summary>
    internal static class Priv
    {
        private static readonly System.Reflection.FieldInfo GhostField = AccessTools.Field(typeof(Piece), "s_ghostLayer");
        private static readonly System.Reflection.FieldInfo FireViewField = AccessTools.Field(typeof(Fireplace), "m_nview");
        private static readonly System.Reflection.FieldInfo FirePieceField = AccessTools.Field(typeof(Fireplace), "m_piece");
        private static readonly System.Reflection.FieldInfo AnimatorField = AccessTools.Field(typeof(BaseAI), "m_animator");
        private static readonly System.Reflection.MethodInfo ResetFeedingMethod = AccessTools.Method(typeof(Tameable), "ResetFeedingTimer");

        internal static int GhostLayer => GhostField != null ? (int)GhostField.GetValue(null) : LayerMask.NameToLayer("ghost");
        internal static ZNetView FireView(Fireplace f) => FireViewField?.GetValue(f) as ZNetView;
        internal static Piece FirePiece(Fireplace f) => FirePieceField?.GetValue(f) as Piece;
        internal static Animator Animator(BaseAI ai) => AnimatorField?.GetValue(ai) as Animator;
        internal static void ResetFeedingTimer(Tameable t) => ResetFeedingMethod?.Invoke(t, null);

        // Acessos privados usados por PetDrops, Plantas e Transmog.
        private static readonly System.Reflection.FieldInfo LastHitField = AccessTools.Field(typeof(Character), "m_lastHit");
        private static readonly System.Reflection.FieldInfo AllAreasField = AccessTools.Field(typeof(PrivateArea), "m_allAreas");
        private static readonly System.Reflection.MethodInfo AreaIsEnabled = AccessTools.Method(typeof(PrivateArea), "IsEnabled");
        private static readonly System.Reflection.MethodInfo AreaIsInside = AccessTools.Method(typeof(PrivateArea), "IsInside", new[] { typeof(Vector3), typeof(float) });
        private static readonly System.Reflection.FieldInfo NoPlacementCostField = AccessTools.Field(typeof(Player), "m_noPlacementCost");
        private static readonly System.Reflection.FieldInfo VisNviewField = AccessTools.Field(typeof(VisEquipment), "m_nview");

        internal static HitData LastHit(Character c) => LastHitField?.GetValue(c) as HitData;
        internal static List<PrivateArea> AllAreas() => AllAreasField?.GetValue(null) as List<PrivateArea> ?? new List<PrivateArea>();
        internal static bool AreaEnabled(PrivateArea a) => AreaIsEnabled != null && (bool)AreaIsEnabled.Invoke(a, null);
        internal static bool AreaInside(PrivateArea a, Vector3 pos, float radius) =>
            AreaIsInside != null && (bool)AreaIsInside.Invoke(a, new object[] { pos, radius });
        internal static bool NoPlacementCost(Player p) => NoPlacementCostField != null && (bool)NoPlacementCostField.GetValue(p);
        internal static ZNetView VisNview(VisEquipment v) => VisNviewField?.GetValue(v) as ZNetView;
    }

    internal static class AesirChests
    {
        private static readonly List<Piece> PieceBuffer = new List<Piece>(256);

        /// <summary>
        /// Bau mais proximo com pelo menos 1 unidade do item, que o jogador possa abrir.
        /// Exclui o bau em uso por outro (m_inUse) e o que o proprio jogador tem aberto.
        ///
        /// Nao usa OverlapSphere. A primeira versao usava, com buffer de 64 e depois 256, e
        /// o defeito nao era o tamanho do buffer: uma base densa tem centenas de colisores de
        /// parede, chao e escada em 8 m, e o OverlapSphere nao tem ordem nenhuma -- o bau
        /// podia simplesmente nao caber. Buffer maior so empurra o problema.
        ///
        /// A varredura e a lista de pecas do proprio jogo (Piece.s_allPieces, a mesma que o
        /// Piece.GetAllPiecesInRadius usa), filtrada por distancia. Nao ha teto de colisor:
        /// por mais apertada que a base esteja, o bau e visitado. Custa uma passada na lista
        /// de pecas carregadas -- por isso o cache de intervalo em quem chama, nao por frame.
        /// </summary>
        internal static Container FindWith(Vector3 origin, float radius, long playerId, string sharedName)
        {
            if (string.IsNullOrEmpty(sharedName)) return null;

            float radiusSqr = radius * radius;
            Container best = null;
            float bestDist = float.MaxValue;

            // O jogo so marca a peca como fantasma enquanto ela esta sendo posicionada; uma
            // peca de verdade ja colocada tem layer normal. A checagem copia o
            // GetAllPiecesInRadius para nao olhar peca em construcao.
            int ghostLayer = Priv.GhostLayer;

            PieceBuffer.Clear();
            Piece.GetAllPiecesInRadius(origin, radius, PieceBuffer);
            for (int i = 0; i < PieceBuffer.Count; i++)
            {
                Piece piece = PieceBuffer[i];
                if (piece == null) continue;
                if (ghostLayer != 0 && piece.gameObject.layer == ghostLayer) continue;

                // O Container fica no mesmo GameObject da Piece (Container.Awake faz
                // GetComponent<Piece>() no proprio objeto). O GetComponentInParent cobre o
                // caso de um bau montado com o colisor num filho.
                Container container = piece.GetComponent<Container>()
                    ?? piece.GetComponentInParent<Container>()
                    ?? piece.GetComponentInChildren<Container>();
                if (container == null) continue;

                Inventory inv = container.GetInventory();
                if (inv == null) continue;
                if (container.IsInUse()) continue;                       // aberto por outro jogador
                if (!container.CheckAccess(playerId)) continue;          // bau privado de outro
                // flash:false -- o padrao do jogo pisca o escudo da ward; perto de um bau
                // alheio isso piscaria a cada varredura, de graca.
                if (!PrivateArea.CheckAccess(container.transform.position, flash: false)) continue;
                if (inv.CountItems(sharedName, -1, false) <= 0) continue;

                float dist = (container.transform.position - origin).sqrMagnitude;
                if (dist > radiusSqr) continue;
                if (dist < bestDist) { bestDist = dist; best = container; }
            }
            return best;
        }

        /// <summary>
        /// O criador da peca e VIP? O criador vem da ZDO (replicada), e a lista VIP do
        /// cliente so cobre o jogador local com confianca -- entao isto responde "sim" quando
        /// o criador e o proprio jogador local VIP. Um bau de OUTRO VIP cai no caso
        /// desconhecido e e recusado: e o lado seguro, e o /depositar tambem nao olha o dono.
        ///
        /// Nao use isto em ANIMAL: bicho nao tem componente Piece, a peca vem nula e a funcao
        /// devolvia false, matando o item (b) inteiro com OnlyOwnerVip ligado (o padrao).
        /// Para animal, quem responde e o campo "owner" da ZDO -- ver AesirPullAnimal.
        /// </summary>
        internal static bool OwnerIsVip(Piece piece)
        {
            if (!AesirPullConfig.OnlyOwnerVip.Value) return true;
            if (piece == null) return false;
            Player local = Player.m_localPlayer;
            if (local == null) return false;
            return piece.GetCreator() == local.GetPlayerID() && VipList.VipListApi.IsLocalPlayerVip();
        }

        /// <summary>Tira uma unidade do bau e devolve true. Reivindica a posse antes: sem isso
        /// o RemoveItem roda so no cliente e o item nao sai do ZDO para os outros.</summary>
        internal static bool Take(Container container, string sharedName, int amount)
        {
            if (container == null || amount <= 0) return false;
            Inventory inv = container.GetInventory();
            if (inv == null || inv.CountItems(sharedName, -1, false) < amount) return false;

            ZNetView nview = container.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid() && !nview.IsOwner())
                nview.ClaimOwnership();

            inv.RemoveItem(sharedName, amount, -1, false);
            return true;
        }
    }

    // ------------------------------------------------------- Aesir 10 (a): fogueiras

    [HarmonyPatch]
    internal static class AesirPullFireplace
    {
        private static readonly Dictionary<int, float> NextTry = new Dictionary<int, float>();

        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.UpdateFireplace))]
        private static class Patch
        {
            private static void Postfix(Fireplace __instance)
            {
                try { MaybeRefuel(__instance); }
                catch (Exception ex)
                {
                    Debug.LogError($"[Deadheim Aesir10] Erro ao abastecer fogueira: {ex.Message}");
                }
            }
        }

        private static void MaybeRefuel(Fireplace fire)
        {
            if (!AesirPullConfig.Enabled.Value) return;
            if (fire == null) return;
            ZNetView fireView = Priv.FireView(fire);
            if (fireView == null || !fireView.IsValid()) return;
            if (fire.m_infiniteFuel || !fire.m_canRefill) return;
            if (!fireView.IsOwner()) return;

            Player local = Player.m_localPlayer;
            if (local == null || !VipList.VipListApi.IsLocalPlayerVip()) return;

            // Durante a janela de raid, a fogueira de base inimiga nao puxa de bau alheio.
            // O Deadheim nao referencia o RaidSystem (mods separados): quem diz "aqui manda o
            // raid" e o WardBridge, que o RaidSystem registra no Awake dele. Sem RaidSystem
            // instalado o delegate e null e este trecho nao faz nada.
            if (Wards.WardBridge.Governed(fire.transform.position)) return;

            float fuel = fireView.GetZDO().GetFloat(ZDOVars.s_fuel);
            if (fuel > fire.m_maxFuel * AesirPullConfig.FireplaceThreshold.Value) return;

            int id = fire.GetInstanceID();
            float now = Time.realtimeSinceStartup;
            if (NextTry.TryGetValue(id, out float next) && now < next) return;
            NextTry[id] = now + AesirPullConfig.PullIntervalSeconds.Value;

            string fuelName = FuelName(fire);
            if (string.IsNullOrEmpty(fuelName)) return;
            if (!AesirChests.OwnerIsVip(Priv.FirePiece(fire))) return;

            Container chest = AesirChests.FindWith(fire.transform.position,
                AesirPullConfig.Radius.Value, local.GetPlayerID(), fuelName);
            if (chest == null) return;

            // Quantas unidades faltam, no maximo 50 (nao esvazia um estoque gigante de uma vez).
            int want = Mathf.Clamp(Mathf.CeilToInt(fire.m_maxFuel - fuel), 1, 50);
            int have = chest.GetInventory().CountItems(fuelName, -1, false);
            int take = Mathf.Min(want, have);
            if (take <= 0) return;
            if (!AesirChests.Take(chest, fuelName, take)) return;

            // O mesmo caminho do jogo: RPC no dono, que soma e mostra o efeito.
            fireView.InvokeRPC("RPC_AddFuelAmount", (float)take);
            local.Message(MessageHud.MessageType.TopLeft,
                $"Aesir: {take}x {Localization.instance.Localize(fuelName)} do bau para a fogueira.");
        }

        /// <summary>O combustivel do prefab; AllowedFuel sobrescreve quando configurado.</summary>
        private static string FuelName(Fireplace fire)
        {
            string cfg = AesirPullConfig.AllowedFuel?.Value;
            if (!string.IsNullOrWhiteSpace(cfg))
            {
                string first = cfg.Split(',').Select(s => s.Trim()).FirstOrDefault(s => s.Length > 0);
                if (!string.IsNullOrEmpty(first)) return first;
            }
            return fire.m_fuelItem != null && fire.m_fuelItem.m_itemData != null
                ? fire.m_fuelItem.m_itemData.m_shared.m_name
                : null;
        }
    }

    // --------------------------------------------------------- Aesir 10 (b): animais

    [HarmonyPatch]
    internal static class AesirPullAnimal
    {
        private static readonly Dictionary<int, float> NextTry = new Dictionary<int, float>();

        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateConsumeItem))]
        private static class Patch
        {
            private static void Postfix(MonsterAI __instance, Humanoid humanoid, bool __result)
            {
                if (__result) return; // achou no chao: o vanilla resolveu
                try { MaybeFeed(__instance, humanoid); }
                catch (Exception ex)
                {
                    Debug.LogError($"[Deadheim Aesir10] Erro ao alimentar animal: {ex.Message}");
                }
            }
        }

        private static void MaybeFeed(MonsterAI ai, Humanoid humanoid)
        {
            if (!AesirPullConfig.Enabled.Value) return;
            if (ai == null || ai.m_consumeItems == null || ai.m_consumeItems.Count == 0) return;

            Tameable tameable = ai.GetComponent<Tameable>();
            if (tameable == null || !tameable.IsTamed()) return;

            ZNetView nview = ai.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;

            Player local = Player.m_localPlayer;
            if (local == null || !VipList.VipListApi.IsLocalPlayerVip()) return;

            // O dono do animal e quem mandou domesticar: a ZDO guarda o playerID. Sem isso
            // qualquer um alimentaria o bicho de outro com o proprio bau.
            if (!IsOwnerLocal(ai, local)) return;

            if (!tameable.IsHungry()) return;

            int id = ai.GetInstanceID();
            float now = Time.realtimeSinceStartup;
            if (NextTry.TryGetValue(id, out float next) && now < next) return;
            NextTry[id] = now + AesirPullConfig.PullIntervalSeconds.Value;

            string food = FoodName(ai);
            if (string.IsNullOrEmpty(food)) return;
            // Sem OwnerIsVip aqui: bicho nao tem Piece, e o dono ja foi checado por
            // IsOwnerLocal (o campo "owner" da ZDO). Com OnlyOwnerVip ligado -- o padrao --
            // a checagem de peca devolvia false e nenhum animal era alimentado nunca.

            Container chest = AesirChests.FindWith(ai.transform.position,
                AesirPullConfig.Radius.Value, local.GetPlayerID(), food);
            if (chest == null) return;
            if (!AesirChests.Take(chest, food, 1)) return;

            // Simula o que o consumo no chao faz: sacia, gera o efeito e anima o bicho. O
            // m_onConsumedItem nao e chamado de proposito -- o Tameable.OnConsumedItem so
            // dispara o efeito e chama ResetFeedingTimer, que fazemos aqui direto.
            Priv.ResetFeedingTimer(tameable);
            Priv.Animator(ai)?.SetTrigger("consume");
            if (humanoid != null)
                humanoid.m_consumeItemEffects.Create(ai.transform.position, Quaternion.identity);

            local.Message(MessageHud.MessageType.TopLeft,
                $"Aesir: {Localization.instance.Localize(food)} do bau para {tameable.GetHoverName()}.");
        }

        private static string FoodName(MonsterAI ai)
        {
            foreach (ItemDrop drop in ai.m_consumeItems)
                if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
                    return drop.m_itemData.m_shared.m_name;
            return null;
        }

        /// <summary>
        /// O ZDO "owner" do animal e o playerID de quem domesticou.
        ///
        /// Nome confirmado no assembly publicizado: ZDOVars.s_owner e
        /// "owner".GetStableHashCode(), ou seja as duas formas dao a MESMA chave -- o fallback
        /// so existe para o caso de o campo mudar de nome numa versao futura. Animal sem dono
        /// gravado (0) passa: e bicho domesticado por outro que ainda nao escreveu a chave, e
        /// recusar deixaria o dono sem o recurso.
        /// </summary>
        private static bool IsOwnerLocal(MonsterAI ai, Player local)
        {
            ZNetView nview = ai.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return false;
            ZDO zdo = nview.GetZDO();
            if (zdo == null) return false;

            long owner = zdo.GetLong(ZDOVars.s_owner, 0L);
            if (owner == 0L) owner = zdo.GetLong("owner".GetStableHashCode(), 0L);
            return owner == 0L || owner == local.GetPlayerID();
        }
    }
}
