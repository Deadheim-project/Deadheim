extern alias GuildsMod;
using System;
using System.Collections.Generic;
using System.Linq;
using GuildsMod::Guilds;
using UnityEngine;

namespace RaidSystem
{
    /// <summary>
    /// Integration with blaxxun's Guilds mod via official GuildsAPI.dll.
    /// API.IsLoaded() returns false when Guilds is not installed — safe soft dependency.
    /// Color is read from guild.General.color (HTML hex string, e.g. "#FF5533FF").
    /// </summary>
    public static class GuildsIntegration
    {
        public static bool IsActive => API.IsLoaded();

        public static string GetPlayerTeam(Player player)
        {
            if (IsActive) { Guild g = API.GetPlayerGuild(player); if (g != null) return g.Name; }
            return null;
        }

        /// <summary>
        /// Resolve a guild a partir de um Player.GetPlayerID().
        ///
        /// A versao anterior comparava com ZNet.PlayerInfo.m_characterID.UserID, que e o id
        /// de rede do peer e nao o id do personagem: dominios diferentes, entao a comparacao
        /// nunca casava e este metodo devolvia sempre null. Isso derrubava o registro de
        /// jogador (RPC_UpdatePlayerData saia cedo) e a atribuicao de conquista.
        ///
        /// Player.GetPlayer compara justamente por GetPlayerID, que e o valor que recebemos.
        /// </summary>
        public static string GetPlayerTeam(long playerID)
        {
            if (!IsActive || playerID == 0L) return null;

            Player player = Player.GetPlayer(playerID);
            if (player != null)
            {
                Guild guild = API.GetPlayerGuild(player);
                if (guild != null) return guild.Name;
            }

            // Fallback: personagem nao instanciado neste peer, mas conectado ao servidor.
            if (ZNet.instance != null && ZDOMan.instance != null)
            {
                foreach (var zpi in ZNet.instance.m_players)
                {
                    if (zpi.m_characterID.IsNone()) continue;

                    ZDO zdo = ZDOMan.instance.GetZDO(zpi.m_characterID);
                    if (zdo == null || zdo.GetLong(ZDOVars.s_playerID, 0L) != playerID) continue;

                    Guild fallback = API.GetPlayerGuild(PlayerReference.fromPlayerInfo(zpi));
                    if (fallback != null) return fallback.Name;
                    break;
                }
            }
            return null;
        }

        public static string GetOwnGuildName()
        {
            if (IsActive) { Guild g = API.GetOwnGuild(); if (g != null) return g.Name; }
            return null;
        }

        public static bool AreAllies(Player a, Player b)
        {
            if (IsActive)
            {
                Guild gA = API.GetPlayerGuild(a);
                Guild gB = API.GetPlayerGuild(b);
                if (gA != null && gB != null) return gA.Name == gB.Name;
            }
            return false;
        }

        public static bool AreAllies(long playerA, long playerB)
        {
            string tA = GetPlayerTeam(playerA); string tB = GetPlayerTeam(playerB);
            return !string.IsNullOrEmpty(tA) && !string.IsNullOrEmpty(tB)
                && string.Equals(tA, tB, StringComparison.OrdinalIgnoreCase);
        }

        public static List<string> GetAllTeamNames()
        {
            if (IsActive) { var gs = API.GetGuilds(); if (gs.Count > 0) return gs.Select(g => g.Name).ToList(); }
            return new List<string>();
        }

        public static List<string> GetTeamMemberNicks(string teamName)
        {
            if (IsActive) { Guild g = API.GetGuild(teamName); if (g != null) return g.Members.Keys.Select(m => m.name).ToList(); }
            return new List<string>();
        }

        public static List<string> GetOnlineTeamMembers(string teamName)
        {
            if (IsActive) { Guild g = API.GetGuild(teamName); if (g != null) return API.GetOnlinePlayers(g).Select(p => p.name).ToList(); }
            return new List<string>();
        }

        /// <summary>
        /// Gets guild color. Guilds stores it as HTML hex in guild.General.color.
        /// Falls back to deterministic HSV color from name hash.
        /// </summary>
        public static Color GetGuildColor(string guildName)
        {
            if (IsActive)
            {
                Guild guild = API.GetGuild(guildName);
                if (guild != null && !string.IsNullOrEmpty(guild.General.color))
                    if (ColorUtility.TryParseHtmlString(guild.General.color, out Color color))
                        return color;
            }
            return ColorFromName(guildName);
        }

        public static Color ColorFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return Color.gray;
            int hash = 5381;
            foreach (char c in name) hash = ((hash << 5) + hash) + c;
            float hue = Mathf.Abs(hash % 360) / 360f;
            float sat = 0.6f + (Mathf.Abs((hash >> 8) % 30) / 100f);
            float val = 0.7f + (Mathf.Abs((hash >> 16) % 20) / 100f);
            return Color.HSVToRGB(hue, sat, val);
        }

    }
}
