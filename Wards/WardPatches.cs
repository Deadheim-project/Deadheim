using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Deadheim.Wards
{
    /// <summary>
    /// Conjunto unico de patches de ward. Um patch por metodo do jogo.
    ///
    /// Antes o mesmo metodo era patcheado em varios lugares ao mesmo tempo:
    /// Player.PlacePiece tinha patch em Ward.cs, RaidSystem/Patches.cs e PlayerWardPatches.cs;
    /// WearNTear.RPC_Damage tinha tres; PrivateArea.IsEnabled e Awake tinham dois cada.
    /// </summary>
    [HarmonyPatch]
    public static class WardPatches
    {
        private static bool IsDungeonPiece(GameObject go)
        {
            if (go == null) return false;
            string name = WardProfiles.CleanName(go);
            return Plugin.DungeonPrefabs.Value.Split(',').Any(p => p.Trim() == name);
        }

        // ---------------------------------------------------------------- ciclo de vida

        /// <summary>
        /// Raio, combustivel inicial e guild do dono.
        /// Substitui CraftingStations.PrivateAreaAwake, que cuidava do raio a parte.
        /// </summary>
        [HarmonyPatch(typeof(PrivateArea), "Awake")]
        public static class AwakePatch
        {
            private static void Postfix(PrivateArea __instance)
            {
                try
                {
                    WardProfile profile = WardProfiles.For(__instance);
                    if (profile == null) return;

                    float radius = profile.ResolveRadius();
                    __instance.m_radius = radius;
                    if (__instance.m_areaMarker != null) __instance.m_areaMarker.m_radius = radius;

                    WardCore.InitFuel(__instance);
                    WardCore.StampGuild(__instance);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Wards] Awake falhou: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Ward recem colocado: SetCreator roda depois do Awake e e o primeiro momento
        /// em que da para saber de quem ele e.
        /// </summary>
        [HarmonyPatch(typeof(Piece), "SetCreator")]
        public static class SetCreatorPatch
        {
            private static void Postfix(Piece __instance)
            {
                if (!WardProfiles.IsWard(__instance.gameObject)) return;
                WardCore.StampGuild(__instance.GetComponent<PrivateArea>());
            }
        }

        // ------------------------------------------------------------------- acesso

        /// <summary>Membro da guild dona entra em tudo que o vanilla ja checa via PrivateArea.</summary>
        [HarmonyPatch(typeof(PrivateArea), "IsPermitted")]
        public static class IsPermittedPatch
        {
            private static void Postfix(PrivateArea __instance, long playerID, ref bool __result)
            {
                if (__result) return;
                if (WardCore.HasGuildAccess(__instance, playerID)) __result = true;
            }
        }

        /// <summary>
        /// Ward sem combustivel para de proteger. Tambem libera portas e baus de dungeon,
        /// que nao devem ficar presos atras de ward.
        /// </summary>
        [HarmonyPatch(typeof(PrivateArea), "IsEnabled")]
        public static class IsEnabledPatch
        {
            private static void Postfix(PrivateArea __instance, ref bool __result)
            {
                if (!__result) return;

                if (!WardCore.HasFuel(__instance))
                {
                    __result = false;
                    return;
                }

                Player player = Player.m_localPlayer;
                if (player == null || !player.m_hovering) return;

                Interactable hovered = player.m_hovering.GetComponentInParent<Interactable>();
                if (hovered is Door door && IsDungeonPiece(door.gameObject)) __result = false;
                else if (hovered is Container container && IsDungeonPiece(container.gameObject)) __result = false;
            }
        }

        [HarmonyPatch(typeof(Door), nameof(Door.CanInteract))]
        public static class DoorCanInteractPatch
        {
            private static void Postfix(Door __instance, ref bool __result)
            {
                if (IsDungeonPiece(__instance.gameObject)) __result = true;
            }
        }

        // -------------------------------------------------------------- combustivel

        /// <summary>
        /// Abastecer: o jogador usa o item de combustivel olhando para o ward.
        /// PrivateArea.UseItem devolve false no vanilla, entao o gancho estava livre.
        /// </summary>
        [HarmonyPatch(typeof(PrivateArea), "UseItem")]
        public static class UseItemPatch
        {
            private static void Postfix(PrivateArea __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
            {
                if (__result) return;
                if (WardCore.AddFuel(__instance, user, item)) __result = true;
            }
        }

        /// <summary>
        /// Combustivel no hover. O vanilla ja monta nome, dono e lista de permitidos,
        /// entao aqui so entra o que falta, em vez de reescrever o texto inteiro.
        /// </summary>
        [HarmonyPatch(typeof(PrivateArea), "GetHoverText")]
        public static class GetHoverTextPatch
        {
            private static void Postfix(PrivateArea __instance, ref string __result)
            {
                if (!WardCore.UsesFuel(__instance)) return;

                float fuel = WardCore.GetFuel(__instance);
                StringBuilder text = new StringBuilder(__result ?? string.Empty);
                text.Append("\nCombustivel: " + Math.Round(fuel, 2) + "/" + Mathf.FloorToInt(WardCore.MaxFuel));
                text.Append(fuel <= 0f
                    ? " <color=red>(desligado)</color>"
                    : "\n[Use " + WardProfiles.FuelItem.Value + " para abastecer]");
                __result = text.ToString();
            }
        }

        // --------------------------------------------------------------------- dano

        /// <summary>
        /// Percentual de dano do ward e de tudo que ele cobre. 0% = invulneravel.
        /// Roda em Priority.Low para nao atropelar o patch de zona de raid do RaidSystem.
        /// </summary>
        [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
        public static class DamagePatch
        {
            [HarmonyPriority(Priority.Low)]
            private static bool Prefix(WearNTear __instance, ref HitData hit)
            {
                try
                {
                    if (__instance == null || hit == null) return true;

                    Vector3 pos = __instance.transform.position;
                    Player attacker = hit.GetAttacker() as Player;
                    PrivateArea ward = WardCore.GetProtectingWard(pos, attacker);
                    if (ward == null) return true;

                    // Zona segura do mundo: nada protegido por ward toma dano perto da origem.
                    // A versao antiga media a distancia do jogador local, o que estourava
                    // NullReference no servidor dedicado e bloqueava dano no mundo inteiro.
                    if (Utils.DistanceXZ(pos, Vector3.zero) <= Plugin.SafeArea.Value)
                    {
                        ward.FlashShield(false);
                        return false;
                    }

                    float percent = Mathf.Clamp(WardProfiles.DamagePercent.Value, 0f, 100f);
                    if (percent <= 0f)
                    {
                        ward.FlashShield(false);
                        return false;
                    }

                    hit.ApplyModifier(percent / 100f);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Wards] Patch de dano falhou: " + ex.Message);
                    return true;
                }
            }
        }

        // ------------------------------------------------------- limite e espacamento

        /// <summary>
        /// Limite de wards por jogador e distancia minima do ward alheio.
        /// Construir dentro do ward de outro ja e barrado pelo vanilla via PrivateArea,
        /// que agora enxerga guild por causa do IsPermittedPatch.
        /// </summary>
        [HarmonyPatch(typeof(Player), "PlacePiece")]
        public static class PlacePiecePatch
        {
            private static bool Prefix(Piece piece, Player __instance)
            {
                try
                {
                    if (piece == null || __instance == null) return true;

                    WardProfile profile = WardProfiles.For(piece.gameObject);
                    if (profile == null || !profile.CountsToLimit) return true;
                    if (SynchronizationManager.Instance.PlayerIsAdmin) return true;

                    Vector3 pos = __instance.m_placementGhost != null
                        ? __instance.m_placementGhost.transform.position
                        : __instance.transform.position;

                    if (WardBridge.Governed(pos))
                    {
                        __instance.Message(MessageHud.MessageType.Center, "Nao e possivel colocar ward em zona de raid.");
                        return false;
                    }

                    if (WardCore.HasWardTooClose(pos, __instance, out PrivateArea blocking))
                    {
                        blocking.FlashShield(false);
                        __instance.Message(MessageHud.MessageType.Center, "Muito perto do ward de outro jogador.");
                        return false;
                    }

                    int limit = WardCore.GetWardLimit();
                    if (Plugin.PlayerWardCount < 999 && Plugin.PlayerWardCount >= limit)
                    {
                        __instance.Message(MessageHud.MessageType.Center, "Limite de wards atingido (" + limit + ").");
                        return false;
                    }

                    Minimap.instance?.AddPin(pos, Minimap.PinType.Boss, "WARD", true, false);
                    WardCore.RequestWardCount();
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Wards] Patch de colocacao falhou: " + ex.Message);
                    return true;
                }
            }
        }

        // -------------------------------------------- terraformacao (picareta e hoe)

        /// <summary>
        /// TerrainOp.Awake e o ponto unico por onde passam picareta, hoe e cultivador:
        /// os tres criam um TerrainOp cujo Awake aplica a operacao no heightmap.
        /// </summary>
        [HarmonyPatch(typeof(TerrainOp), "Awake")]
        public static class TerrainPatch
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(TerrainOp __instance)
            {
                try
                {
                    if (!WardProfiles.ProtectTerrain.Value) return true;
                    if (SynchronizationManager.Instance.PlayerIsAdmin) return true;

                    if (!WardCore.IsBlocked(__instance.transform.position, Player.m_localPlayer,
                            "Terreno protegido por um ward.")) return true;

                    UnityEngine.Object.Destroy(__instance.gameObject);
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Wards] Patch de terreno falhou: " + ex.Message);
                    return true;
                }
            }
        }

        // ------------------------------------------------------------ nome de portal

        [HarmonyPatch(typeof(TeleportWorld), "SetText")]
        public static class PortalNamePatch
        {
            private static bool Prefix(TeleportWorld __instance)
            {
                try
                {
                    if (!WardProfiles.ProtectPortals.Value) return true;
                    if (SynchronizationManager.Instance.PlayerIsAdmin) return true;

                    return !WardCore.IsBlocked(__instance.transform.position, Player.m_localPlayer,
                        "Portal protegido por um ward.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Wards] Patch de portal falhou: " + ex.Message);
                    return true;
                }
            }
        }

        // ----------------------------------------------------------------- plantacao

        [HarmonyPatch(typeof(Pickable), "Interact")]
        public static class PickablePatch
        {
            private static bool Prefix(Pickable __instance, Humanoid character)
            {
                try
                {
                    if (!WardProfiles.ProtectPlants.Value) return true;
                    if (SynchronizationManager.Instance.PlayerIsAdmin) return true;

                    return !WardCore.IsBlocked(__instance.transform.position, character as Player,
                        "Plantacao protegida por um ward.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Wards] Patch de colheita falhou: " + ex.Message);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Destructible), "Damage")]
        public static class DestructiblePatch
        {
            private static bool Prefix(Destructible __instance, HitData hit)
            {
                try
                {
                    if (!WardProfiles.ProtectPlants.Value || hit == null) return true;

                    // So plantacao: pedra, arvore e minerio seguem livres.
                    if (__instance.GetComponent<Plant>() == null
                        && __instance.GetComponent<Pickable>() == null) return true;

                    return !WardCore.IsBlocked(__instance.transform.position, hit.GetAttacker() as Player,
                        "Plantacao protegida por um ward.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Wards] Patch de destruicao de planta falhou: " + ex.Message);
                    return true;
                }
            }
        }
    }
}
