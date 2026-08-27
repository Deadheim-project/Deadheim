using System;
using UnityEngine;

namespace Deadheim.Wards
{
    /// <summary>
    /// Ponto de entrada para outro plugin dizer "aqui quem manda sou eu, ward nao se mete".
    ///
    /// O RaidSystem registra Util.IsRaidEnabledHere aqui no Awake dele. Dentro de zona de
    /// raid valem as regras de horario do RaidSystem, e nenhum patch de ward atua.
    /// Sem ninguem registrado o modulo funciona sozinho, sem depender do RaidSystem.
    /// </summary>
    public static class WardBridge
    {
        public static Func<Vector3, bool> IsExternallyGoverned;

        /// <summary>
        /// Resolve a guild de um jogador pelo playerID. Registrado pelo RaidSystem, que ja
        /// fala com a API do Guilds. Mantido como delegate para o Deadheim nao precisar
        /// referenciar Guilds.dll: sem ninguem registrado, acesso por guild fica desligado.
        /// </summary>
        public static Func<long, string> GuildOfPlayer;

        public static bool Governed(Vector3 point)
        {
            Func<Vector3, bool> check = IsExternallyGoverned;
            if (check == null) return false;

            try
            {
                return check(point);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Wards] Hook de governanca externa falhou: " + ex.Message);
                return false;
            }
        }

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
