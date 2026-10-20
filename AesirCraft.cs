using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

// Aesir 13: o VIP fabrica usando tambem o que esta nos baus proximos que ele pode abrir.
//
// Dois ganchos, os mesmos que o jogo usa para fabricar:
//  - Player.HaveRequirementItems (Recipe): se o inventario nao basta, soma os baus;
//  - Player.ConsumeResources: antes do consumo do jogo, tira dos baus o que falta no inventario
//    (o jogo depois tira so o que o inventario tem, entao nada e consumido em dobro).
// So fabricacao (Recipe). Construcao (Piece) nao entra: pecas passam por outras travas.
namespace Deadheim
{
    internal static class AesirCraftConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> Radius;
        public static ConfigEntry<bool> VipOnly;

        public static void Bind(ConfigFile config)
        {
            const string secao = "AesirCraft";
            Enabled = Plugin.Synced(config.Bind(secao, "Enabled", true,
                "Aesir 13: fabricar usando o conteudo dos baus proximos."));
            Radius = Plugin.Synced(config.Bind(secao, "Radius", 5f,
                new ConfigDescription("Raio, em metros a partir do jogador, dos baus que contam.",
                    new AcceptableValueRange<float>(1f, 20f))));
            VipOnly = Plugin.Synced(config.Bind(secao, "VipOnly", true,
                "So VIP (Aesir) usa baus ao fabricar. Desligado, todos usam."));
        }
    }

    internal static class AesirCraft
    {
        private static readonly List<Piece> PieceBuffer = new List<Piece>(256);
        private static readonly List<Container> Cached = new List<Container>();
        private static float _cachedAt = -10f;
        private static Vector3 _cachedOrigin;
        private static bool _busy;

        private static bool Active(Player player)
        {
            if (player == null || player != Player.m_localPlayer) return false;
            if (AesirCraftConfig.Enabled == null || !AesirCraftConfig.Enabled.Value) return false;
            if (AesirCraftConfig.VipOnly.Value && !VipList.VipListApi.IsLocalPlayerVip()) return false;
            return true;
        }

        /// <summary>Baus proximos que o jogador pode abrir. Cache de 1 s: o painel de fabricacao
        /// pergunta os requisitos a cada frame.</summary>
        private static List<Container> Nearby(Player player)
        {
            Vector3 origin = player.transform.position;
            if (Time.realtimeSinceStartup - _cachedAt < 1f && (origin - _cachedOrigin).sqrMagnitude < 1f)
                return Cached;

            Cached.Clear();
            _cachedAt = Time.realtimeSinceStartup;
            _cachedOrigin = origin;

            float radius = AesirCraftConfig.Radius.Value;
            long playerId = player.GetPlayerID();
            int ghostLayer = Priv.GhostLayer;

            PieceBuffer.Clear();
            Piece.GetAllPiecesInRadius(origin, radius, PieceBuffer);
            for (int i = 0; i < PieceBuffer.Count; i++)
            {
                Piece piece = PieceBuffer[i];
                if (piece == null || piece.gameObject.layer == ghostLayer) continue;
                Container container = piece.GetComponent<Container>()
                    ?? piece.GetComponentInParent<Container>()
                    ?? piece.GetComponentInChildren<Container>();
                if (container == null || Cached.Contains(container)) continue;
                if (container.GetInventory() == null) continue;
                if (container.IsInUse()) continue;                              // aberto por outro jogador
                if (!container.CheckAccess(playerId)) continue;                 // bau privado de outro
                if (!PrivateArea.CheckAccess(container.transform.position, flash: false)) continue;
                Cached.Add(container);
            }
            return Cached;
        }

        private static int InChests(Player player, string sharedName)
        {
            int total = 0;
            foreach (Container c in Nearby(player))
            {
                Inventory inv = c != null ? c.GetInventory() : null;
                if (inv != null) total += inv.CountItems(sharedName, -1, false);
            }
            return total;
        }

        private static string SharedName(Piece.Requirement req) =>
            req?.m_resItem?.m_itemData?.m_shared?.m_name;

        [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
        private static class HaveItemsPatch
        {
            private static void Postfix(Player __instance, Recipe piece, bool discover, int qualityLevel, ref bool __result)
            {
                if (__result || discover || _busy || piece == null) return;
                if (!Active(__instance)) return;
                Inventory inv = __instance.GetInventory();
                if (inv == null) return;

                foreach (Piece.Requirement req in piece.m_resources)
                {
                    string name = SharedName(req);
                    if (string.IsNullOrEmpty(name)) continue;
                    int need = req.GetAmount(qualityLevel);
                    if (need <= 0) continue;
                    int have = inv.CountItems(name, -1, false);
                    if (have >= need) continue;
                    if (have + InChests(__instance, name) < need) return;   // continua faltando
                }
                __result = true;
            }
        }

        // Construcao: o martelo pergunta HaveRequirements(Piece, CanBuild) a cada frame (fantasma
        // vermelho/branco) e ao colocar. O consumo passa pelo ConsumeResources abaixo, o mesmo da
        // fabricacao.
        [HarmonyPatch(typeof(Player), "HaveRequirements", new[] { typeof(Piece), typeof(Player.RequirementMode) })]
        private static class HavePieceRequirementsPatch
        {
            private static void Postfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
            {
                if (__result || _busy || piece == null || mode != Player.RequirementMode.CanBuild) return;
                if (!Active(__instance)) return;
                Inventory inv = __instance.GetInventory();
                if (inv == null) return;

                foreach (Piece.Requirement req in piece.m_resources)
                {
                    string name = SharedName(req);
                    if (string.IsNullOrEmpty(name)) continue;
                    int need = req.m_amount;
                    if (need <= 0) continue;
                    int have = inv.CountItems(name, -1, false);
                    if (have >= need) continue;
                    if (have + InChests(__instance, name) < need) return;   // continua faltando
                }
                __result = true;
            }
        }

        [HarmonyPatch(typeof(Player), "ConsumeResources")]
        private static class ConsumePatch
        {
            private static void Prefix(Player __instance, Piece.Requirement[] requirements, int qualityLevel, int multiplier)
            {
                if (_busy || requirements == null || !Active(__instance)) return;
                Inventory inv = __instance.GetInventory();
                if (inv == null) return;
                if (multiplier < 1) multiplier = 1;

                _busy = true;
                try
                {
                    foreach (Piece.Requirement req in requirements)
                    {
                        string name = SharedName(req);
                        if (string.IsNullOrEmpty(name)) continue;
                        int need = req.GetAmount(qualityLevel) * multiplier;
                        int missing = need - inv.CountItems(name, -1, false);
                        if (missing <= 0) continue;

                        foreach (Container c in Nearby(__instance))
                        {
                            if (missing <= 0) break;
                            Inventory ci = c != null ? c.GetInventory() : null;
                            if (ci == null) continue;
                            int take = Mathf.Min(missing, ci.CountItems(name, -1, false));
                            if (take <= 0) continue;
                            if (AesirChests.Take(c, name, take)) missing -= take;
                        }
                    }
                    _cachedAt = -10f;   // os baus mudaram: refaz a lista na proxima consulta
                }
                finally { _busy = false; }
            }
        }
    }
}
