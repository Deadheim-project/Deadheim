using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Grupo (party) do mod Groups (blaxxun). Por reflexao, como o PvpGuilds: sem o Groups
    /// ninguem esta em grupo e nada quebra.
    ///
    /// O Groups so conhece o grupo do jogador local, entao a pergunta "a e b estao no mesmo
    /// grupo?" so tem resposta quando um dos dois e o jogador local. E sempre o caso onde a
    /// regra roda: o RPC_Damage roda no cliente da vitima e a checagem do atacante no dele.
    /// </summary>
    internal static class PvpGroups
    {
        private const string AssemblyName = "Groups";
        private const string ApiTypeName = "Groups.API";

        /// <summary>So para o driver de teste: ids que estao no grupo do jogador local.</summary>
        internal static Func<long, bool> TestOverride;

        /// <summary>So para o driver de teste: chave do grupo do jogador local.</summary>
        internal static Func<long> TestGroupKey;

        private static bool _resolved;
        private static MethodInfo _findMember;
        private static MethodInfo _groupPlayers;
        private static FieldInfo _peerId;

        public static bool IsAvailable
        {
            get
            {
                Resolve();
                return _findMember != null;
            }
        }

        /// <summary>O jogador <paramref name="playerId"/> esta no grupo do jogador local?</summary>
        public static bool InLocalGroup(long playerId)
        {
            if (playerId == 0L) return false;
            if (TestOverride != null) return TestOverride(playerId);
            if (!IsAvailable) return false;
            try
            {
                // FindGroupMemberByPlayerId devolve PlayerReference? (Nullable): null = fora do grupo.
                return _findMember.Invoke(null, new object[] { playerId }) != null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Deadheim PvP] Groups.API.FindGroupMemberByPlayerId falhou: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Chave do grupo do jogador local, igual para todos os membros (o menor peerId da
        /// lista); 0 = sem grupo. Publicada na ZDO do jogador para quem nao e do grupo poder
        /// comparar dois jogadores (bonus de monstro por aliado, AllyScaling).
        /// </summary>
        public static long LocalGroupKey()
        {
            if (TestGroupKey != null) return TestGroupKey();
            Resolve();
            if (_groupPlayers == null || _peerId == null) return 0L;
            try
            {
                long key = 0L;
                if (_groupPlayers.Invoke(null, null) is System.Collections.IEnumerable members)
                    foreach (object member in members)
                    {
                        long id = (long)_peerId.GetValue(member);
                        if (id != 0L && (key == 0L || id < key)) key = id;
                    }
                return key;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Deadheim PvP] Groups.API.GroupPlayers falhou: " + ex.Message);
                _groupPlayers = null;
                return 0L;
            }
        }

        public static bool SameGroup(Player a, Player b)
        {
            if (a == null || b == null || a == b) return false;
            Player local = Player.m_localPlayer;
            if (local == null) return false;
            if (a == local) return InLocalGroup(b.GetPlayerID());
            if (b == local) return InLocalGroup(a.GetPlayerID());
            return false;
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
                    Debug.Log("[Deadheim PvP] Mod Groups nao instalado: sem aliados por grupo.");
                    return;
                }

                Type api = assembly.GetType(ApiTypeName);
                _findMember = api?.GetMethod("FindGroupMemberByPlayerId",
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(long) }, null);
                _groupPlayers = api?.GetMethod("GroupPlayers", BindingFlags.Public | BindingFlags.Static,
                    null, Type.EmptyTypes, null);
                _peerId = assembly.GetType("Groups.PlayerReference")?.GetField("peerId");
                Debug.Log($"[Deadheim PvP] Groups integrado (FindGroupMemberByPlayerId={_findMember != null}).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Deadheim PvP] Nao foi possivel ligar a API do Groups: " + ex.Message);
            }
        }
    }
}
