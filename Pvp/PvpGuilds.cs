using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Guilda de um jogador, lida do mod Guilds (blaxxun), que o RaidSystem e o Velas ja
    /// usam como sistema de cla. Por reflexao, como o GuildsClanProvider do Velas: o
    /// Deadheim nao ganha dependencia dura, e sem o Guilds todo mundo fica "sem guilda"
    /// (ninguem e aliado de ninguem), em vez de quebrar.
    /// </summary>
    internal static class PvpGuilds
    {
        private const string AssemblyName = "Guilds";
        private const string ApiTypeName = "Guilds.API";

        /// <summary>So para o driver de teste, que nao tem como por o boneco numa guilda de verdade.</summary>
        internal static Func<Player, string> TestOverride;

        private static bool _resolved;
        private static MethodInfo _isLoaded;
        private static MethodInfo _getPlayerGuild;
        // No Guilds 1.1.x Guild.Name e campo publico, nao propriedade.
        private static Func<object, string> _guildName;

        public static bool IsAvailable
        {
            get
            {
                Resolve();
                if (_isLoaded == null) return false;
                try { return (bool)_isLoaded.Invoke(null, Array.Empty<object>()); }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Deadheim PvP] Guilds.API.IsLoaded falhou: " + ex.Message);
                    return false;
                }
            }
        }

        /// <summary>Nome da guilda do jogador, ou null. O jogador precisa estar carregado aqui.</summary>
        public static string GuildOf(Player player)
        {
            if (player == null) return null;
            if (TestOverride != null) return TestOverride(player);
            if (!IsAvailable || _getPlayerGuild == null || _guildName == null) return null;

            try
            {
                object guild = _getPlayerGuild.Invoke(null, new object[] { player });
                string name = guild != null ? _guildName(guild) : null;
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Deadheim PvP] Guilds.API.GetPlayerGuild falhou: " + ex.Message);
                return null;
            }
        }

        public static string GuildOf(long playerId)
            => playerId == 0L ? null : GuildOf(Player.GetPlayer(playerId));

        public static bool SameGuild(Player a, Player b)
        {
            if (a == null || b == null || a == b) return false;
            string guildA = GuildOf(a);
            return guildA != null && string.Equals(guildA, GuildOf(b), StringComparison.OrdinalIgnoreCase);
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == AssemblyName);
                if (assembly == null)
                {
                    Debug.Log("[Deadheim PvP] Mod Guilds nao instalado: sem aliados por guilda.");
                    return;
                }

                Type api = assembly.GetType(ApiTypeName);
                if (api == null)
                {
                    Debug.LogWarning("[Deadheim PvP] Guilds carregado mas sem Guilds.API; aliados por guilda desligados.");
                    return;
                }

                const BindingFlags anyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                _isLoaded = api.GetMethod("IsLoaded", anyStatic, null, Type.EmptyTypes, null);
                _getPlayerGuild = api.GetMethods(anyStatic).FirstOrDefault(m =>
                    m.Name == "GetPlayerGuild" && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(Player));
                Type guildType = _getPlayerGuild?.ReturnType;
                FieldInfo nameField = guildType?.GetField("Name", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo nameProperty = guildType?.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                if (nameField != null) _guildName = g => nameField.GetValue(g) as string;
                else if (nameProperty != null) _guildName = g => nameProperty.GetValue(g) as string;

                Debug.Log($"[Deadheim PvP] Guilds integrado (IsLoaded={_isLoaded != null}, " +
                          $"GetPlayerGuild={_getPlayerGuild != null}, Name={_guildName != null}).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Deadheim PvP] Nao foi possivel ligar a API do Guilds: " + ex.Message);
            }
        }
    }
}
