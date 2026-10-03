using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RaidSystem
{
    /// <summary>
    /// A janela de raid de cada castelo: quando ela abre, quando fecha e o que acontece no fim.
    ///
    /// - Guilda que segura o castelo a janela inteira (ninguem derrubou a RaidWard) ganha a
    ///   defesa: cargas extras de tributo (os materiais do tier do castelo, resgatados na
    ///   propria RaidWard) e pontos de defesa para os membros online.
    /// - Castelo que caiu nesta janela vira zona segura ate ela fechar: a luta ali acabou.
    /// - Com "Castle Rules Only During Raid", fora da janela o castelo e terra comum para o PvP
    ///   do Deadheim (sem as regras de zona de guerra).
    /// </summary>
    public static class CastleDefense
    {
        private static readonly Dictionary<string, DateTime?> _windowStart = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
        private static float _nextTick;

        // ------------------------------------------------------------ resolvedores do Deadheim

        /// <summary>Castelo para as regras de PvP: so durante a raid dele, se a config mandar.</summary>
        public static string CastleAt(Vector3 pos)
        {
            RaidZone zone = Util.GetRaidZoneAt(pos);
            if (zone == null) return null;
            // Hora do servidor (no cliente, sem o relogio do Windows): adiantar a hora do PC nao abre a janela.
            if (RaidSystemPlugin.CastleRulesOnlyDuringRaid.Value == Toggle.On && !Util.IsRaidHour(zone, Deadheim.RelogioServidor.UtcNow)) return null;
            return zone.Name;
        }

        /// <summary>O castelo que cobre o ponto caiu nesta janela de raid (ainda aberta).</summary>
        public static bool IsFallenAt(Vector3 pos)
        {
            if (RaidSystemPlugin.SafeAfterConquest.Value != Toggle.On) return false;
            RaidZone zone = Util.GetRaidZoneAt(pos);
            if (zone == null) return false;
            DateTime? start = Util.CurrentWindowStartUtc(zone, Deadheim.RelogioServidor.UtcNow);
            if (start == null) return false;
            TerritoryInfo territory = Util.GetTerritoryAt(pos);
            return territory != null && territory.LastConquestTimestamp >= ToUnix(start.Value);
        }

        // ------------------------------------------------------------ fim de janela (servidor)

        public static void Update()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 20f;

            DateTime now = DateTime.UtcNow;
            foreach (RaidZone zone in Util.GetRaidZones())
            {
                DateTime? current = Util.CurrentWindowStartUtc(zone, now);
                _windowStart.TryGetValue(zone.Name, out DateTime? known);
                if (current != null && known == null)
                {
                    _windowStart[zone.Name] = current;
                    Debug.Log($"[RaidSystem] Janela de raid aberta em {zone.Name} (desde {current.Value:HH:mm} UTC).");
                }
                else if (current == null && known != null)
                {
                    _windowStart[zone.Name] = null;
                    try { OnWindowEnd(zone, known.Value); }
                    catch (Exception ex) { Debug.LogError($"[RaidSystem] Fim da janela de {zone.Name} falhou: {ex}"); }
                }
            }
        }

        private static void OnWindowEnd(RaidZone zone, DateTime windowStart)
        {
            TerritoryInfo territory = Util.GetTerritoryAt(new Vector3(zone.X, 0f, zone.Z));
            string owner = territory?.OwnerTeamId;
            if (string.IsNullOrEmpty(owner))
            {
                Debug.Log($"[RaidSystem] Janela de raid fechada em {zone.Name}: castelo sem dono.");
                return;
            }
            if (territory.LastConquestTimestamp >= ToUnix(windowStart))
            {
                Debug.Log($"[RaidSystem] Janela de raid fechada em {zone.Name}: conquistado nesta janela por {owner}, sem defesa.");
                return;
            }

            int charges = Math.Max(0, RaidSystemPlugin.DefenseTributeCharges.Value);
            if (charges > 0)
            {
                DataStore.Modify(d =>
                {
                    TerritoryInfo t = d.Territories.FirstOrDefault(x => string.Equals(x.Name, territory.Name, StringComparison.OrdinalIgnoreCase));
                    if (t != null) t.PendingTribute += charges;
                });
                RPCManager.BroadcastFullSync();
            }

            List<string> defenders = new List<string>();
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                ZDO zdo = peer == null || peer.m_characterID.IsNone() ? null : ZDOMan.instance.GetZDO(peer.m_characterID);
                if (zdo == null) continue;
                long id = zdo.GetLong(ZDOVars.s_playerID, 0L);
                string name = zdo.GetString(ZDOVars.s_playerName, peer.m_playerName);
                if (id == 0L || !string.Equals(GuildsIntegration.GetPlayerTeam(id), owner, StringComparison.OrdinalIgnoreCase)) continue;
                ScoreManager.RecordCastleDefense(id.ToString(), name, owner);
                defenders.Add(name);
            }

            string text = $"<color=#ffd700>{owner}</color> defendeu o castelo {zone.Name}!" +
                          (charges > 0 ? $" +{charges} carga(s) de tributo na RaidWard." : string.Empty);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ShowMessage", (int)MessageHud.MessageType.Center, text);
            Debug.Log($"[RaidSystem] Defesa do castelo {zone.Name} por {owner}: +{charges} tributo, " +
                      $"pontos para {defenders.Count} membro(s) online ({string.Join(", ", defenders)}).");
        }

        private static long ToUnix(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();

        public static void Reset() => _windowStart.Clear();
    }
}
