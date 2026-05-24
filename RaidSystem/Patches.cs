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
        private static (string pid, string nick, string teamId) _lastWardAttacker;
        private static readonly HashSet<int> _handledWardDestructions = new HashSet<int>();
        private static readonly HashSet<int> _adminRemovingDoors = new HashSet<int>();

        private static bool IsRaidWard(WearNTear wearNTear)
        {
            if (wearNTear == null) return false;
            GameObject pieceObject = wearNTear.m_piece != null ? wearNTear.m_piece.gameObject : wearNTear.gameObject;
            return pieceObject != null && pieceObject.name.Contains("RaidWard");
        }

        private static void HandleWardDestroyed(WearNTear wearNTear, string source)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            if (!IsRaidWard(wearNTear)) return;

            Vector3 pos = ((Component)wearNTear).transform.position;
            if (!Util.IsRaidEnabledHere(pos)) return;

            int instanceId = wearNTear.GetInstanceID();
            if (!_handledWardDestructions.Add(instanceId)) return;

            Quaternion rot = ((Component)wearNTear).transform.rotation;
            Debug.Log($"[RaidSystem] RaidWard destroyed by {source} at X:{pos.x:F0} Z:{pos.z:F0}; scheduling respawn.");

            if (_lastWardAttacker.pid != null && !string.IsNullOrEmpty(_lastWardAttacker.teamId))
                RPCManager.HandleConquest(_lastWardAttacker.pid, _lastWardAttacker.nick, _lastWardAttacker.teamId, pos);
            else
                Debug.LogWarning("[RaidSystem] RaidWard destroyed without a valid guild attacker; respawning without conquest.");

            _lastWardAttacker = default;
            Util.RespawnWard(pos, rot);
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
                    if (___m_nview == null) return false;
                    Vector3 pos = ((Component)__instance).transform.position;
                    bool inRaidZone = Util.IsRaidEnabledHere(pos);
                    bool isWard = __instance.gameObject.name.Contains("RaidWard");
                    bool isDoorOrGate = Util.IsRaidDoorOrGate(__instance.gameObject);

                    // Track last attacker for conquest detection
                    if (isWard && ZNet.instance.IsServer())
                    {
                        Player attacker = hit.GetAttacker() as Player;
                        if (attacker != null)
                        {
                            _lastWardAttacker = (
                                attacker.GetPlayerID().ToString(),
                                attacker.m_nview.GetZDO().GetString("playerName"),
                                GuildsIntegration.GetPlayerTeam(attacker));
                        }
                    }

                    if (!inRaidZone) return true;
                    if (Util.IsRaidDisabledThisTime(pos)) return false;
                    if (!isWard && !isDoorOrGate) return false;

                    if (isDoorOrGate) return true;

                    hit.ApplyModifier(1f - RaidSystemPlugin.WardReductionDamage.Value / 100f);
                    return true;
                }
                catch (Exception ex) { Debug.LogError(ex.Message + " - " + ex.StackTrace); return false; }
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
                _lastWardAttacker = default;
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
                => SynchronizationManager.Instance.PlayerIsAdmin
                   || !Util.IsRaidEnabledHere(((Component)__instance).transform.position);
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

                    ScoreManager.RecordKill(
                        killer.GetPlayerID().ToString(),
                        killer.m_nview.GetZDO().GetString("playerName"),
                        killerTeam);
                    ScoreManager.RecordDeath(
                        dead.GetPlayerID().ToString(),
                        dead.m_nview.GetZDO().GetString("playerName"),
                        deadTeam);

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
