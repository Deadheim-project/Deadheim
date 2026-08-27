using System;
using HarmonyLib;
using UnityEngine;

namespace RaidSystem
{
    /// <summary>
    /// Portal de minerio: um portal aceita itens nao-teleportaveis quando o DESTINO dele e
    /// territorio dominado pela guild do jogador.
    ///
    /// A checagem do vanilla e no portal de ENTRADA (TeleportWorld.cs:120), nao no de saida.
    /// Por isso a regra olha o destino — e o unico jeito de "minerio entra no castelo"
    /// funcionar quando o jogador embarca la na mina.
    ///
    /// Efeito colateral desejado: minerio nunca SAI de territorio por portal, entao o tributo
    /// so viaja por terra. A regra "onde tem renda, nao tem portal de saida" vale por
    /// construcao, sem excecao no codigo.
    /// </summary>
    public static class OrePortal
    {
        public static bool AllowsOre(TeleportWorld portal, Player player, ZNetView nview)
        {
            try
            {
                if (portal == null || player == null) return false;
                if (RaidSystemPlugin.OrePortalEnabled.Value != Toggle.On) return false;

                ZDO self = nview != null ? nview.GetZDO() : null;
                if (self == null) return false;

                ZDOID targetId = self.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal);
                if (targetId == ZDOID.None || ZDOMan.instance == null) return false;

                ZDO destination = ZDOMan.instance.GetZDO(targetId);
                if (destination == null) return false;

                Vector3 destinationPos = destination.GetPosition();

                RaidZone zone = Util.GetRaidZoneAt(destinationPos);
                if (zone == null || zone.Tier < RaidSystemPlugin.OrePortalMinTier.Value)
                    return false;

                string owner = Util.GetTerritoryOwner(destinationPos);
                if (string.IsNullOrEmpty(owner)) return false;

                string team = GuildsIntegration.GetPlayerTeam(player);
                return !string.IsNullOrEmpty(team)
                       && string.Equals(team, owner, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RaidSystem] OrePortal check failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// m_allowAllItems e campo de INSTANCIA do MonoBehaviour: mexer nele afeta so aquele
        /// portal na cena, nunca o prefab. Ligamos, deixamos o vanilla usar o portao dele, e
        /// devolvemos no Finalizer — Finalizer e nao Postfix porque o campo tem que voltar
        /// mesmo se o Teleport lancar excecao.
        /// </summary>
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
        public static class TeleportPatch
        {
            private static void Prefix(TeleportWorld __instance, Player player,
                                       ZNetView ___m_nview, out bool __state)
            {
                __state = __instance.m_allowAllItems;
                if (!__state && AllowsOre(__instance, player, ___m_nview))
                    __instance.m_allowAllItems = true;
            }

            private static void Finalizer(TeleportWorld __instance, bool __state)
                => __instance.m_allowAllItems = __state;
        }

        /// <summary>
        /// Mesmo truque no UpdatePortal, que le m_allowAllItems em TeleportWorld.cs:92 para
        /// acender o efeito. Sem isso o portal aceita minerio mas nao mostra que aceita.
        /// </summary>
        [HarmonyPatch(typeof(TeleportWorld), "UpdatePortal")]
        public static class UpdatePortalPatch
        {
            private static void Prefix(TeleportWorld __instance, ZNetView ___m_nview,
                                       out bool __state)
            {
                __state = __instance.m_allowAllItems;
                if (__state || __instance.m_proximityRoot == null) return;

                Player closest = Player.GetClosestPlayer(
                    __instance.m_proximityRoot.position, __instance.m_activationRange);
                if (closest != null && AllowsOre(__instance, closest, ___m_nview))
                    __instance.m_allowAllItems = true;
            }

            private static void Finalizer(TeleportWorld __instance, bool __state)
                => __instance.m_allowAllItems = __state;
        }

        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.GetHoverText))]
        public static class HoverTextPatch
        {
            private static void Postfix(TeleportWorld __instance, ZNetView ___m_nview,
                                        ref string __result)
            {
                Player lp = Player.m_localPlayer;
                if (lp == null) return;
                if (!AllowsOre(__instance, lp, ___m_nview)) return;
                __result += "\n<color=#FFD700>[Aceita minério]</color>";
            }
        }
    }
}
