using HarmonyLib;
using Jotunn.Managers;
using Splatform;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static Deadheim.Plugin;

namespace Deadheim
{
    [HarmonyPatch]
    public class Patches : MonoBehaviour
    {

        [HarmonyPatch(typeof(Player), "SetPlayerID")]
        internal class SetPlayerID
        {
            private static void Postfix(long playerID, string name)
            {
                try
                {
                    if (!Player.m_localPlayer) return;
                    steamId = ((IUser)PlatformManager.DistributionPlatform.LocalUser).PlatformUserID.m_userID;
                    Plugin.PlayerName = Player.m_localPlayer.m_nview.GetZDO().GetString("playerName");

                    Debug.Log("steamId: " + steamId);
                }
                catch
                {

                }
            }
        }              

        [HarmonyPatch(typeof(Chat), "OnNewChatMessage")]
        internal class OnNewChatMessage
        {
            private static bool Prefix(string text)
            {
                if (text.ToLower().Contains("i have arrived")) return false;
                if (text.ToLower().Contains("i have arrived")) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "HaveSeenTutorial")]
        public class Player_HaveSeenTutorial_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(Player __instance, ref string name)
            {
                if (!__instance.m_shownTutorials.Contains(name))
                {
                    __instance.m_shownTutorials.Add(name);
                }
            }
        }

