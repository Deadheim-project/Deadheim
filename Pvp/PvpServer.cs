using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Lado servidor do PvP: ranking K/D, marca de PK (com niveis), recompensa de defesa,
    /// bounty (cacado) e a entrega do estado a cada cliente. O cliente da vitima conta como
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

        /// <summary>Admin pela adminlist do servidor. O host de um servidor com jogador e admin.</summary>
        public static bool IsAdminPeer(long peerId)
        {
            ZNet net = ZNet.instance;
            if (net == null) return false;
            if (ZRoutedRpc.instance != null && peerId == ZRoutedRpc.instance.m_id) return true;
            ZNetPeer peer = net.GetPeer(peerId);
            return peer?.m_socket != null && net.IsAdmin(peer.m_socket.GetHostName());
        }

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
                case PvpNet.OpBounty:
                {
                    string action = pkg.ReadString();
                    string target = pkg.ReadString();
                    int paid = pkg.ReadInt();
                    int request = pkg.GetPos() < pkg.Size() ? pkg.ReadInt() : 0;
                    PvpBounty.OnCommand(peer, action, target, paid, request);
                    break;
                }
                case PvpNet.OpRank: SendRank(peer); break;
                case PvpNet.OpPve: PvpPve.OnCommand(peer, pkg.ReadString(), pkg.ReadString()); break;
                case PvpNet.OpAdmin: OnAdmin(peer, pkg.ReadString(), pkg.ReadString(), pkg.ReadDouble()); break;
                default:
                    Debug.LogWarning($"[Deadheim PvP] Operacao desconhecida '{op}' de {peer.Name}.");
                    break;
            }
        }

        // ------------------------------------------------------------------- hello

        private static void OnHello(PvpPeer peer)
        {
            PvpPlayerRecord record = PvpStore.Player(peer.PlayerId, peer.Name);
            SendState(peer);
            Debug.Log($"[Deadheim PvP] {peer.Name} ({peer.PlayerId}) sincronizado.");

            if (record.pendingCoins > 0)
            {
                SendReward(peer.PeerId, "Coins", record.pendingCoins, "Moedas guardadas enquanto voce estava fora");
                Debug.Log($"[Deadheim PvP] {peer.Name} recebeu {record.pendingCoins} moedas pendentes.");
                record.pendingCoins = 0;
                PvpStore.MarkDirty();
            }

            if (record.combatLogPending)
            {
                record.combatLogPending = false;
                PvpStore.MarkDirty();
                if (string.Equals(PvpConfig.CombatLogout.Value, "Death", StringComparison.OrdinalIgnoreCase))
                {
                    PvpNet.SendToClient(peer.PeerId, PvpNet.Package(PvpNet.OpPunish));
                    Debug.Log($"[Deadheim PvP] {peer.Name} voltou depois de deslogar em combate: morre onde saiu.");
                }
            }
        }

        public static void SendState(PvpPeer peer)
        {
            PvpPlayerRecord record = PvpStore.Player(peer.PlayerId, peer.Name);
            PvpBounty.Describe(peer.PlayerId, out double pending, out double hunted, out int pot, out bool untilDeath);
            bool pk = record.IsPk;

            ZPackage pkg = PvpNet.Package(PvpNet.OpState);
            pkg.Write(pk && !record.pkPermanent ? Math.Max(0d, record.pkLeft) : 0d);
            pkg.Write(pk && record.pkPermanent);
            pkg.Write(pk ? record.pkPenalty : 0);
            pkg.Write(record.pkKills);
            pkg.Write(pending);
            pkg.Write(hunted);
            pkg.Write(pot);
            pkg.Write(untilDeath);
            pkg.Write(PvpBounty.IsPaused(peer.PlayerId));
            pkg.Write(Math.Max(0d, record.bountyReadyAt - Now));
            pkg.Write(record.pvePermanent);
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
            bool victimWasAggressor = pkg.ReadBool();
            int coinsDropped = pkg.ReadInt();
            int cargoDropped = pkg.ReadInt();

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
            bool victimWasPk = victimRecord.IsPk;
            bool victimWasHunted = PvpBounty.IsHunted(victim.PlayerId);

            Debug.Log($"[Deadheim PvP] Morte: {victim.Name} por {(killerId != 0L ? killerName : "PvE")} " +
                      $"arena={arena} castelo={castle ?? "-"} defesaDoCastelo={killerDefendingCastle} " +
                      $"vitimaPK={victimWasPk} vitimaCacada={victimWasHunted} vitimaAgressora={victimWasAggressor} " +
                      $"moedasNoChao={coinsDropped} cargaNoChao={cargoDropped} pos=({position.x:F0},{position.z:F0})");

            PvpBounty.OnDeath(victim, killerId, killerName, arena);

            // A marca sai morto por jogador. Morte PvE so tira o PK comum com PkClearsOnPveDeath: senao
            // o PK morreria de proposito em casa para ficar limpo. O permanente so sai morto por jogador.
            bool clearsPk = killerId != 0L
                ? victimRecord.pkPermanent || PvpConfig.PkClearsOnDeath.Value
                : !victimRecord.pkPermanent && PvpConfig.PkClearsOnPveDeath.Value;
            if (victimWasPk && clearsPk)
            {
                bool wasPermanent = victimRecord.pkPermanent;
                victimRecord.ClearPk();
                PvpStore.MarkDirty();
                if (wasPermanent)
                    PvpNet.Broadcast($"<color=#ff3030>O PK permanente {victim.Name}</color> foi morto por <color=#ffb347>{killerName}</color>.", true);
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

                // Arena e castelo sao lugar de lutar: matar ali nao faz de ninguem PK. Matar quem
                // atacou primeiro (agressor) e legitima defesa.
                bool selfDefense = victimWasAggressor && PvpConfig.AggressorRule.Value;
                bool pk = !arena && castle == null && !victimWasPk && !victimWasHunted && !selfDefense
                          && PvpConfig.Tiers.Count > 0;
                if (selfDefense && !arena && castle == null)
                    Debug.Log($"[Deadheim PvP] {killerName} matou {victim.Name}, que era agressor: legitima defesa, sem PK.");
                if (pk) MarkPk(killerId, killerName, killerRecord);

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
                    string tag = pk ? $" <color=#ff5050>[PK #{killerRecord.pkKills}]</color>" : string.Empty;
                    string loot = LootText(coinsDropped, cargoDropped);
                    PvpNet.Broadcast($"<color=#ffb347>{killerName}</color>{tag} matou <color=#ffb347>{victim.Name}</color>{where}{loot}.");
                }
            }

            SendState(victim);
        }

        private static string LootText(int coins, int cargo)
        {
            if (coins <= 0 && cargo <= 0) return string.Empty;
            string what = coins > 0 && cargo > 0 ? $"{coins} moedas e {cargo} itens"
                : coins > 0 ? $"{coins} moedas" : $"{cargo} itens";
            return " e deixou " + what + " no chao";
        }

        /// <summary>
        /// Mais um abate que deu PK: sobe o nivel (PkTiers) pela sequencia desde que a marca
        /// atual comecou. Nivel permanente so sai com a morte por jogador.
        /// </summary>
        private static void MarkPk(long killerId, string killerName, PvpPlayerRecord record)
        {
            if (!record.IsPk) record.pkStreak = 0;
            record.pkStreak++;
            record.pkKills++;
            if (!PvpConfig.TryGetTier(record.pkStreak, out PvpConfig.PkTier tier)) return;

            if (tier.Permanent) record.pkPermanent = true;
            else if (!record.pkPermanent) record.pkLeft = Math.Max(record.pkLeft, tier.Minutes * 60d);
            record.pkPenalty = (int)tier.Penalty;
            PvpStore.MarkDirty();

            string length = record.pkPermanent ? "PERMANENTE (ate ser morto por um jogador)" : PkLengthText(record.pkLeft);
            Debug.Log($"[Deadheim PvP] {killerName} ({killerId}) agora e PK {length}, sequencia {record.pkStreak}, " +
                      $"perda={tier.Penalty} (contador de PK: {record.pkKills}).");
            if (PvpPeer.TryFindByPlayerId(killerId, out PvpPeer killerPeer))
            {
                SendState(killerPeer);
                PvpNet.Message(killerPeer.PeerId,
                    $"<color=#ff5050>Voce e PK {length}.</color> Se morrer, perde {PvpConfig.PkSkillLossMultiplier.Value:0.#}x skill" +
                    PenaltyText(tier.Penalty) + ".");
            }
            if (record.pkPermanent && tier.Permanent && record.pkStreak == tier.Kills)
                PvpNet.Broadcast($"<color=#ff3030>{killerName} virou PK PERMANENTE</color> e aparece no mapa ate ser morto.", true);
        }

        private static string PkLengthText(double seconds)
            => PvpClient.FormatDuration(seconds) + (PvpConfig.PkTimeOnlineOnly.Value ? " (tempo online)" : string.Empty);

        public static string PenaltyText(PvpConfig.PkPenalty penalty)
        {
            switch (penalty)
            {
                case PvpConfig.PkPenalty.Unequipped: return " e tudo que nao estiver equipado cai no chao";
                case PvpConfig.PkPenalty.All: return " e o inventario inteiro cai no chao";
                default: return string.Empty;
            }
        }

        /// <summary>Moedas para um jogador: na hora se estiver online, senao quando ele voltar.</summary>
        public static void PayCoins(long playerId, string name, int amount, string reason)
        {
            if (amount <= 0 || playerId == 0L) return;
            if (PvpPeer.TryFindByPlayerId(playerId, out PvpPeer peer))
            {
                SendReward(peer.PeerId, "Coins", amount, reason);
                return;
            }
            PvpStore.Player(playerId, name).pendingCoins += amount;
            PvpStore.MarkDirty();
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
            string pk = record.pkKills > 0 ? $"  <color=#ff5050>PK {record.pkKills}</color>" : string.Empty;
            return $"{position,2}. {name}  K {record.kills}  D {record.deaths}  K/D {record.Ratio:0.00}{pk}";
        }

        // -------------------------------------------------------------------- tick

        public static void Update()
        {
            if (!IsServer || !PvpConfig.Active) return;
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 1f;

            ReportStartZoneOnce();
            RetryPendingHellos();
            RememberPeers();
            TickPkTimers();
            PvpBounty.Tick();
            PvpStore.Tick();
        }

        // ---------------------------------------------------------------- tempo de PK

        private static float _lastPkTick = -1f;

        /// <summary>
        /// Desconta o tempo de PK. Com PkTimeOnlineOnly so de quem esta online: deslogar nao
        /// limpa a marca. Quando acaba, o servidor avisa o jogador.
        /// </summary>
        private static void TickPkTimers()
        {
            float now = Time.realtimeSinceStartup;
            // Teto de 10 s: um engasgo longo do servidor nao conta como tempo cumprido de uma vez.
            double elapsed = _lastPkTick < 0f ? 0d : Mathf.Clamp(now - _lastPkTick, 0f, 10f);
            _lastPkTick = now;
            if (elapsed <= 0d) return;

            List<PvpPlayerRecord> players = PvpStore.Data.players;
            if (PvpConfig.PkTimeOnlineOnly.Value)
            {
                foreach (PvpPeer peer in PvpPeer.Online())
                {
                    PvpPlayerRecord record = players.Find(p => p.id == peer.PlayerId);
                    if (record != null) TickPk(record, elapsed);
                }
            }
            else
            {
                foreach (PvpPlayerRecord record in players.ToArray()) TickPk(record, elapsed);
            }
        }

        private static void TickPk(PvpPlayerRecord record, double elapsed)
        {
            if (record.pkPermanent || record.pkLeft <= 0d) return;
            PvpStore.MarkDirty();
            if (!record.TickPk(elapsed)) return;
            Debug.Log($"[Deadheim PvP] Marca de PK de {record.name} ({record.id}) acabou.");
            if (PvpPeer.TryFindByPlayerId(record.id, out PvpPeer peer))
            {
                SendState(peer);
                PvpNet.Message(peer.PeerId, "<color=#7CFC00>Sua marca de PK acabou.</color>");
            }
        }

        // ----------------------------------------------------------------- admin

        /// <summary>
        /// pk &lt;minutos&gt; [jogador]: marca (minutos &gt; 0), torna permanente (-1) ou limpa (0) o PK
        /// de alguem, ou do proprio admin sem jogador. Moderacao e teste.
        /// </summary>
        private static void OnAdmin(PvpPeer admin, string action, string target, double value)
        {
            if (!IsAdminPeer(admin.PeerId)) { PvpNet.Message(admin.PeerId, "So admin."); return; }
            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "pk":
                {
                    long id = admin.PlayerId;
                    string name = admin.Name;
                    if (!string.IsNullOrWhiteSpace(target) && !PvpBounty.TryFindTarget(target, out id, out name))
                    {
                        PvpNet.Message(admin.PeerId, $"Nao conheco nenhum jogador chamado '{target}'.");
                        return;
                    }
                    PvpPlayerRecord record = PvpStore.Player(id, name);
                    if (value == 0d) record.ClearPk();
                    else if (value < 0d)
                    {
                        record.pkPermanent = true;
                        if (PvpConfig.TryGetTier(int.MaxValue, out PvpConfig.PkTier top) && top.Permanent) record.pkPenalty = (int)top.Penalty;
                    }
                    else
                    {
                        record.pkPermanent = false;
                        record.pkLeft = value * 60d;
                    }
                    if (record.IsPk && record.pkStreak <= 0) record.pkStreak = 1;
                    PvpStore.MarkDirty();
                    SendState(id);
                    string state = !record.IsPk ? "sem PK" : record.pkPermanent ? "PK PERMANENTE" : "PK por " + PkLengthText(record.pkLeft);
                    PvpNet.Message(admin.PeerId, $"{name}: {state}.");
                    Debug.Log($"[Deadheim PvP] Admin {admin.Name} deixou {name} ({id}) {state}.");
                    break;
                }
                default:
                    PvpNet.Message(admin.PeerId, $"Comando de admin desconhecido: {action}.");
                    break;
            }
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

        // ------------------------------------------------------ deslogar em combate

        private struct SeenPeer
        {
            public long PlayerId;
            public string Name;
            public PvpFlags Flags;
            public long LastAttacker;
            public bool Dead;
            public Vector3 Position;
        }

        // Ultimo estado visto de cada peer: quando ele cai, a ZDO do personagem pode ja ter sumido.
        private static readonly Dictionary<long, SeenPeer> _seen = new Dictionary<long, SeenPeer>();

        private static bool TryRead(ZNetPeer peer, out SeenPeer seen)
        {
            seen = default;
            ZDO zdo = peer == null || peer.m_characterID.IsNone() || ZDOMan.instance == null
                ? null : ZDOMan.instance.GetZDO(peer.m_characterID);
            if (zdo == null) return false;
            seen = new SeenPeer
            {
                PlayerId = zdo.GetLong(ZDOVars.s_playerID, 0L),
                Name = zdo.GetString(ZDOVars.s_playerName, peer.m_playerName),
                Flags = (PvpFlags)zdo.GetInt(PvpState.ZdoFlags, 0),
                LastAttacker = zdo.GetLong(PvpState.ZdoLastAttacker, 0L),
                Dead = zdo.GetBool(ZDOVars.s_dead),
                Position = zdo.GetPosition(),
            };
            return seen.PlayerId != 0L;
        }

        private static void RememberPeers()
        {
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
                if (peer != null && peer.IsReady() && TryRead(peer, out SeenPeer seen)) _seen[peer.m_uid] = seen;
        }

        /// <summary>
        /// Saiu do jogo em combate (Alt+F4, desligar a internet): o personagem some na hora,
        /// sem tumba e sem morte. CombatLogout decide o preco: Rank conta a morte para ele e o
        /// abate para quem bateu; Death tambem o mata onde saiu quando ele voltar.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        private static class CombatLogoutPatch
        {
            private static void Prefix(ZNet __instance, ZNetPeer peer)
            {
                try
                {
                    if (!__instance.IsServer() || !PvpConfig.Active || peer == null) return;
                    bool live = TryRead(peer, out SeenPeer seen);
                    if (!live && !_seen.TryGetValue(peer.m_uid, out seen)) return;
                    _seen.Remove(peer.m_uid);
                    OnDisconnect(seen);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Deadheim PvP] Checagem de deslogar em combate falhou: " + ex);
                }
            }
        }

        private static void OnDisconnect(SeenPeer seen)
        {
            string mode = PvpConfig.CombatLogout.Value ?? "Off";
            if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase)) return;
            if (seen.Dead || (seen.Flags & PvpFlags.Combat) == 0 || (seen.Flags & PvpFlags.Arena) != 0) return;

            PvpPlayerRecord record = PvpStore.Player(seen.PlayerId, seen.Name);
            record.deaths++;
            if (string.Equals(mode, "Death", StringComparison.OrdinalIgnoreCase)) record.combatLogPending = true;

            string killerName = null;
            if (seen.LastAttacker != 0L && seen.LastAttacker != seen.PlayerId)
            {
                PvpPlayerRecord killer = PvpStore.Data.players.Find(p => p.id == seen.LastAttacker);
                if (killer != null)
                {
                    killer.kills++;
                    killerName = killer.name;
                    PvpBridge.RaiseKilled(new PvpKill
                    {
                        KillerId = killer.id,
                        KillerName = killer.name,
                        VictimId = seen.PlayerId,
                        VictimName = seen.Name,
                        Position = seen.Position,
                        Castle = PvpBridge.Castle(seen.Position),
                    });
                }
            }
            PvpStore.MarkDirty();

            Debug.Log($"[Deadheim PvP] {seen.Name} ({seen.PlayerId}) deslogou em combate. Abate para {killerName ?? "ninguem"}, modo={mode}.");
            PvpNet.Broadcast($"<color=#ffb347>{seen.Name}</color> deslogou em combate" +
                             (killerName != null ? $" e conta como morto por <color=#ffb347>{killerName}</color>." : "."));
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
                PvpBounty.Reset();
                _pendingHello.Clear();
                _defenseRewardAt.Clear();
                _seen.Clear();
                _startZoneReported = false;
            }
        }

        /// <summary>
        /// O cacado (e o PK permanente) aparece no mapa de todo mundo. Em vez de desenhar marcador proprio,
        /// reaproveitamos a lista de jogadores que o servidor ja manda a cada 2 s: basta
        /// marcar a posicao dele como publica, e o mapa do vanilla faz o resto.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), "UpdatePlayerList")]
        private static class HuntedOnMapPatch
        {
            private static void Postfix(ZNet __instance)
            {
                if (!__instance.IsServer() || !PvpConfig.Active) return;
                bool anyPermanent = PvpConfig.PkPermanentOnMap.Value && PvpStore.Data.players.Exists(p => p.pkPermanent);
                if (!PvpBounty.AnyHunted && !anyPermanent) return;

                List<ZNet.PlayerInfo> players = __instance.m_players;
                foreach (ZNetPeer peer in __instance.GetPeers())
                {
                    if (peer == null || !peer.IsReady()) continue;
                    if (!PvpPeer.TryFrom(peer, out PvpPeer info)) continue;
                    bool permanentPk = anyPermanent && PvpStore.Player(info.PlayerId, info.Name).pkPermanent;
                    if (!PvpBounty.IsHunted(info.PlayerId) && !permanentPk) continue;

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
