using HarmonyLib;
using Deadheim.Vanilla;
using System;
using System.Collections.Generic;
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
        /// que nao devem ficar presos atras de ward: so o ward que cobre a peca mirada. Antes
        /// mirar uma porta de dungeon desligava todos os wards daquele cliente, inclusive no
        /// GetProtectingWard do dano que ele processa como dono das pecas (B16).
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
                Component dungeonPiece = hovered is Door door && IsDungeonPiece(door.gameObject) ? door
                    : hovered is Container container && IsDungeonPiece(container.gameObject) ? container
                    : null;
                if (dungeonPiece != null && __instance.IsInside(dungeonPiece.transform.position, 0f)) __result = false;
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

                    // Zona segura: nada protegido por ward toma dano perto da origem (SafeArea) nem
                    // numa zona segura do PvP (ilha inicial, SafeZones). Fora dela a base e
                    // raidavel e toma DamagePercent do dano.
                    // A versao antiga media a distancia do jogador local, o que estourava
                    // NullReference no servidor dedicado e bloqueava dano no mundo inteiro.
                    if (Utils.DistanceXZ(pos, Vector3.zero) <= Plugin.SafeArea.Value || Pvp.PvpZones.IsSafeArea(pos))
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
                    if (Admin.LocalPlayerIsAdmin()) return true;

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

                    // Limite 0 = sem limite: a base e raidavel, entao o limite nao protege nada.
                    int limit = WardCore.GetWardLimit();
                    if (limit > 0 && Plugin.PlayerWardCount < 999 && Plugin.PlayerWardCount >= limit)
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
                    if (Admin.LocalPlayerIsAdmin()) return true;

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
                    if (Admin.LocalPlayerIsAdmin()) return true;

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
                    if (Admin.LocalPlayerIsAdmin()) return true;

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
                    if (hit == null) return true;

                    bool plant = __instance.GetComponent<Plant>() != null || __instance.GetComponent<Pickable>() != null;
                    if (plant)
                    {
                        if (!WardProfiles.ProtectPlants.Value) return true;
                        return !WardCore.IsBlocked(__instance.transform.position, hit.GetAttacker() as Player,
                            "Plantacao protegida por um ward.");
                    }

                    // Pedra pequena, toco, arbusto: Destructible que nao e peca construida.
                    return !NatureBlocked(__instance.transform.position, hit, __instance.gameObject);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Wards] Patch de destruicao de planta falhou: " + ex.Message);
                    return true;
                }
            }
        }

        // ---------------------------------------------------------- pedra e arvore

        /// <summary>
        /// Pedra, minerio, arvore e tronco dentro do ward alheio. Roda no cliente de quem
        /// bate (IDestructible.Damage), antes do RPC: o golpe nem sai. Usa o ponto do golpe,
        /// nao o centro do objeto, porque rocha grande (MineRock5) passa da borda do ward.
        /// </summary>
        internal static bool NatureBlocked(Vector3 fallback, HitData hit, GameObject target = null)
        {
            if (!WardProfiles.ProtectNature.Value || hit == null) return false;
            if (!(hit.GetAttacker() is Player attacker) || attacker != Player.m_localPlayer) return false;
            if (Admin.LocalPlayerIsAdmin()) return false;
            // Veio de minerio fica livre (ProtectNatureOres=false): senao uma guilda fecharia
            // os veios do mapa com wards de 150 m que ninguem derruba fora do castelo.
            if (!WardProfiles.ProtectNatureOres.Value && IsOreVein(target)) return false;

            Vector3 point = hit.m_point != Vector3.zero ? hit.m_point : fallback;
            PrivateArea ward = WardCore.GetProtectingWard(point, attacker);
            if (ward == null) return false;
            float limit = WardProfiles.ProtectNatureRadius.Value;
            if (limit > 0f && Utils.DistanceXZ(point, ward.transform.position) > limit) return false;

            ward.FlashShield(false);
            attacker.Message(MessageHud.MessageType.Center, "Pedras e arvores protegidas por um ward.");
            return true;
        }

        private static string _oreDropsRaw;
        private static HashSet<string> _oreDrops = new HashSet<string>();

        /// <summary>Solta algum item de NatureOreDrops? (MineRock, MineRock5 ou DropOnDestroyed)</summary>
        internal static bool IsOreVein(GameObject target)
        {
            if (target == null) return false;
            string raw = WardProfiles.NatureOreDrops.Value ?? string.Empty;
            if (raw != _oreDropsRaw)
            {
                _oreDropsRaw = raw;
                _oreDrops = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string name in raw.Split(','))
                    if (name.Trim().Length > 0) _oreDrops.Add(name.Trim());
            }
            if (_oreDrops.Count == 0) return false;

            MineRock rock = target.GetComponent<MineRock>();
            if (rock != null && Drops(rock.m_dropItems)) return true;
            MineRock5 vein = target.GetComponent<MineRock5>();
            if (vein != null && Drops(vein.m_dropItems)) return true;
            DropOnDestroyed drop = target.GetComponent<DropOnDestroyed>();
            return drop != null && Drops(drop.m_dropWhenDestroyed);
        }

        private static bool Drops(DropTable table)
        {
            if (table?.m_drops == null) return false;
            foreach (DropTable.DropData data in table.m_drops)
                if (data.m_item != null && _oreDrops.Contains(data.m_item.name)) return true;
            return false;
        }

        [HarmonyPatch(typeof(MineRock), "Damage")]
        public static class MineRockPatch
        {
            private static bool Prefix(MineRock __instance, HitData hit)
                => !NatureBlocked(__instance.transform.position, hit, __instance.gameObject);
        }

        [HarmonyPatch(typeof(MineRock5), "Damage")]
        public static class MineRock5Patch
        {
            private static bool Prefix(MineRock5 __instance, HitData hit)
                => !NatureBlocked(__instance.transform.position, hit, __instance.gameObject);
        }

        [HarmonyPatch(typeof(TreeBase), "Damage")]
        public static class TreeBasePatch
        {
            private static bool Prefix(TreeBase __instance, HitData hit) => !NatureBlocked(__instance.transform.position, hit);
        }

        [HarmonyPatch(typeof(TreeLog), "Damage")]
        public static class TreeLogPatch
        {
            private static bool Prefix(TreeLog __instance, HitData hit) => !NatureBlocked(__instance.transform.position, hit);
        }
    }
}