        [HarmonyPatch(typeof(SE_Stats), nameof(SE_Stats.Setup))]
        public static class SE_Stats_Setup_Patch
        {
            private static void Postfix(ref SE_Stats __instance)
            {
                int meginjordbuff = 200;
                if (__instance.m_addMaxCarryWeight > 0)
                    __instance.m_addMaxCarryWeight = (__instance.m_addMaxCarryWeight - 150) + meginjordbuff;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Game), "Update")]
        private static void GameUpdate()
        {
            if (Player.m_localPlayer)
            {
                InventoryGui.instance.m_pvp.interactable = false;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        public static class PlayerUpdate
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Player __instance)
            {
                if (Plugin.StaffMessage.Value != "") Player.m_localPlayer.Message(MessageHud.MessageType.Center, Plugin.StaffMessage.Value);
            }
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.GetOtherPublicPlayers))]
        private class AdminGetOtherPublicPlayers
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(List<ZNet.PlayerInfo> playerList)
            {
                if (!SynchronizationManager.Instance.PlayerIsAdmin || ZNet.instance == null || playerList == null) return;

                string localName = Player.m_localPlayer?.m_nview?.GetZDO()?.GetString("playerName") ?? "";
                foreach (ZNet.PlayerInfo player in ZNet.instance.GetPlayerList())
                {
                    if (string.IsNullOrWhiteSpace(player.m_name)) continue;
                    if (player.m_name == localName) continue;
                    if (playerList.Exists(existing => existing.m_name == player.m_name)) continue;
                    playerList.Add(player);
                }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        public static void Awake_Postfix(ref Player __instance)
        {
            if (ZRoutedRpc.instance == null)
                return;

            ItemService.SetWardFirePlace();

            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "Sync", new ZPackage());
        }

        [HarmonyPatch(typeof(Player), nameof(Player.EdgeOfWorldKill))]
        [HarmonyPrefix]
        public static bool EdgeOfWorldKill()
        {
            return false;
        }


        [HarmonyPatch(typeof(CraftingStation), "Start")]
        public static class WorkbenchRangeIncrease
        {
            public static void Prefix(ref CraftingStation __instance, ref float ___m_rangeBuild, GameObject ___m_areaMarker)
            {
                try
                {
                    ___m_rangeBuild = 30;
                    ___m_areaMarker.GetComponent<CircleProjector>().m_radius = ___m_rangeBuild;
                }
                catch
                {
                }
            }
        }

        public static void setDeathStat(HitData ___m_lastHit)
        {
            switch (___m_lastHit.m_hitType)
            {
                case HitData.HitType.Undefined:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByUndefined, 1f);
                    break;
                case HitData.HitType.EnemyHit:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByEnemyHit, 1f);
                    break;
                case HitData.HitType.PlayerHit:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByPlayerHit, 1f);
                    break;
                case HitData.HitType.Fall:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByFall, 1f);
                    break;
                case HitData.HitType.Drowning:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByDrowning, 1f);
                    break;
                case HitData.HitType.Burning:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByBurning, 1f);
                    break;
                case HitData.HitType.Freezing:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByFreezing, 1f);
                    break;
                case HitData.HitType.Poisoned:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByPoisoned, 1f);
                    break;
                case HitData.HitType.Water:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByWater, 1f);
                    break;
                case HitData.HitType.Smoke:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathBySmoke, 1f);
                    break;
                case HitData.HitType.EdgeOfWorld:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByEdgeOfWorld, 1f);
                    break;
                case HitData.HitType.Impact:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByImpact, 1f);
                    break;
                case HitData.HitType.Cart:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByCart, 1f);
                    break;
                case HitData.HitType.Tree:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByTree, 1f);
                    break;
                case HitData.HitType.Self:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathBySelf, 1f);
                    break;
                case HitData.HitType.Structural:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByStructural, 1f);
                    break;
                case HitData.HitType.Turret:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByTurret, 1f);
                    break;
                case HitData.HitType.Boat:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByBoat, 1f);
                    break;
                case HitData.HitType.Stalagtite:
                    Game.instance.IncrementPlayerStat(PlayerStatType.DeathByStalagtite, 1f);
                    break;
                default:
                    ZLog.LogWarning("Not implemented death type " + ___m_lastHit.m_hitType.ToString());
                    break;
            }
        }

        [HarmonyPatch(typeof(Skills), "RaiseSkill")]
        public static class RaiseSkill
        {
            private static bool Prefix(ref Skills __instance, ref Skills.SkillType skillType, ref float factor)
            {
                Skills.Skill skill = __instance.GetSkill(skillType);

                if (skill.m_level >= Plugin.SkillCap.Value) return false;

                factor *= Plugin.SkillMultiplier.Value;
                return true;
            }
        }

        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
        public static class TeleportWorldAesir
        {
            private static bool Prefix(TeleportWorld __instance, Player player)
            {
                // Garante que só estamos rodando a lógica para o jogador local no cliente dele
                if (player != Player.m_localPlayer) return true;

                // 1. Se o jogador for VIP (Aesir), permite o teleporte
                if (Plugin.Vip.Value.Contains(Plugin.steamId)) return true;

                // 2. Pega a tag do portal
                string portalTag = __instance.GetText();

                // Se a tag do portal NÃO estiver na lista VIP, permite o teleporte
                if (!Plugin.VipPortalNames.Value.Contains(portalTag)) return true;

                // 3. Jogador não é VIP tentando acessar portal VIP: Bloqueia!
                player.Message(MessageHud.MessageType.Center, "Only Aesir can access this portal.");
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), "PlacePiece")]
        public static class NoBuild_Patch
        {
            [HarmonyPriority(Priority.Last)]
            private static bool Prefix(Piece piece, Player __instance)
            {
                bool isVip = Vip.Value.Contains(steamId);
                if (isVip) return true;

                if (piece.gameObject.name == "AesirChest")
                {
                    __instance.Message(MessageHud.MessageType.Center, "Esse báu é apenas para Aesir's");
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Prevents a persistent NullReferenceException spam in ZNetScene.RemoveObjects.
        /// When a null ZNetView entry exists in m_instances (e.g. from an orphaned or
        /// unresolved prefab), the NRE unwinds the stack before the cleanup loop runs,
        /// so the entry stays in the dictionary and the same error fires every frame.
        /// This prefix purges any null entries before RemoveObjects iterates over them.
        /// </summary>
        [HarmonyPatch(typeof(ZNetScene), "RemoveObjects")]
        public static class ZNetScene_RemoveObjects_NullGuard
        {
            private static readonly FieldInfo s_instances =
                typeof(ZNetScene).GetField("m_instances", BindingFlags.Instance | BindingFlags.NonPublic);

            private static bool _dirty = true;   // assume dirty until proven clean
            private static bool _recoveringFromNullReference;

            [HarmonyPrefix]
            private static void Prefix(
                ZNetScene __instance,
                List<ZDO> currentNearObjects,
                List<ZDO> currentDistantObjects)
            {
                // These lists are rebuilt every frame and can briefly receive a stale
                // ZDO during world startup, independently of m_instances being clean.
                RemoveNullZdos(currentNearObjects);
                RemoveNullZdos(currentDistantObjects);

                if (!_dirty) return;

                if (s_instances?.GetValue(__instance) is not Dictionary<ZDO, ZNetView> instances)
                    return;

                List<ZDO> nullKeys = null;
                foreach (var kvp in instances)
                {
                    if (kvp.Value == null || kvp.Value.GetZDO() == null)
                    {
                        nullKeys ??= new List<ZDO>();
                        nullKeys.Add(kvp.Key);
                    }
                }

                if (nullKeys != null)
                {
                    foreach (var key in nullKeys)
                        instances.Remove(key);
                    Debug.LogWarning($"[Deadheim] Removed {nullKeys.Count} null ZNetView entries from ZNetScene.m_instances to stop NullReferenceException spam.");
                }
                _dirty = false;
            }

            private static void RemoveNullZdos(List<ZDO> objects)
            {
                if (objects == null) return;

                for (int index = objects.Count - 1; index >= 0; index--)
                {
                    if (objects[index] == null)
                        objects.RemoveAt(index);
                }
            }

            /// <summary>
            /// A broken network object may be inserted after the initial scan.  Suppress
            /// only the first NullReferenceException, request one fresh cleanup pass and
            /// let a consecutive failure surface normally instead of hiding an unrelated
            /// bug forever.
            /// </summary>
            [HarmonyFinalizer]
            private static System.Exception Finalizer(System.Exception __exception)
            {
                if (__exception == null)
                {
                    _recoveringFromNullReference = false;
                    return null;
                }

                if (__exception is System.NullReferenceException && !_recoveringFromNullReference)
                {
                    _dirty = true;
                    _recoveringFromNullReference = true;
                    Debug.LogWarning("[Deadheim] Deferred one ZNetScene.RemoveObjects call so invalid network objects can be cleaned safely.");
                    return null;
                }

                return __exception;
            }

            // Reset the dirty flag when the player logs out so a fresh world load rescans.
            [HarmonyPatch(typeof(Game), "Logout")]
            [HarmonyPostfix]
            private static void OnLogout()
            {
                _dirty = true;
                _recoveringFromNullReference = false;
            }
        }
    }
}
