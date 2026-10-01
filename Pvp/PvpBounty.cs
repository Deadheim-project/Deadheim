using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Deadheim.Pvp
{
    [Serializable]
    internal sealed class PvpBountyContribution
    {
        public long id;
        public string name;
        public int amount;
    }

    /// <summary>Uma cabeca a premio. Os relogios sao segundos de alvo ONLINE, nao de parede.</summary>
    [Serializable]
    internal sealed class PvpBountyRecord
    {
        public long targetId;
        public string targetName;
        public int pot;
        /// <summary>Segundos online que faltam para o alvo virar CACADO (o aviso para fugir).</summary>
        public double delayLeft;
        /// <summary>Segundos online ja cumpridos como CACADO.</summary>
        public double elapsed;
        public List<PvpBountyContribution> contributions = new List<PvpBountyContribution>();
    }

    /// <summary>
    /// Bounty: um jogador paga moedas para outro ser cacado. Quem matar o alvo fica com
    /// BountyKillerSharePercent do pote; o resto e da casa. O tempo cresce com o pote e so
    /// corre com o alvo online; a partir de BountyUntilDeathAt so acaba com a morte dele.
    /// O alvo pode pagar o pote x BountyBuyoutMultiplier para a casa e se livrar.
    ///
    /// Tudo decidido aqui no servidor. O cliente so tira as moedas do proprio inventario ao
    /// pedir (o inventario do Valheim e do cliente); se o pedido e recusado, as moedas voltam.
    /// Sem recompensa por sobreviver: nada sai do nada, entao combinar morte nao gera ouro.
    /// </summary>
    internal static class PvpBounty
    {
        private sealed class Runtime
        {
            public bool Paused;
            public double LastTick;
        }

        private const string Coins = "Coins";
        private static readonly Dictionary<long, Runtime> _runtime = new Dictionary<long, Runtime>();
        private static float _nextSaveMark;

        private static double Now => PvpState.Now;

        private static List<PvpBountyRecord> All => PvpStore.Data.bounties;

        private static PvpBountyRecord Find(long targetId) => All.Find(b => b.targetId == targetId);

        private static int OnlinePlayers => ZNet.instance != null ? ZNet.instance.GetNrOfPlayers() : 0;

        public static bool AnyHunted
        {
            get
            {
                foreach (PvpBountyRecord bounty in All)
                    if (bounty.delayLeft <= 0d) return true;
                return false;
            }
        }

        public static bool HasBounty(long playerId) => Find(playerId) != null;

        public static bool IsHunted(long playerId)
        {
            PvpBountyRecord bounty = Find(playerId);
            return bounty != null && bounty.delayLeft <= 0d;
        }

        public static bool IsPaused(long playerId)
            => _runtime.TryGetValue(playerId, out Runtime rt) && rt.Paused && Find(playerId) != null;

        public static bool UntilDeath(int pot)
            => PvpConfig.BountyUntilDeathAt.Value > 0 && pot >= PvpConfig.BountyUntilDeathAt.Value;

        public static double TotalSeconds(int pot)
            => Math.Max(0d, pot / 1000d * PvpConfig.BountyMinutesPer1000.Value * 60d);

        /// <summary>Estado da bounty do jogador para o cliente: aviso, cacado, pote.</summary>
        public static void Describe(long playerId, out double pending, out double hunted, out int pot, out bool untilDeath)
        {
            pending = 0d;
            hunted = 0d;
            pot = 0;
            untilDeath = false;
            PvpBountyRecord bounty = Find(playerId);
            if (bounty == null) return;
            pot = bounty.pot;
            untilDeath = UntilDeath(bounty.pot);
            if (bounty.delayLeft > 0d) pending = bounty.delayLeft;
            else hunted = untilDeath ? 0d : Math.Max(0d, TotalSeconds(bounty.pot) - bounty.elapsed);
        }

        public static int BuyoutCost(int pot)
        {
            float multiplier = PvpConfig.BountyBuyoutMultiplier.Value;
            return multiplier > 0f ? Mathf.CeilToInt(pot * multiplier) : 0;
        }

        public static void Reset() => _runtime.Clear();

        // --------------------------------------------------------------- comandos

        /// <summary>
        /// place &lt;alvo&gt; &lt;valor&gt; | pay &lt;valor&gt; | list. Em place e pay o cliente ja tirou
        /// &lt;valor&gt; moedas do inventario: toda recusa devolve.
        /// </summary>
        public static void OnCommand(PvpPeer peer, string action, string target, int paid)
        {
            action = (action ?? string.Empty).Trim().ToLowerInvariant();
            switch (action)
            {
                case "place": Place(peer, target, paid, false); break;
                case "pay": Buyout(peer, paid); break;
                case "admin":
                    // Bounty paga pela casa: ferramenta de admin (e do teste solo). Pode ser em si mesmo.
                    if (!PvpServer.IsAdminPeer(peer.PeerId)) { PvpNet.Message(peer.PeerId, "So admin."); return; }
                    Place(peer, target, paid, true);
                    break;
                default: List(peer); break;
            }
        }

        private static void Refund(PvpPeer peer, int amount, string why)
        {
            if (amount > 0) PvpServer.SendReward(peer.PeerId, Coins, amount, "Devolvido: " + why);
            PvpNet.Message(peer.PeerId, why);
        }

        private static void Place(PvpPeer peer, string targetName, int paid, bool byHouse)
        {
            // A casa paga a bounty de admin: nada foi tirado do inventario, entao nada volta.
            if (byHouse) paid = Math.Max(0, paid);
            void Refuse(string why) => Refund(peer, byHouse ? 0 : paid, why);

            if (!PvpConfig.BountyEnabled.Value) { Refuse("A bounty esta desligada neste servidor."); return; }
            if (paid < PvpConfig.BountyMinimum.Value && !byHouse)
            {
                Refund(peer, paid, $"A bounty minima e {PvpConfig.BountyMinimum.Value} moedas.");
                return;
            }

            if (!TryFindTarget(targetName, out long targetId, out string name))
            {
                Refuse($"Nao conheco nenhum jogador chamado '{targetName}'.");
                return;
            }
            if (targetId == peer.PlayerId && !byHouse) { Refuse("Voce nao pode colocar bounty em si mesmo."); return; }
            if (PvpStore.Player(targetId, name).pvePermanent) { Refuse($"{name} e PvE permanente: nao pode ser cacado."); return; }
            if (paid <= 0) { Refuse("Valor invalido."); return; }

            PvpBountyRecord bounty = Find(targetId);
            if (bounty == null)
            {
                PvpPlayerRecord record = PvpStore.Player(targetId, name);
                if (record.bountyReadyAt > Now && !byHouse)
                {
                    Refuse($"{name} acabou de sair de uma bounty. Outra so em {PvpClient.FormatDuration(record.bountyReadyAt - Now)}.");
                    return;
                }
                bounty = new PvpBountyRecord
                {
                    targetId = targetId,
                    targetName = name,
                    delayLeft = Math.Max(0, PvpConfig.BountyDelaySeconds.Value),
                };
                All.Add(bounty);
            }

            bounty.pot += paid;
            long payerId = byHouse ? 0L : peer.PlayerId;
            PvpBountyContribution mine = bounty.contributions.Find(c => c.id == payerId);
            if (mine == null)
                bounty.contributions.Add(new PvpBountyContribution { id = payerId, name = byHouse ? "casa" : peer.Name, amount = paid });
            else mine.amount += paid;
            PvpStore.MarkDirty();

            string length = UntilDeath(bounty.pot)
                ? "ate morrer para um jogador"
                : $"por {PvpClient.FormatDuration(TotalSeconds(bounty.pot) - bounty.elapsed)} online";
            string when = bounty.delayLeft > 0d ? $" Vira CACADO em {PvpClient.FormatDuration(bounty.delayLeft)}." : string.Empty;
            PvpNet.Broadcast($"<color=#ff8c00>BOUNTY:</color> <color=#ffb347>{bounty.targetName}</color> vale " +
                             $"<color=#ffd700>{Payout(bounty.pot)}</color> moedas para quem matar ({length}).{when}", true);
            PvpNet.Message(peer.PeerId, $"Voce colocou {paid} moedas na cabeca de {bounty.targetName}.");
            if (PvpPeer.TryFindByPlayerId(targetId, out PvpPeer targetPeer))
            {
                PvpNet.Message(targetPeer.PeerId, bounty.delayLeft > 0d
                    ? $"<color=#ff5050>Colocaram {bounty.pot} moedas na sua cabeca.</color> Em {PvpClient.FormatDuration(bounty.delayLeft)} voce vira CACADO: " +
                      "aparece no mapa e perde a zona segura. Ainda da tempo de teleportar." +
                      (BuyoutCost(bounty.pot) > 0 ? $" Ou pague {BuyoutCost(bounty.pot)} com /bounty pagar." : string.Empty)
                    : $"<color=#ff5050>Sua bounty subiu para {bounty.pot} moedas.</color>");
                PvpServer.SendState(targetPeer);
            }
            Debug.Log($"[Deadheim PvP] Bounty: {(byHouse ? "casa (admin " + peer.Name + ")" : peer.Name)} ({peer.PlayerId}) +{paid} " +
                      $"em {bounty.targetName} ({targetId}); pote={bounty.pot}.");
        }

        internal static bool TryFindTarget(string name, out long id, out string resolvedName)
        {
            id = 0L;
            resolvedName = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();
            if (PvpPeer.TryFindByName(name, out PvpPeer online))
            {
                id = online.PlayerId;
                resolvedName = online.Name;
                return true;
            }
            PvpPlayerRecord known = PvpStore.Data.players.Find(p => string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase));
            if (known == null) return false;
            id = known.id;
            resolvedName = known.name;
            return true;
        }

        private static void Buyout(PvpPeer peer, int paid)
        {
            PvpBountyRecord bounty = Find(peer.PlayerId);
            if (bounty == null) { Refund(peer, paid, "Nao ha bounty na sua cabeca."); return; }
            int cost = BuyoutCost(bounty.pot);
            if (cost <= 0) { Refund(peer, paid, "Pagar a propria bounty esta desligado neste servidor."); return; }
            if (paid < cost) { Refund(peer, paid, $"Para encerrar a sua bounty sao {cost} moedas."); return; }

            if (paid > cost) PvpServer.SendReward(peer.PeerId, Coins, paid - cost, "Troco da bounty");
            Finish(bounty);
            PvpNet.Broadcast($"<color=#ff8c00>BOUNTY:</color> <color=#ffb347>{bounty.targetName}</color> pagou {cost} moedas e comprou a propria cabeca.");
            Debug.Log($"[Deadheim PvP] Bounty de {bounty.targetName} comprada por {cost} (pote {bounty.pot}, casa).");
        }

        private static void List(PvpPeer peer)
        {
            List<string> lines = new List<string>();
            foreach (PvpBountyRecord bounty in All)
            {
                string state;
                if (bounty.delayLeft > 0d) state = "cacado em " + PvpClient.FormatDuration(bounty.delayLeft);
                else if (UntilDeath(bounty.pot)) state = "CACADO ate morrer";
                else state = "CACADO por mais " + PvpClient.FormatDuration(TotalSeconds(bounty.pot) - bounty.elapsed);
                bool online = PvpPeer.TryFindByPlayerId(bounty.targetId, out _);
                lines.Add($"{bounty.targetName}: {Payout(bounty.pot)} moedas para quem matar, {state}" + (online ? string.Empty : " (offline, tempo parado)"));
            }
            PvpNet.Message(peer.PeerId, lines.Count == 0 ? "Nenhuma bounty ativa." : string.Join("\n", lines), false);
        }

        private static int Payout(int pot) => Mathf.FloorToInt(pot * Mathf.Clamp(PvpConfig.BountyKillerSharePercent.Value, 0f, 100f) / 100f);

        /// <summary>Encerra a bounty, qualquer que seja o motivo, e comeca a recarga do alvo.</summary>
        private static void Finish(PvpBountyRecord bounty)
        {
            All.Remove(bounty);
            _runtime.Remove(bounty.targetId);
            PvpPlayerRecord record = PvpStore.Player(bounty.targetId, bounty.targetName);
            record.bountyReadyAt = Now + PvpConfig.BountyCooldownMinutes.Value * 60d;
            PvpStore.MarkDirty();
            PvpServer.SendState(bounty.targetId);
        }

        // ------------------------------------------------------------------- morte

        public static void OnDeath(PvpPeer victim, long killerId, string killerName, bool arena)
        {
            PvpBountyRecord bounty = Find(victim.PlayerId);
            if (bounty == null || killerId == 0L) return;
            if (arena && !PvpConfig.BountyArenaKillsCount.Value) return;

            int payout = Payout(bounty.pot);
            Finish(bounty);
            PvpServer.PayCoins(killerId, killerName, payout, "Bounty de " + bounty.targetName);
            PvpNet.Broadcast($"<color=#ff8c00>BOUNTY:</color> <color=#ffb347>{killerName}</color> cacou <color=#ffb347>{bounty.targetName}</color> " +
                             $"e levou <color=#ffd700>{payout}</color> moedas!", true);
            Debug.Log($"[Deadheim PvP] Bounty paga: {killerName} ({killerId}) +{payout} por {bounty.targetName}; casa={bounty.pot - payout}.");
        }

        // -------------------------------------------------------------------- tick

        public static void Tick()
        {
            List<PvpBountyRecord> all = All;
            if (all.Count == 0) return;

            foreach (PvpBountyRecord bounty in new List<PvpBountyRecord>(all))
            {
                if (!_runtime.TryGetValue(bounty.targetId, out Runtime rt))
                    _runtime[bounty.targetId] = rt = new Runtime { LastTick = Now };
                double elapsed = Math.Max(0d, Now - rt.LastTick);
                rt.LastTick = Now;

                // Offline o relogio fica parado: e preciso cumprir o tempo inteiro online.
                if (!PvpPeer.TryFindByPlayerId(bounty.targetId, out PvpPeer peer)) continue;
                UpdatePause(bounty, rt, peer);
                if (rt.Paused)
                {
                    // Parado, o cliente precisa do fim novo a cada segundo, senao a contagem dele anda.
                    PvpServer.SendState(peer);
                    continue;
                }

                if (bounty.delayLeft > 0d)
                {
                    bounty.delayLeft -= elapsed;
                    if (bounty.delayLeft <= 0d)
                    {
                        bounty.delayLeft = 0d;
                        PvpServer.SendState(peer);
                        PvpNet.Broadcast($"<color=#ff8c00>BOUNTY:</color> <color=#ffb347>{bounty.targetName}</color> agora esta " +
                                         $"<color=#ff5050>CACADO</color> e aparece no mapa. {Payout(bounty.pot)} moedas para quem matar.", true);
                    }
                }
                else
                {
                    bounty.elapsed += elapsed;
                    if (!UntilDeath(bounty.pot) && bounty.elapsed >= TotalSeconds(bounty.pot)) Expire(bounty);
                }
            }

            // O estado de quem e cacado muda a cada segundo; gravar a cada 10 s basta.
            if (Time.time >= _nextSaveMark)
            {
                _nextSaveMark = Time.time + 10f;
                PvpStore.MarkDirty();
            }
        }

        private static void Expire(PvpBountyRecord bounty)
        {
            Finish(bounty);
            float refund = Mathf.Clamp(PvpConfig.BountyExpiredRefundPercent.Value, 0f, 100f) / 100f;
            if (refund > 0f)
                foreach (PvpBountyContribution c in bounty.contributions)
                    PvpServer.PayCoins(c.id, c.name, Mathf.FloorToInt(c.amount * refund), "Bounty de " + bounty.targetName + " expirou");
            PvpNet.Broadcast($"<color=#ff8c00>BOUNTY:</color> <color=#ffb347>{bounty.targetName}</color> sobreviveu a bounty.", true);
            Debug.Log($"[Deadheim PvP] Bounty de {bounty.targetName} expirou; devolvido {refund:P0} do pote {bounty.pot}.");
        }

        /// <summary>
        /// O relogio so anda quando da para cacar: fora do proprio ward e com gente online
        /// suficiente. O aviso (delay) tambem so corre online e sem pausa.
        /// </summary>
        private static void UpdatePause(PvpBountyRecord bounty, Runtime rt, PvpPeer peer)
        {
            string reason = null;
            if (PvpConfig.BountyPausesInOwnWard.Value && bounty.delayLeft <= 0d && (FlagsOf(peer) & PvpFlags.InOwnWard) != 0)
                reason = "dentro do proprio ward";
            else if (OnlinePlayers < PvpConfig.BountyMinPlayers.Value)
                reason = $"menos de {PvpConfig.BountyMinPlayers.Value} jogadores online";

            bool paused = reason != null;
            if (paused == rt.Paused) return;
            rt.Paused = paused;
            PvpNet.Message(peer.PeerId, paused
                ? $"<color=#ff8c00>Bounty pausada:</color> {reason}. O tempo so conta fora dele."
                : "<color=#ff8c00>Bounty retomada.</color>");
            PvpServer.SendState(peer);
            Debug.Log($"[Deadheim PvP] Bounty de {bounty.targetName} {(paused ? "pausada (" + reason + ")" : "retomada")}.");
        }

        private static PvpFlags FlagsOf(PvpPeer peer)
        {
            ZDO zdo = peer.CharacterId.IsNone() || ZDOMan.instance == null ? null : ZDOMan.instance.GetZDO(peer.CharacterId);
            return zdo != null ? (PvpFlags)zdo.GetInt(PvpState.ZdoFlags, 0) : PvpFlags.None;
        }

        public static bool TryParseAmount(string text, out int amount)
            => int.TryParse((text ?? string.Empty).Trim().Replace(".", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out amount)
               && amount > 0;
    }
}
