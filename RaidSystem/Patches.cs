using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RaidSystem
{
    [HarmonyPatch]
    public class Patches
    {
        public static bool hasAwake;
        // Um slot por ward. Antes era um so para o mapa inteiro, entao duas guilds
        // atacando wards diferentes ao mesmo tempo trocavam o credito da conquista.
        private static readonly Dictionary<int, (string pid, string nick, string teamId)> _wardAttackers
            = new Dictionary<int, (string, string, string)>();
        private static readonly HashSet<int> _handledWardDestructions = new HashSet<int>();
        private static readonly HashSet<int> _adminRemovingDoors = new HashSet<int>();

        private static bool IsRaidWard(WearNTear wearNTear)
        {
            if (wearNTear == null) return false;
            GameObject pieceObject = wearNTear.m_piece != null ? wearNTear.m_piece.gameObject : wearNTear.gameObject;
            return pieceObject != null && pieceObject.name.Contains("RaidWard");
        }

        /// <summary>
        /// Roda em quem for dono da ZDO do ward, nao necessariamente no servidor:
        /// WearNTear.Damage faz InvokeRPC sem alvo, e isso vai para o dono da ZDO, que
        /// costuma ser o cliente do proprio atacante. Por isso aqui nao ha checagem de
        /// IsServer: quem detecta a destruicao apenas avisa o servidor, e o servidor
        /// decide a conquista.
        /// </summary>
        private static void HandleWardDestroyed(WearNTear wearNTear, string source)
        {
            if (ZNet.instance == null || ZRoutedRpc.instance == null) return;
            if (!IsRaidWard(wearNTear)) return;

            Vector3 pos = ((Component)wearNTear).transform.position;
            if (!Util.IsRaidEnabledHere(pos)) return;

            // Ids de objetos destruidos nunca voltam, entao o set so cresce. O servidor ja
            // deduplica por posicao numa janela de 10s, entao podar aqui e seguro.
            if (_handledWardDestructions.Count > 128) _handledWardDestructions.Clear();

            int instanceId = wearNTear.GetInstanceID();
            if (!_handledWardDestructions.Add(instanceId)) return;

            Quaternion rot = ((Component)wearNTear).transform.rotation;
            Debug.Log($"[RaidSystem] RaidWard destroyed by {source} at X:{pos.x:F0} Z:{pos.z:F0}; reporting to server.");

            _wardAttackers.TryGetValue(instanceId, out var attacker);
            _wardAttackers.Remove(instanceId);

            ZPackage pkg = new ZPackage();
            pkg.Write(attacker.pid ?? string.Empty);
            pkg.Write(attacker.nick ?? string.Empty);
            pkg.Write(pos);
            pkg.Write(rot);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.instance.GetServerPeerID(), "RaidSystem_WardDestroyed", pkg);
        }

        /// <summary>Catapulta mais proxima do ponto de impacto, para atribuir o tiro sem dono.</summary>
        private static long ResolveSiegeShooter(Vector3 point)
        {
            float best = RaidSystemPlugin.SiegeAttributionRadius.Value;
            long shooter = 0L;
            foreach (Catapult c in UnityEngine.Object.FindObjectsByType<Catapult>(FindObjectsSortMode.None))
            {
                if (c == null) continue;
                float d = Vector3.Distance(c.transform.position, point);
                if (d > best) continue;
                ZDO zdo = c.GetComponent<ZNetView>()?.GetZDO();
                long candidate = zdo != null ? zdo.GetLong("rs_shooter", 0L) : 0L;
                if (candidate == 0L) continue;
                best = d; shooter = candidate;
            }
            return shooter;
        }

        private static bool IsAdminSender(long sender)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return false;

            ZNetPeer peer = ZNet.instance.GetPeer(sender);
            string hostName = peer?.m_socket?.GetHostName();
            return !string.IsNullOrEmpty(hostName) && ZNet.instance.IsAdmin(hostName);
        }

        [HarmonyPatch(typeof(WearNTear), "Destroy")]
        public static class DestroyPatch
        {
            private static bool Prefix(WearNTear __instance)
            {
                if (ZNet.instance == null) return true;
                Vector3 pos = ((Component)__instance).transform.position;

                if (Util.IsRaidEnabledHere(pos) && Util.IsRaidDoorOrGate(__instance.gameObject))
                {
                    if (_adminRemovingDoors.Contains(__instance.GetInstanceID()))
                        return true;

                    if (Util.IsRaidDisabledThisTime(pos))
                        RaidDoorManager.CloseAndRepair(__instance);
                    else
                        RaidDoorManager.Breach(__instance);
                    return false;
                }

                return true;
            }

            private static void Postfix(WearNTear __instance)
            {
                HandleWardDestroyed(__instance, "Destroy");
            }
        }

        [HarmonyPatch(typeof(WearNTear), "RPC_Remove")]
        public static class RPCRemovePatch
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(WearNTear __instance, long sender)
            {
                if (__instance == null || ZNet.instance == null || !ZNet.instance.IsServer()) return true;

                Vector3 pos = ((Component)__instance).transform.position;
                if (!Util.IsRaidEnabledHere(pos) || !Util.IsRaidDoorOrGate(__instance.gameObject)) return true;

                if (!IsAdminSender(sender))
                    return false;

                _adminRemovingDoors.Add(__instance.GetInstanceID());
                return true;
            }

            private static void Finalizer(WearNTear __instance)
            {
                if (__instance != null)
                    _adminRemovingDoors.Remove(__instance.GetInstanceID());
            }
        }

        [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
        public static class RPCDamagePatch
        {
            [HarmonyPriority(0)]
            private static bool Prefix(WearNTear __instance, ref HitData hit, ZNetView ___m_nview)
            {
                try
                {
                    // return false aqui bloquearia o dano; sem nview o certo e deixar o vanilla decidir.
                    if (___m_nview == null) return true;
                    Vector3 pos = ((Component)__instance).transform.position;
                    bool inRaidZone = Util.IsRaidEnabledHere(pos);
                    bool isWard = __instance.gameObject.name.Contains("RaidWard");
                    bool isDoorOrGate = Util.IsRaidDoorOrGate(__instance.gameObject);

                    // Track attacker for conquest detection. Sem IsServer: isto roda no dono
                    // da ZDO, que normalmente e o cliente do atacante, nao o servidor.
                    if (isWard)
                    {
                        Player attacker = hit.GetAttacker() as Player;
                        if (attacker != null)
                        {
                            _wardAttackers[__instance.GetInstanceID()] = (
                                attacker.GetPlayerID().ToString(),
                                attacker.m_nview.GetZDO().GetString("playerName"),
                                GuildsIntegration.GetPlayerTeam(attacker));
                        }
                        else if (hit.m_hitType == HitData.HitType.Catapult)
                        {
                            // Tiro de catapulta chega sem atacante; o operador vem do carimbo
                            // que CatapultShootPatch deixou na ZDO da maquina mais proxima.
                            long shooter = ResolveSiegeShooter(pos);
                            if (shooter != 0L)
                            {
                                Player shooterPlayer = Player.GetPlayer(shooter);
                                _wardAttackers[__instance.GetInstanceID()] = (
                                    shooter.ToString(),
                                    shooterPlayer != null ? shooterPlayer.GetPlayerName() : string.Empty,
                                    GuildsIntegration.GetPlayerTeam(shooter));
                            }
                        }
                    }

                    if (!inRaidZone) return true;
                    if (Util.IsRaidDisabledThisTime(pos)) return false;
                    if (!isWard && !isDoorOrGate) return false;

                    if (isDoorOrGate) return true;

                    bool siege = hit.m_hitType == HitData.HitType.Catapult;

                    if (RaidSystemPlugin.LogWardHits.Value == Toggle.On)
                        Debug.Log($"[RaidSystem] Ward hit: hitType={hit.m_hitType} toolTier={hit.m_toolTier} " +
                                  $"dano={hit.GetTotalDamage():F1} siege={siege}");

                    if (RaidSystemPlugin.SiegeOnly.Value == Toggle.On && !siege)
                        return false;   // ward so cai por cerco

                    RaidZone zone = Util.GetRaidZoneAt(pos);
                    if (!siege && zone != null && hit.m_toolTier < zone.MinToolTier)
                        return false;   // ferramenta fraca demais para este territorio

                    float reduction = siege
                        ? RaidSystemPlugin.WardReductionDamageSiege.Value
                        : RaidSystemPlugin.WardReductionDamage.Value;

                    hit.ApplyModifier(1f - reduction / 100f);
                    return true;
                }
                catch (Exception ex)
                {
                    // Falhar aqui com return false tornava a estrutura invulneravel em silencio.
                    Debug.LogError("[RaidSystem] RPC_Damage patch failed: " + ex.Message + " - " + ex.StackTrace);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(WearNTear), "ApplyDamage")]
        public static class WearNTearApplyDamagePatch
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(WearNTear __instance, float damage, ref bool __result)
            {
                try
                {
                    if (__instance == null || damage <= 0f) return true;
                    Vector3 pos = ((Component)__instance).transform.position;
                    if (!Util.IsRaidEnabledHere(pos)) return true;

                    if (IsRaidWard(__instance))
                    {
                        if (Util.IsRaidDisabledThisTime(pos))
                        {
                            __result = true;
                            return false;
                        }

                        float wardHealth = RaidDoorManager.GetHealth(__instance);
                        if (wardHealth - damage <= 0f)
                            HandleWardDestroyed(__instance, "lethal damage");

                        return true;
                    }

                    if (!Util.IsRaidDoorOrGate(__instance.gameObject)) return true;

                    if (Util.IsRaidDisabledThisTime(pos))
                    {
                        RaidDoorManager.CloseAndRepair(__instance);
                        __result = true;
                        return false;
                    }

                    float health = RaidDoorManager.GetHealth(__instance);
                    if (health - damage > 0f) return true;

                    RaidDoorManager.Breach(__instance);
                    __result = true;
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[RaidSystem] Door lethal damage patch failed: " + ex.Message);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        private static class ZNetOnNewConnectionPatch
        {
            public static void Postfix(ZNet __instance)
            {
                if (__instance.IsServer()) return;
                RaidSystemPlugin.SteamId = ZNet.GetUID().ToString();
            }
        }

        [HarmonyPatch(typeof(Game), "Logout")]
        public static class LogoutPatch
        {
            private static void Postfix()
            {
                hasAwake = false;
                _wardAttackers.Clear();
                _handledWardDestructions.Clear();
                _adminRemovingDoors.Clear();
            }
        }

        [HarmonyPatch(typeof(Player), "OnSpawned")]
        public static class OnSpawnedPatch
        {
            private static void Postfix()
            {
                if (hasAwake) return;
                hasAwake = true;
                ZRoutedRpc.instance.InvokeRoutedRPC(
                    ZRoutedRpc.instance.GetServerPeerID(),
                    "RaidSystem_RequestFullSync",
                    new ZPackage());
            }
        }

        [HarmonyPatch(typeof(Player), "CheckCanRemovePiece")]
        public static class CheckCanRemovePiecePatch
        {
            [HarmonyPriority(0)]
            private static bool Prefix(Piece piece)
                => SynchronizationManager.Instance.PlayerIsAdmin
                   || !Util.IsRaidEnabledHere(((Component)piece).transform.position);
        }

        [HarmonyPatch(typeof(Player), "PlacePiece")]
        public static class NoBuildPatch
        {
            [HarmonyPriority(800)]
            private static bool Prefix(Piece piece, Player __instance)
            {
                if (SynchronizationManager.Instance.PlayerIsAdmin) return true;

                if (piece != null && piece.gameObject.name.Contains("RaidWard")
                    && RaidSystemPlugin.WardOnlyAdminCanBuild.Value == Toggle.On)
                {
                    __instance.Message(MessageHud.MessageType.Center, "Somente admins podem colocar a Raid Ward.");
                    return false;
                }

                if (Util.IsRaidEnabledHere(((Component)__instance).transform.position))
                {
                    __instance.Message(MessageHud.MessageType.Center, "Nao e possivel construir em area de raid.");
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Door), "Interact")]
        public static class DoorInteractPatch
        {
            private static bool Prefix(Door __instance, Humanoid character)
            {
                try
                {
                    if (!Util.IsRaidEnabledHere(__instance.transform.position)) return true;

                    Player player = character as Player;
                    if (player == null) return true;
                    if (Util.PlayerOwnsTerritory(player, __instance.transform.position)) return true;

                    string owner = Util.GetTerritoryOwner(__instance.transform.position);
                    player.Message(MessageHud.MessageType.Center, string.IsNullOrEmpty(owner)
                        ? "Territorio sem dono."
                        : $"Acesso restrito a guild dominante: {owner}");
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[RaidSystem] Door access check failed: " + ex.Message);
                    return true;
                }
            }
        }

        /// <summary>
        /// Liga o PvP dentro do pvpRadius de uma zona. O campo era lido da config e nunca
        /// consultado; agora vale, mas so com Force PvP In Zones ligado (Off por padrao).
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.IsPVPEnabled))]
        public static class ForcePvpPatch
        {
            private static void Postfix(Player __instance, ref bool __result)
            {
                if (__result || __instance == null) return;
                if (Util.IsInPvpZone(__instance.transform.position)) __result = true;
            }
        }

        /// <summary>
        /// Catapult.ShootProjectile faz Setup(null, ...): o projetil nao tem dono, entao um ward
        /// derrubado por catapulta chegaria em RPC_Damage sem atacante e ninguem conquistaria.
        /// Shoot() roda no cliente de quem operou, entao aqui sabemos quem foi.
        /// </summary>
        [HarmonyPatch(typeof(Catapult), "Shoot")]
        public static class CatapultShootPatch
        {
            private static void Postfix(Catapult __instance)
            {
                Player lp = Player.m_localPlayer;
                if (lp == null) return;
                ZNetView nview = __instance.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
                nview.GetZDO().Set("rs_shooter", lp.GetPlayerID());
            }
        }

        /// <summary>
        /// RaidWard nao abre o menu de permissoes do guard_stone: interagir com ele resgata tributo.
        /// O guard por nome de prefab e obrigatorio - sem ele isso valeria para toda ward do servidor.
        /// </summary>
        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Interact))]
        public static class RaidWardInteractPatch
        {
            private static bool Prefix(PrivateArea __instance, Humanoid human, bool hold, bool alt)
            {
                if (hold) return true;
                if (Util.CleanPrefabName(__instance.gameObject.name) != "RaidWard") return true;

                Player player = human as Player;
                if (player == null) return true;

                int free = player.GetInventory().GetEmptySlots();
                if (free < RaidSystemPlugin.TributeRequiredFreeSlots.Value)
                {
                    player.Message(MessageHud.MessageType.Center,
                        $"Libere pelo menos {RaidSystemPlugin.TributeRequiredFreeSlots.Value} espaços na mochila.");
                    return false;
                }

                ZPackage pkg = new ZPackage();
                pkg.Write(__instance.transform.position);
                ZRoutedRpc.instance.InvokeRoutedRPC(
                    ZRoutedRpc.instance.GetServerPeerID(), "RaidSystem_ClaimTribute", pkg);
                return false;
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.GetHoverText))]
        public static class RaidWardHoverPatch
        {
            private static void Postfix(PrivateArea __instance, ref string __result)
            {
                if (Util.CleanPrefabName(__instance.gameObject.name) != "RaidWard") return;

                Vector3 pos = __instance.transform.position;
                TerritoryInfo t = Util.GetTerritoryAt(pos);
                string owner = string.IsNullOrEmpty(t?.OwnerTeamId) ? "sem dono" : t.OwnerTeamId;
                int pending = t?.PendingTribute ?? 0;

                __result = $"Raid Ward\nDomínio: {owner}\nTributo pendente: {pending}\n" +
                           "[<color=yellow><b>$KEY_Use</b></color>] resgatar";
            }
        }

        [HarmonyPatch(typeof(Character), "ApplyDamage")]
        public static class ApplyDamagePatch
        {
            public static void Postfix(Character __instance, HitData hit)
            {
                try
                {
                    if (__instance.GetHealth() > 0f
                        || hit.GetAttacker() == null
                        || !hit.GetAttacker().IsPlayer()
                        || !__instance.IsPlayer()) return;
                    if (!ZNet.instance.IsServer()) return;

                    Player killer = (Player)hit.GetAttacker();
                    Player dead = (Player)__instance;
                    string killerTeam = GuildsIntegration.GetPlayerTeam(killer);
                    string deadTeam = GuildsIntegration.GetPlayerTeam(dead);
                    if (string.IsNullOrEmpty(killerTeam) || string.IsNullOrEmpty(deadTeam)
                        || killerTeam == deadTeam) return;

                    // Points Per Defense existia na config mas nada registrava defesa.
                    // Defesa = abate dentro de territorio que a guild do matador ja domina.
                    string holder = Util.GetTerritoryOwner(((Component)__instance).transform.position);
                    bool defended = !string.IsNullOrEmpty(holder)
                                    && string.Equals(holder, killerTeam, StringComparison.OrdinalIgnoreCase);

                    ScoreManager.RecordPvpOutcome(
                        killer.GetPlayerID().ToString(),
                        killer.m_nview.GetZDO().GetString("playerName"),
                        killerTeam,
                        dead.GetPlayerID().ToString(),
                        dead.m_nview.GetZDO().GetString("playerName"),
                        deadTeam,
                        defended);

                    dWebHook.SendRaidMessage(
                        $"**[Abate]** **{killer.m_nview.GetZDO().GetString("playerName")}** [{killerTeam}] eliminou " +
                        $"**{dead.m_nview.GetZDO().GetString("playerName")}** [{deadTeam}].\n" +
                        ScoreManager.FormatLeaderboardForWebhook());
                }
                catch (Exception ex) { Debug.Log("ApplyDamage error: " + ex.Message); }
            }
        }
    }
}
