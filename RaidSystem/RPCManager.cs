using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;

namespace RaidSystem
{
    [HarmonyPatch]
    internal class RPCManager
    {
        public static void RPC_RequestFullSync(long sender, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer()) return;
            ZPackage r = new(); r.Write(DataStore.Serialize());
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, "RaidSystem_FullSyncResponse", r);
        }

        public static void RPC_FullSyncResponse(long sender, ZPackage pkg)
        {
            if (ZNet.instance.IsServer()) return;
            string json = pkg.ReadString(); if (string.IsNullOrEmpty(json)) return;
            DataStore.Deserialize(json);
            var lp = Player.m_localPlayer;
            if (lp != null)
            {
                string pid = lp.GetPlayerID().ToString();
                var info = DataStore.Load().Players.FirstOrDefault(p => p.PlayerId == pid);
                RaidSystemPlugin.LocalPlayerInfo = info;
                string guildName = GuildsIntegration.GetOwnGuildName();
                RaidSystemPlugin.HasTeam = !string.IsNullOrEmpty(guildName);
                if (info != null) info.TeamId = guildName;
            }
            GUI.LoadMenu();
        }

        public static void RPC_UpdatePlayerData(long sender, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer()) return;
            string nick = pkg.ReadString(), steamId = pkg.ReadString(), playerId = pkg.ReadString(), desc = pkg.ReadString(), teamId = pkg.ReadString();
            if (long.TryParse(playerId, out long parsedPlayerId))
                teamId = GuildsIntegration.GetPlayerTeam(parsedPlayerId);
            if (string.IsNullOrEmpty(teamId)) return;

            string previousTeam = null;
            DataStore.Modify(data =>
            {
                var e = data.Players.FirstOrDefault(p => p.PlayerId == playerId);
                if (e == null) { e = new PlayerInfo(); data.Players.Add(e); }
                previousTeam = e.TeamId;
                e.Nick = nick; e.SteamId = steamId; e.PlayerId = playerId; e.Description = desc; e.TeamId = teamId;
            });
            if (!string.Equals(previousTeam, teamId, StringComparison.OrdinalIgnoreCase))
            {
                string action = string.IsNullOrEmpty(previousTeam) ? "entrou na guild" : $"mudou de **{previousTeam}** para";
                dWebHook.SendRaidMessage($"**[RaidSystem]** {nick} {action} **{teamId}**.");
            }
            BroadcastFullSync();
        }

        public static void BroadcastFullSync()
        {
            if (!ZNet.instance.IsServer()) return;
            ZPackage r = new(); r.Write(DataStore.Serialize());
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "RaidSystem_FullSyncResponse", r);
        }

        public static void RPC_RequestScores(long sender, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer()) return;
            ZPackage r = new(); r.Write(ScoreManager.SerializeScores());
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, "RaidSystem_ScoresResponse", r);
        }

        public static void RPC_ScoresResponse(long sender, ZPackage pkg)
        {
            if (ZNet.instance.IsServer()) return;
            var scores = ScoreManager.DeserializeScores(pkg.ReadString());
            var d = DataStore.Load(); d.Scores.Clear(); d.Scores.AddRange(scores);
            GUI.UpdateScoreboard();
        }

        public static void RPC_ConquestNotification(long sender, ZPackage pkg)
        {
            if (ZNet.instance.IsServer()) return;
            string nick = pkg.ReadString(), team = pkg.ReadString(), zone = pkg.ReadString();
            float x = pkg.ReadSingle(), z = pkg.ReadSingle(); int cd = pkg.ReadInt();
            string loc = string.IsNullOrEmpty(zone) ? $"X:{(int)x} Z:{(int)z}" : zone;
            string msg = $"{team} ({nick}) {RaidSystemPlugin.ConquestMessage.Value} {loc}";
            if (cd > 0) msg += $" [Protecao: {cd}min]";
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, msg);
        }

        public static void HandleConquest(string pid, string nick, string teamId, Vector3 pos)
        {
            if (!ZNet.instance.IsServer()) return;
            RaidZone zone = Util.GetRaidZoneAt(pos);
            string zoneName = zone?.Name ?? $"X:{(int)pos.x} Z:{(int)pos.z}";
            Vector3 territoryPosition = zone?.Position ?? pos;

            DataStore.Modify(data =>
            {
                TerritoryInfo territory = data.Territories.FirstOrDefault(t =>
                    string.Equals(t.Name, zoneName, StringComparison.OrdinalIgnoreCase));
                if (territory == null)
                {
                    territory = new TerritoryInfo { Name = zoneName };
                    data.Territories.Add(territory);
                }

                territory.X = territoryPosition.x;
                territory.Y = territoryPosition.y;
                territory.Z = territoryPosition.z;
                territory.OwnerTeamId = teamId;
                territory.LastConquestTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            });

            ScoreManager.RecordConquest(pid, nick, teamId);
            ZPackage n = new(); n.Write(nick); n.Write(teamId); n.Write(zoneName); n.Write(pos.x); n.Write(pos.z); n.Write(0);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "RaidSystem_Conquest", n);
            string webhookMessage = $"**[Conquista]** **{teamId}** conquistou **{zoneName}** com **{nick}**.\n";
            webhookMessage += ScoreManager.FormatLeaderboardForWebhook();
            dWebHook.SendRaidMessage(webhookMessage);
            BroadcastFullSync();
        }

        public static void SendPlayerRegistration(string desc = "")
        {
            var lp = Player.m_localPlayer; if (lp == null) return;
            string teamId = GuildsIntegration.GetOwnGuildName();
            if (string.IsNullOrEmpty(teamId))
            {
                lp.Message(MessageHud.MessageType.Center, "Entre em uma guild para usar o RaidSystem.");
                return;
            }

            ZPackage pkg = new(); pkg.Write(lp.m_nview.GetZDO().GetString("playerName")); pkg.Write(RaidSystemPlugin.SteamId);
            pkg.Write(lp.GetPlayerID().ToString()); pkg.Write(desc); pkg.Write(teamId);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "RaidSystem_UpdatePlayerData", pkg);
        }

        [HarmonyPatch(typeof(Game), "Start")]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.Register<ZPackage>("RaidSystem_RequestFullSync", new Action<long, ZPackage>(RPC_RequestFullSync));
                ZRoutedRpc.instance.Register<ZPackage>("RaidSystem_FullSyncResponse", new Action<long, ZPackage>(RPC_FullSyncResponse));
                ZRoutedRpc.instance.Register<ZPackage>("RaidSystem_UpdatePlayerData", new Action<long, ZPackage>(RPC_UpdatePlayerData));
                ZRoutedRpc.instance.Register<ZPackage>("RaidSystem_RequestScores", new Action<long, ZPackage>(RPC_RequestScores));
                ZRoutedRpc.instance.Register<ZPackage>("RaidSystem_ScoresResponse", new Action<long, ZPackage>(RPC_ScoresResponse));
                ZRoutedRpc.instance.Register<ZPackage>("RaidSystem_Conquest", new Action<long, ZPackage>(RPC_ConquestNotification));
                Debug.Log("[RaidSystem] RPCs registered.");
            }
        }
    }
}
