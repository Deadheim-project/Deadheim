using System;
using UnityEngine;

namespace Deadheim.Wards
{
    /// <summary>
    /// Resolve a guilda de um jogador para o acesso por guilda aos wards. O PvpModule registra
    /// o resolvedor (PvpGuilds, que le o mod Guilds); o Deadheim nao referencia Guilds.dll.
    /// Sem ninguem registrado, acesso por guild fica desligado.
    /// </summary>
    public static class WardBridge
    {
        public static Func<long, string> GuildOfPlayer;

        public static string GuildOf(long playerId)
        {
            Func<long, string> resolve = GuildOfPlayer;
            if (resolve == null || playerId == 0L) return null;

            try
            {
                return resolve(playerId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Wards] Hook de guild falhou: " + ex.Message);
                return null;
            }
        }
    }
}
