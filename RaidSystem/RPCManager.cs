using HarmonyLib;
using System;
using System.Collections.Generic;
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
            float x = pkg.ReadSingle(), z = pkg.ReadSingle();
            string loc = string.IsNullOrEmpty(zone) ? $"X:{(int)x} Z:{(int)z}" : zone;
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center,
                $"{team} ({nick}) {RaidSystemPlugin.ConquestMessage.Value} {loc}");
        }

        /// <summary>
        /// Destruicao de RaidWard reportada por quem era dono da ZDO. A conquista e decidida
        /// aqui, no servidor: a guild e re-derivada do playerId em vez de vir do pacote, e a
        /// zona e o horario sao revalidados, para um cliente nao forjar conquista.
        /// </summary>
        public static void RPC_WardDestroyed(long sender, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer()) return;

            string pid = pkg.ReadString();
            string nick = pkg.ReadString();
            Vector3 pos = pkg.ReadVector3();
            Quaternion rot = pkg.ReadQuaternion();

            if (!Util.IsRaidEnabledHere(pos))
            {
                Debug.LogWarning("[RaidSystem] Ward destruction reported outside a raid zone; ignored.");
                return;
            }

            // Varios peers podem reportar a mesma destruicao.
            string key = WardKey(pos);
            if (_recentWardReports.TryGetValue(key, out float last) && Time.time - last <= 10f) return;
            _recentWardReports[key] = Time.time;

            string teamId = null;
            if (!Util.IsRaidDisabledThisTime(pos) && long.TryParse(pid, out long playerId) && playerId != 0L)
                teamId = GuildsIntegration.GetPlayerTeam(playerId);

            if (!string.IsNullOrEmpty(teamId))
                HandleConquest(pid, nick, teamId, pos);
            else
                Debug.LogWarning("[RaidSystem] RaidWard destroyed without a valid guild attacker; respawning without conquest.");

            Util.RespawnWard(pos, rot);
        }

        private static readonly Dictionary<string, float> _recentWardReports = new Dictionary<string, float>();

        private static string WardKey(Vector3 pos)
            => Mathf.RoundToInt(pos.x / 5f) + "_" + Mathf.RoundToInt(pos.z / 5f);

        public static void HandleConquest(string pid, string nick, string teamId, Vector3 pos)
        {
            if (!ZNet.instance.IsServer()) return;
            RaidZone zone = Util.GetRaidZoneAt(pos);
            string zoneName = zone?.Name ?? $"X:{(int)pos.x} Z:{(int)pos.z}";
            Vector3 territoryPosition = zone?.Position ?? pos;

            // Herdado de proposito: castelo gordo vale mais. Declarado fora da lambda
            // porque a mensagem do webhook le o valor depois.
            int inherited = 0;

            DataStore.Modify(data =>
            {
                TerritoryInfo territory = data.Territories.FirstOrDefault(t =>
                    string.Equals(t.Name, zoneName, StringComparison.OrdinalIgnoreCase));
                if (territory == null)
                {
                    territory = new TerritoryInfo { Name = zoneName };
                    data.Territories.Add(territory);
                }

                inherited = territory.PendingTribute;

                territory.X = territoryPosition.x;
                territory.Y = territoryPosition.y;
                territory.Z = territoryPosition.z;
                territory.OwnerTeamId = teamId;
                territory.LastConquestTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            });

            ScoreManager.RecordConquest(pid, nick, teamId);
            ZPackage n = new(); n.Write(nick); n.Write(teamId); n.Write(zoneName); n.Write(pos.x); n.Write(pos.z);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "RaidSystem_Conquest", n);
            string webhookMessage = $"**[Conquista]** **{teamId}** conquistou **{zoneName}** com **{nick}**";
            if (inherited > 0) webhookMessage += $" e herdou **{inherited}** carga(s) de tributo";
            webhookMessage += ".\n";
            webhookMessage += ScoreManager.FormatLeaderboardForWebhook();
            dWebHook.SendRaidMessage(webhookMessage);
            BroadcastFullSync();
        }

        /// <summary>
        /// Resgate de tributo. O playerId vem no pacote mas NAO e confiado: o servidor confere que
        /// o peer que mandou e mesmo o dono daquele personagem (Util.ResolvePlayerId). Sem isso um
        /// cliente se passa por membro da guild dominante e saca o tributo dela.
        /// </summary>
        public static void RPC_ClaimTribute(long sender, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer()) return;

            Vector3 pos = pkg.ReadVector3();

            long playerId = Util.ResolvePlayerId(sender);
            if (playerId == 0L && sender == ZRoutedRpc.instance.GetServerPeerID() && Player.m_localPlayer != null)
                playerId = Player.m_localPlayer.GetPlayerID();
            if (playerId == 0L) return;

            string team = GuildsIntegration.GetPlayerTeam(playerId);
            if (string.IsNullOrEmpty(team)) return;

            RaidZone zone = Util.GetRaidZoneAt(pos);
            if (zone == null) return;

            TerritoryInfo territory = Util.GetTerritoryAt(pos);
            if (territory == null
                || string.IsNullOrEmpty(territory.OwnerTeamId)
                || !string.Equals(territory.OwnerTeamId, team, StringComparison.OrdinalIgnoreCase))
                return;

            int charges = territory.PendingTribute;
            if (charges <= 0) return;

            Dictionary<string, int> loot = TributeManager.Roll(zone.Tier, charges);
            if (loot.Count == 0) return;

            DataStore.Modify(_ => { territory.PendingTribute = 0; });

            ZPackage response = new ZPackage();
            response.Write(loot.Count);
            foreach (var kv in loot) { response.Write(kv.Key); response.Write(kv.Value); }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, "RaidSystem_GrantTribute", response);

            dWebHook.SendRaidMessage(
                $"**[Tributo]** **{team}** resgatou **{charges}** carga(s) em **{zone.Name}**.");

            BroadcastFullSync();
        }

        public static void RPC_GrantTribute(long sender, ZPackage pkg)
        {
            Player lp = Player.m_localPlayer;
            if (lp == null) return;

            int count = pkg.ReadInt();
            var received = new List<string>();

            for (int i = 0; i < count; i++)
            {
                string prefabName = pkg.ReadString();
                int amount = pkg.ReadInt();

                GameObject prefab = ObjectDB.instance != null
                    ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
                if (prefab == null) continue;

                // O que nao couber cai no chao, nunca some.
                if (lp.GetInventory().CanAddItem(prefab, amount))
                    lp.GetInventory().AddItem(prefab, amount);
                else
                    DropOnGround(lp, prefab, amount);

                received.Add($"{amount}x {prefabName}");
            }

            lp.Message(MessageHud.MessageType.Center,
                received.Count > 0 ? "Tributo: " + string.Join(", ", received) : "Nada a resgatar.");
        }

        private static void DropOnGround(Player player, GameObject prefab, int amount)
        {
            Vector3 pos = player.transform.position + player.transform.forward * 1.5f + Vector3.up;
            GameObject go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            ItemDrop drop = go.GetComponent<ItemDrop>();
            if (drop != null && drop.m_itemData != null)
            {
                drop.m_itemData.m_stack = Mathf.Min(amount, drop.m_itemData.m_shared.m_maxStackSize);
                drop.Save();
            }
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
                ZRoutedRpc.instance.Register<ZPackage>("RaidSystem_WardDestroyed", new Action<long, ZPackage>(RPC_WardDestroyed));
                ZRoutedRpc.instance.Register<ZPackage>("RaidSystem_ClaimTribute", new Action<long, ZPackage>(RPC_ClaimTribute));
                ZRoutedRpc.instance.Register<ZPackage>("RaidSystem_GrantTribute", new Action<long, ZPackage>(RPC_GrantTribute));
                Debug.Log("[RaidSystem] RPCs registered.");

                TributeManager.LoadTables();
                TributeManager.ValidateTables();
            }
        }
    }
}
