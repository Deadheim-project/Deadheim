using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Lado servidor do PvP: ranking K/D, marca de PK, recompensa de defesa, desafio
    /// (cacado) e a entrega do estado a cada cliente. O cliente da vitima conta como
    /// morreu; quem foi o matador o servidor resolve pela ZDO, nunca pelo que o
    /// cliente escreve no pacote.
    /// </summary>
    internal static class PvpServer
    {
        private static readonly Dictionary<string, double> _defenseRewardAt = new Dictionary<string, double>();
        private static readonly Dictionary<long, float> _pendingHello = new Dictionary<long, float>();
        private static float _nextTick;

        private static double Now => PvpState.Now;

        public static bool IsServer => ZNet.instance != null && ZNet.instance.IsServer();

        public static void Handle(long sender, string op, ZPackage pkg)
        {
            if (!PvpConfig.Active) return;
            if (!PvpPeer.TryResolve(sender, out PvpPeer peer))
            {
                // O hello sai no OnSpawned, as vezes antes da ZDO do personagem chegar aqui.
                // Descartar deixaria o jogador sem PK, desafio e diretorio de cla ate relogar.
                if (op == PvpNet.OpHello)
                {
                    _pendingHello[sender] = Time.time + 30f;
                    return;
                }
                Debug.LogWarning($"[Deadheim PvP] '{op}' de um peer sem personagem ({sender}); ignorado.");
                return;
            }

            switch (op)
            {
                case PvpNet.OpHello: OnHello(peer); break;
                case PvpNet.OpDeath: OnDeath(peer, pkg); break;
                case PvpNet.OpChallenge: PvpChallenge.OnCommand(peer, pkg.ReadString()); break;
                case PvpNet.OpRank: SendRank(peer); break;
                default:
                    Debug.LogWarning($"[Deadheim PvP] Operacao desconhecida '{op}' de {peer.Name}.");
                    break;
            }
        }

        // ------------------------------------------------------------------- hello

        private static void OnHello(PvpPeer peer)
        {
            PvpStore.Player(peer.PlayerId, peer.Name);
            SendState(peer);
            Debug.Log($"[Deadheim PvP] {peer.Name} ({peer.PlayerId}) sincronizado.");
        }

        public static void SendState(PvpPeer peer)
        {
            PvpPlayerRecord record = PvpStore.Player(peer.PlayerId, peer.Name);
            PvpChallenge.Remaining(peer.PlayerId, out double pending, out double hunted);

            ZPackage pkg = PvpNet.Package(PvpNet.OpState);
            pkg.Write(Math.Max(0d, record.pkUntil - Now));
            pkg.Write(pending);
            pkg.Write(hunted);
            pkg.Write(Math.Max(0d, record.challengeReadyAt - Now));
            PvpNet.SendToClient(peer.PeerId, pkg);
        }

        public static void SendState(long playerId)
        {
            if (PvpPeer.TryFindByPlayerId(playerId, out PvpPeer peer)) SendState(peer);
        }

        // ------------------------------------------------------------------- morte

        private static void OnDeath(PvpPeer victim, ZPackage pkg)
        {
            ZDOID killerZdo = pkg.ReadZDOID();
            bool arena = pkg.ReadBool();
            string castle = pkg.ReadString();
            if (string.IsNullOrEmpty(castle)) castle = null;
            bool killerDefendingCastle = pkg.ReadBool() && castle != null;
            Vector3 position = pkg.ReadVector3();

            long killerId = 0L;
            string killerName = null;
            ZDO killer = killerZdo.IsNone() || ZDOMan.instance == null ? null : ZDOMan.instance.GetZDO(killerZdo);
            if (killer != null)
            {
                killerId = killer.GetLong(ZDOVars.s_playerID, 0L);
                killerName = killer.GetString(ZDOVars.s_playerName, "?");
            }
            if (killerId == victim.PlayerId) killerId = 0L;

            PvpPlayerRecord victimRecord = PvpStore.Player(victim.PlayerId, victim.Name);
            bool victimWasPk = victimRecord.pkUntil > Now;
            bool victimWasHunted = PvpChallenge.IsHunted(victim.PlayerId);

            Debug.Log($"[Deadheim PvP] Morte: {victim.Name} por {(killerId != 0L ? killerName : "PvE")} " +
                      $"arena={arena} castelo={castle ?? "-"} defesaDoCastelo={killerDefendingCastle} " +
                      $"vitimaPK={victimWasPk} vitimaCacada={victimWasHunted} pos=({position.x:F0},{position.z:F0})");

            PvpChallenge.OnDeath(victim, killerId, killerName);

            if (victimWasPk && PvpConfig.PkClearsOnDeath.Value)
            {
                victimRecord.pkUntil = 0d;
                PvpStore.MarkDirty();
            }

            if (killerId != 0L)
            {
                PvpPlayerRecord killerRecord = PvpStore.Player(killerId, killerName);

                if (!arena || PvpConfig.ArenaCountsLeaderboard.Value)
                {
                    killerRecord.kills++;
                    victimRecord.deaths++;
                    PvpStore.MarkDirty();
                }

                // Arena e castelo sao lugar de lutar: matar ali nao faz de ninguem PK.
                bool pk = !arena && castle == null && !victimWasPk && !victimWasHunted
                          && PvpConfig.PkMinutes.Value > 0f;
                if (pk)
                {
                    killerRecord.pkUntil = Now + PvpConfig.PkMinutes.Value * 60d;
                    PvpStore.MarkDirty();
                    Debug.Log($"[Deadheim PvP] {killerName} ({killerId}) agora e PK por {PvpConfig.PkMinutes.Value:0.#} min.");
                    if (PvpPeer.TryFindByPlayerId(killerId, out PvpPeer killerPeer))
                    {
                        SendState(killerPeer);
                        PvpNet.Message(killerPeer.PeerId,
                            $"<color=#ff5050>Voce e PK por {PvpConfig.PkMinutes.Value:0} min.</color> " +
                            $"Se morrer, perde {PvpConfig.PkSkillLossMultiplier.Value:0.#}x skill.");
                    }
                }

                if (killerDefendingCastle) GiveCastleReward(killerId, killerName, victim, castle, position);

                PvpBridge.RaiseKilled(new PvpKill
                {
                    KillerId = killerId,
                    KillerName = killerName,
                    VictimId = victim.PlayerId,
                    VictimName = victim.Name,
                    Position = position,
                    Arena = arena,
                    Castle = castle,
                    KillerDefendingCastle = killerDefendingCastle,
                });

                if (PvpConfig.KillFeed.Value)
                {
                    string where = arena ? " na arena"
                        : killerDefendingCastle ? $" defendendo o castelo {castle}"
                        : castle != null ? $" no castelo {castle}"
                        : string.Empty;
                    string tag = pk ? " <color=#ff5050>[PK]</color>" : string.Empty;
                    PvpNet.Broadcast($"<color=#ffb347>{killerName}</color>{tag} matou <color=#ffb347>{victim.Name}</color>{where}.");
                }
            }

            SendState(victim);
        }

        private static void GiveCastleReward(long killerId, string killerName, PvpPeer victim, string castle, Vector3 position)
        {
            if (WorldGenerator.instance == null) return;
            Heightmap.Biome biome = WorldGenerator.instance.GetBiome(position);
            int amount = PvpConfig.CastleRewardFor(biome);
            string item = PvpConfig.CastleRewardItem.Value;
            if (amount <= 0 || string.IsNullOrEmpty(item)) return;

            string key = killerId + ":" + victim.PlayerId;
            double cooldown = PvpConfig.CastleRewardCooldownMinutes.Value * 60d;
            if (_defenseRewardAt.TryGetValue(key, out double last) && Now - last < cooldown)
            {
                if (PvpPeer.TryFindByPlayerId(killerId, out PvpPeer peer))
                    PvpNet.Message(peer.PeerId, $"{victim.Name} ja rendeu recompensa de defesa ha pouco tempo.", false);
                return;
            }
            _defenseRewardAt[key] = Now;

            if (PvpPeer.TryFindByPlayerId(killerId, out PvpPeer killerPeer))
                SendReward(killerPeer.PeerId, item, amount, $"Defesa do castelo {castle} ({biome})");
            Debug.Log($"[Deadheim PvP] Recompensa de defesa: {killerName} +{amount} {item} ({biome}, castelo {castle}).");
        }

        public static void SendReward(long peerId, string prefab, int amount, string reason)
        {
            if (string.IsNullOrEmpty(prefab) || amount <= 0) return;
            ZPackage pkg = PvpNet.Package(PvpNet.OpReward);
            pkg.Write(prefab);
            pkg.Write(amount);
            pkg.Write(reason ?? string.Empty);
            PvpNet.SendToClient(peerId, pkg);
        }

        public static void SendReward(long peerId, PvpConfig.Reward reward, string reason)
        {
            if (reward.IsValid) SendReward(peerId, reward.Prefab, reward.Amount, reason);
        }

        // ------------------------------------------------------------------ ranking

        private static void SendRank(PvpPeer peer)
        {
            List<PvpPlayerRecord> ranked = PvpStore.Data.players
                .Where(p => p.kills > 0 || p.deaths > 0)
                .OrderByDescending(p => p.kills)
                .ThenByDescending(p => p.Ratio)
                .ThenBy(p => p.deaths)
                .ToList();

            int size = Mathf.Clamp(PvpConfig.LeaderboardSize.Value, 1, 50);
            List<string> lines = new List<string>();
            for (int i = 0; i < ranked.Count && i < size; i++)
                lines.Add(FormatRank(i + 1, ranked[i], ranked[i].id == peer.PlayerId));

            int own = ranked.FindIndex(p => p.id == peer.PlayerId);
            string mine = own >= 0
                ? FormatRank(own + 1, ranked[own], true)
                : "Voce ainda nao tem abates nem mortes em PvP.";

            ZPackage pkg = PvpNet.Package(PvpNet.OpRankResult);
            pkg.Write(lines.Count);
            foreach (string line in lines) pkg.Write(line);
            pkg.Write(mine);
            PvpNet.SendToClient(peer.PeerId, pkg);
        }

        private static string FormatRank(int position, PvpPlayerRecord record, bool highlight)
        {
            string name = highlight ? "<color=#ffd700>" + record.name + "</color>" : record.name;
            return $"{position,2}. {name}  K {record.kills}  D {record.deaths}  K/D {record.Ratio:0.00}";
        }

        // -------------------------------------------------------------------- tick

        public static void Update()
        {
            if (!IsServer || !PvpConfig.Active) return;
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 1f;

            ReportStartZoneOnce();
            RetryPendingHellos();
            PvpChallenge.Tick();
            PvpStore.Tick();
        }

        private static void RetryPendingHellos()
        {
            if (_pendingHello.Count == 0) return;
            foreach (KeyValuePair<long, float> pending in new List<KeyValuePair<long, float>>(_pendingHello))
            {
                if (PvpPeer.TryResolve(pending.Key, out PvpPeer peer))
                {
                    _pendingHello.Remove(pending.Key);
                    OnHello(peer);
                }
                else if (Time.time > pending.Value
                         || (ZNet.instance.GetPeer(pending.Key) == null && pending.Key != ZRoutedRpc.instance.m_id))
                {
                    _pendingHello.Remove(pending.Key);
                    Debug.LogWarning($"[Deadheim PvP] Hello de {pending.Key} sem personagem depois de 30 s; desisti.");
                }
            }
        }

        private static bool _startZoneReported;

        /// <summary>
        /// Uma linha no log do servidor com o templo inicial e o tamanho da zona segura:
        /// e o que o admin precisa para escrever ArenaZones/SafeZones com coordenadas certas.
        /// </summary>
        private static void ReportStartZoneOnce()
        {
            if (_startZoneReported || !PvpZones.TryGetStartCenter(out Vector3 temple)) return;
            _startZoneReported = true;
            float started = Time.realtimeSinceStartup;
            bool safe = PvpZones.IsOnStartIsland(temple);
            Debug.Log($"[Deadheim PvP] Templo inicial em x={temple.x:F0} z={temple.z:F0}. " +
                      $"Zona segura inicial: {PvpZones.DescribeIsland()} templo_seguro={safe} " +
                      $"calculo={(Time.realtimeSinceStartup - started) * 1000f:F0}ms");
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.SaveWorld))]
        private static class SaveWorldPatch
        {
            private static void Postfix()
            {
                if (IsServer) PvpStore.SaveIfDirty();
            }
        }

        [HarmonyPatch(typeof(ZNet), "OnDestroy")]
        private static class ShutdownPatch
        {
            private static void Prefix()
            {
                if (IsServer) PvpStore.Unload();
                PvpChallenge.Reset();
                _pendingHello.Clear();
                _defenseRewardAt.Clear();
                _startZoneReported = false;
            }
        }

        /// <summary>
        /// O cacado aparece no mapa de todo mundo. Em vez de desenhar marcador proprio,
        /// reaproveitamos a lista de jogadores que o servidor ja manda a cada 2 s: basta
        /// marcar a posicao dele como publica, e o mapa do vanilla faz o resto.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), "UpdatePlayerList")]
        private static class HuntedOnMapPatch
        {
            private static void Postfix(ZNet __instance)
            {
                if (!__instance.IsServer() || !PvpConfig.Active) return;
                if (!PvpChallenge.AnyHunted) return;

                List<ZNet.PlayerInfo> players = __instance.m_players;
                foreach (ZNetPeer peer in __instance.GetPeers())
                {
                    if (peer == null || !peer.IsReady()) continue;
                    if (!PvpPeer.TryFrom(peer, out PvpPeer info) || !PvpChallenge.IsHunted(info.PlayerId)) continue;

                    for (int i = 0; i < players.Count; i++)
                    {
                        if (players[i].m_characterID != peer.m_characterID) continue;
                        ZNet.PlayerInfo entry = players[i];
                        entry.m_publicPosition = true;
                        entry.m_position = peer.m_refPos;
                        players[i] = entry;
                    }
                }
            }
        }
    }
}
