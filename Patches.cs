using HarmonyLib;
using Deadheim.Vanilla;
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
                    Plugin.PlayerName = Player.m_localPlayer.m_nview.GetZDO().GetString("playerName");
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
                // Player.Update roda para todo Player carregado, inclusive os remotos e, no servidor
                // dedicado, sem jogador local nenhum (m_localPlayer null): so o local mostra o aviso.
                if (__instance == null || __instance != Player.m_localPlayer) return;
                if (!string.IsNullOrEmpty(Plugin.StaffMessage.Value)) __instance.Message(MessageHud.MessageType.Center, Plugin.StaffMessage.Value);
            }
        }

        // Admin ve todo mundo no mapa. A lista de jogadores do vanilla (RPC PlayerList, a cada 2 s)
        // so leva a posicao de quem a deixou publica: os outros chegam no cliente com m_position
        // zerado, e era por isso que apareciam todos em 0,0,0. Logo depois dela o servidor manda
        // so aos admins a posicao de todos (peer.m_refPos, que o cliente envia sempre, publica ou nao).
        private const string AdminPosicoesRpc = "DeadheimAdminPosicoes";
        private static readonly Dictionary<ZDOID, Vector3> _adminPosicoes = new Dictionary<ZDOID, Vector3>();
        private static float _adminPosicoesEm = -999f;

        [HarmonyPatch(typeof(Game), "Start")]
        private class AdminPosicoesRegistra
        {
            private static void Postfix()
            {
                _adminPosicoes.Clear();
                ZRoutedRpc.instance?.Register<ZPackage>(AdminPosicoesRpc, NoClienteAdminPosicoes);
            }
        }

        [HarmonyPatch(typeof(ZNet), "SendPlayerList")]
        private class AdminPosicoesEnvia
        {
            private static void Postfix(ZNet __instance)
            {
                if (!__instance.IsServer() || ZRoutedRpc.instance == null) return;

                List<ZNetPeer> admins = null;
                List<ZNetPeer> prontos = new List<ZNetPeer>();
                foreach (ZNetPeer peer in __instance.GetPeers())
                {
                    if (peer == null || !peer.IsReady() || peer.m_characterID.IsNone()) continue;
                    prontos.Add(peer);
                    if (peer.m_socket != null && __instance.IsAdmin(peer.m_socket.GetHostName()))
                        (admins ??= new List<ZNetPeer>()).Add(peer);
                }
                if (admins == null) return;

                ZPackage pkg = new ZPackage();
                pkg.Write(prontos.Count);
                foreach (ZNetPeer peer in prontos)
                {
                    pkg.Write(peer.m_characterID);
                    pkg.Write(peer.m_refPos);
                }
                foreach (ZNetPeer admin in admins)
                    ZRoutedRpc.instance.InvokeRoutedRPC(admin.m_uid, AdminPosicoesRpc, pkg);
            }
        }

        private static void NoClienteAdminPosicoes(long sender, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID()) return;
            _adminPosicoes.Clear();
            int n = pkg.ReadInt();
            for (int i = 0; i < n; i++)
            {
                ZDOID id = pkg.ReadZDOID();
                _adminPosicoes[id] = pkg.ReadVector3();
            }
            _adminPosicoesEm = Time.time;
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.GetOtherPublicPlayers))]
        private class AdminGetOtherPublicPlayers
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ZNet __instance, List<ZNet.PlayerInfo> playerList)
            {
                if (!Admin.LocalPlayerIsAdmin() || playerList == null) return;
                // Pacote velho (servidor sem esta versao, ou parou de mandar): melhor nao mostrar
                // ninguem do que mostrar em posicao errada.
                if (Time.time - _adminPosicoesEm > 10f) return;

                // ZNet.m_characterID e privado no jogo: o proprio personagem sai pela ZDO do jogador local.
                ZDOID eu = Player.m_localPlayer != null ? Player.m_localPlayer.GetZDOID() : ZDOID.None;
                foreach (ZNet.PlayerInfo player in __instance.GetPlayerList())
                {
                    if (player.m_publicPosition) continue; // o vanilla ja colocou na lista
                    if (player.m_characterID.IsNone() || player.m_characterID == eu) continue;
                    if (!_adminPosicoes.TryGetValue(player.m_characterID, out Vector3 pos)) continue;
                    ZNet.PlayerInfo oculto = player;
                    oculto.m_position = pos;
                    oculto.m_publicPosition = true;
                    playerList.Add(oculto);
                }
            }
        }

        // O Valheim manda a adminlist.txt ao cliente uma vez so, no RPC_PeerInfo. O ServerSync
        // segura o PeerInfo do servidor ate mandar as configs, entao o AdminList chega antes de
        // o cliente registrar o RPC e se perde: o admin nunca se ve como admin no cliente.
        // O CharacterID vem do cliente ja no mundo; ai a lista chega.
        [HarmonyPatch(typeof(ZNet), "RPC_CharacterID")]
        private class ReenviaAdminList
        {
            private static void Postfix(ZNet __instance)
            {
                if (__instance.IsServer()) __instance.SendAdminList();
            }
        }

        /// <summary>A adminlist chegou no cliente: as pecas so de admin voltam ao martelo do admin.</summary>
        [HarmonyPatch(typeof(ZNet), "RPC_AdminList")]
        private class AdminListChegou
        {
            private static void Postfix(ZNet __instance)
            {
                if (!__instance.IsServer()) ItemService.RestoreAdminPieces();
            }
        }

        /// <summary>
        /// Os contadores de ward e portal sao do personagem: o proximo nao herda os do anterior
        /// (B17). 999 = ainda sem resposta do servidor, como no comeco do jogo.
        /// </summary>
        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private class ZeraContadoresNoLogout
        {
            private static void Postfix()
            {
                Plugin.PlayerWardCount = 999;
                Plugin.PlayerPortalCount = 999;
                Plugin.PlayerSafeAreaWardCount = 999;
                TotemCidade.Contagem = 999;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        public static void Awake_Postfix(ref Player __instance)
        {
            if (ZRoutedRpc.instance == null)
                return;

            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "Sync", new ZPackage());
        }

        [HarmonyPatch(typeof(Player), nameof(Player.EdgeOfWorldKill))]
        [HarmonyPrefix]
        public static bool EdgeOfWorldKill()
        {
            return Plugin.EdgeOfWorldKills == null || Plugin.EdgeOfWorldKills.Value;
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
                if (Pvp.PvpConfig.Active)
                    factor *= Mathf.Max(0f, Pvp.PvpPve.IsLocal ? Pvp.PvpConfig.PveSkillMultiplier.Value : Pvp.PvpConfig.PvpSkillMultiplier.Value);
                // Estudo do Treinador: multiplica o ganho que ja passou pelos dois de cima (0.5 x 1.5 = 0.75).
                factor *= Treinador.StudyMultiplier(__instance, skill.m_level);
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
                if (VipList.VipListApi.IsLocalPlayerVip()) return true;

                // 2. Pega a tag do portal
                string portalTag = __instance.GetText();

                // Se a tag do portal NÃO estiver na lista VIP, permite o teleporte
                if (!IsVipPortal(portalTag)) return true;

                // 3. Jogador não é VIP tentando acessar portal VIP: Bloqueia!
                player.Message(MessageHud.MessageType.Center, "Only Aesir can access this portal.");
                return false;
            }

            /// <summary>
            /// A tag inteira esta na lista (separada por virgula), sem diferenciar maiusculas.
            /// O Contains do texto da lista casava portal sem nome ("" esta em qualquer texto) e
            /// qualquer pedaco de um nome VIP ("cava", "lin", ","): o portal comum virava so VIP.
            /// </summary>
            internal static bool IsVipPortal(string portalTag)
            {
                string tag = portalTag?.Trim();
                if (string.IsNullOrEmpty(tag)) return false;
                foreach (string name in (Plugin.VipPortalNames.Value ?? string.Empty).Split(','))
                    if (string.Equals(name.Trim(), tag, System.StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), "PlacePiece")]
        public static class NoBuild_Patch
        {
            [HarmonyPriority(Priority.Last)]
            private static bool Prefix(Piece piece, Player __instance)
            {
                bool isVip = VipList.VipListApi.IsLocalPlayerVip();
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
        /// Null-safe equivalent of the vanilla ZNetScene.RemoveObjects implementation.
        /// A failed or unresolved prefab can leave a destroyed ZNetView, a view without
        /// a ZDO, or a stale ZDO in the per-frame lists. Vanilla dereferences all three
        /// unconditionally, so one bad entry aborts cleanup and repeats the NRE.
        /// </summary>
        [HarmonyPatch(typeof(ZNetScene), "RemoveObjects")]
        public static class ZNetScene_RemoveObjects_NullGuard
        {
            private static readonly FieldInfo s_instances =
                typeof(ZNetScene).GetField("m_instances", BindingFlags.Instance | BindingFlags.NonPublic);
            private static readonly FieldInfo s_tempRemoved =
                typeof(ZNetScene).GetField("m_tempRemoved", BindingFlags.Instance | BindingFlags.NonPublic);
            private static bool s_reportedInvalidInstances;

            [HarmonyPrefix]
            [HarmonyPriority(Priority.Last)]
            private static bool Prefix(
                ZNetScene __instance,
                List<ZDO> currentNearObjects,
                List<ZDO> currentDistantObjects)
            {
                if (s_instances?.GetValue(__instance) is not Dictionary<ZDO, ZNetView> instances ||
                    s_tempRemoved?.GetValue(__instance) is not List<ZNetView> tempRemoved)
                    return true;

                byte earmark = (byte)(Time.frameCount & byte.MaxValue);
                MarkLiveObjects(currentNearObjects, earmark);
                MarkLiveObjects(currentDistantObjects, earmark);

                tempRemoved.Clear();
                int invalidCount = 0;
                HashSet<string> invalidPrefabs = s_reportedInvalidInstances ? null : new HashSet<string>();
                List<ZDO> deadKeys = null;
                foreach (var kvp in instances)
                {
                    ZNetView view = kvp.Value;
                    ZDO zdo = view == null ? null : view.GetZDO();
                    if (zdo == null)
                    {
                        invalidCount++;
                        (deadKeys ??= new List<ZDO>()).Add(kvp.Key);
                        if (invalidPrefabs != null)
                        {
                            int prefabHash = kvp.Key.GetPrefab();
                            GameObject prefab = __instance.GetPrefab(prefabHash);
                            invalidPrefabs.Add(prefab == null ? $"hash:{prefabHash}" : prefab.name);
                        }
                        continue;
                    }

                    if (zdo.TempRemoveEarmark != earmark)
                        tempRemoved.Add(view);
                }

                // A entrada morta sai do dicionario: enquanto ficasse, o CreateObjects achava que a ZDO
                // ja tinha instancia e o objeto ficava invisivel ate relogar (B10). A ZDO volta a
                // nascer no proximo CreateObjects, se ainda estiver por perto.
                if (deadKeys != null)
                    foreach (ZDO key in deadKeys)
                        instances.Remove(key);

                if (invalidCount > 0 && !s_reportedInvalidInstances)
                {
                    s_reportedInvalidInstances = true;
                    Debug.LogWarning(
                        $"[Deadheim] Ignoring {invalidCount} invalid ZNetScene instance entries safely " +
                        $"(prefabs: {string.Join(", ", invalidPrefabs)})."
                    );
                }

                for (int index = 0; index < tempRemoved.Count; index++)
                {
                    ZNetView view = tempRemoved[index];
                    if (view == null) continue;

                    ZDO zdo = view.GetZDO();
                    if (zdo == null) continue;

                    view.ResetZDO();
                    UnityEngine.Object.Destroy(view.gameObject);
                    if (!zdo.Persistent && zdo.IsOwner() && ZDOMan.instance != null)
                        ZDOMan.instance.DestroyZDO(zdo);
                    instances.Remove(zdo);
                }

                return false;
            }

            private static void MarkLiveObjects(List<ZDO> objects, byte earmark)
            {
                if (objects == null) return;

                for (int index = objects.Count - 1; index >= 0; index--)
                {
                    ZDO zdo = objects[index];
                    if (zdo == null)
                    {
                        objects.RemoveAt(index);
                        continue;
                    }

                    zdo.TempRemoveEarmark = earmark;
                }
            }
        }
    }
}
