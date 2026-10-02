using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Um canal em cada direcao, com a operacao como primeira string do pacote. Um RPC
    /// por operacao multiplicaria registro e nome de fio sem ganho nenhum.
    /// </summary>
    internal static class PvpNet
    {
        public const string ToServer = "DH_Pvp_ToServer";
        public const string ToClient = "DH_Pvp_ToClient";

        // Cliente -> servidor
        public const string OpHello = "hello";
        public const string OpDeath = "death";
        public const string OpBounty = "bounty";
        public const string OpRank = "rank";
        public const string OpPve = "pve";
        public const string OpAdmin = "admin";

        // Servidor -> cliente
        public const string OpState = "state";
        public const string OpMessage = "msg";
        public const string OpReward = "reward";
        public const string OpRankResult = "rankres";
        public const string OpPunish = "punish";

        private static bool _registered;

        [HarmonyPatch(typeof(Game), "Start")]
        private static class RegisterPatch
        {
            private static void Postfix()
            {
                if (ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.Register<ZPackage>(ToServer, OnToServer);
                ZRoutedRpc.instance.Register<ZPackage>(ToClient, OnToClient);
                _registered = true;
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private static class LogoutPatch
        {
            private static void Postfix() => _registered = false;
        }

        // ------------------------------------------------------------------ envio

        public static ZPackage Package(string op)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(op);
            return pkg;
        }

        public static void SendToServer(ZPackage pkg)
        {
            if (!_registered || ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), ToServer, pkg);
        }

        public static void SendToClient(long peerId, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(peerId, ToClient, pkg);
        }

        public static void SendToEveryone(ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, ToClient, pkg);
        }

        /// <summary>Mensagem para um jogador: centro da tela e linha no chat.</summary>
        public static void Message(long peerId, string text, bool center = true)
        {
            ZPackage pkg = Package(OpMessage);
            pkg.Write(center);
            pkg.Write(text);
            SendToClient(peerId, pkg);
        }

        public static void Broadcast(string text, bool center = false)
        {
            ZPackage pkg = Package(OpMessage);
            pkg.Write(center);
            pkg.Write(text);
            SendToEveryone(pkg);
        }

        // --------------------------------------------------------------- recebimento

        private static void OnToServer(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            try
            {
                string op = pkg.ReadString();
                PvpServer.Handle(sender, op, pkg);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim PvP] Pacote do cliente " + sender + " falhou: " + ex);
            }
        }

        private static void OnToClient(long sender, ZPackage pkg)
        {
            try
            {
                string op = pkg.ReadString();
                PvpClient.Handle(op, pkg);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim PvP] Pacote do servidor falhou: " + ex);
            }
        }
    }

    /// <summary>Quem e quem no servidor. Nunca confia no playerId que o cliente manda.</summary>
    internal struct PvpPeer
    {
        public long PeerId;
        public long PlayerId;
        public string Name;
        public ZDOID CharacterId;
        public Vector3 Position;

        public bool IsValid => PeerId != 0L && PlayerId != 0L;

        public static bool TryResolve(long peerId, out PvpPeer result)
        {
            result = default;
            ZNet net = ZNet.instance;
            if (net == null) return false;

            // Host local (servidor com jogador): o remetente e o proprio servidor.
            if (ZRoutedRpc.instance != null && peerId == ZRoutedRpc.instance.m_id)
            {
                Player local = Player.m_localPlayer;
                if (local == null) return false;
                result = new PvpPeer
                {
                    PeerId = peerId,
                    PlayerId = local.GetPlayerID(),
                    Name = local.GetPlayerName(),
                    CharacterId = local.GetZDOID(),
                    Position = local.transform.position,
                };
                return result.PlayerId != 0L;
            }

            ZNetPeer peer = net.GetPeer(peerId);
            return peer != null && TryFrom(peer, out result);
        }

        public static bool TryFrom(ZNetPeer peer, out PvpPeer result)
        {
            result = default;
            if (peer == null || peer.m_characterID.IsNone() || ZDOMan.instance == null) return false;
            ZDO zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
            if (zdo == null) return false;

            result = new PvpPeer
            {
                PeerId = peer.m_uid,
                PlayerId = zdo.GetLong(ZDOVars.s_playerID, 0L),
                Name = zdo.GetString(ZDOVars.s_playerName, peer.m_playerName),
                CharacterId = peer.m_characterID,
                Position = peer.m_refPos,
            };
            return result.PlayerId != 0L;
        }

        public static List<PvpPeer> Online()
        {
            List<PvpPeer> list = new List<PvpPeer>();
            ZNet net = ZNet.instance;
            if (net == null) return list;

            foreach (ZNetPeer peer in net.GetPeers())
                if (peer != null && peer.IsReady() && TryFrom(peer, out PvpPeer info)) list.Add(info);

            if (!net.IsDedicated() && ZRoutedRpc.instance != null
                && TryResolve(ZRoutedRpc.instance.m_id, out PvpPeer host)) list.Add(host);
            return list;
        }

        public static bool TryFindByPlayerId(long playerId, out PvpPeer result)
        {
            foreach (PvpPeer peer in Online())
                if (peer.PlayerId == playerId) { result = peer; return true; }
            result = default;
            return false;
        }

        public static bool TryFindByName(string name, out PvpPeer result)
        {
            foreach (PvpPeer peer in Online())
                if (string.Equals(peer.Name, name, StringComparison.OrdinalIgnoreCase)) { result = peer; return true; }
            result = default;
            return false;
        }

        public static bool TryFindByCharacter(ZDOID characterId, out PvpPeer result)
        {
            if (!characterId.IsNone())
                foreach (PvpPeer peer in Online())
                    if (peer.CharacterId == characterId) { result = peer; return true; }
            result = default;
            return false;
        }
    }
}
