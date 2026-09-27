using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Desafio: o jogador se oferece como alvo. Depois de ChallengeDelaySeconds ele vira
    /// CACADO por ChallengeDurationMinutes: aparece no mapa de todos, perde zona segura e
    /// imunidade. Sobreviveu, ganha a recompensa; foi morto, quem matou ganha. Estado so
    /// no servidor: o cliente recebe os segundos restantes e publica a bandeira.
    /// </summary>
    internal static class PvpChallenge
    {
        private sealed class Hunt
        {
            public long PlayerId;
            public string Name;
            public double PendingUntil;
            public double HuntedUntil;
            public bool Paused;
            public double LastTick;
            public bool Active => HuntedUntil > 0d;
        }

        private static readonly Dictionary<long, Hunt> _hunts = new Dictionary<long, Hunt>();

        private static double Now => PvpState.Now;

        public static bool AnyHunted
        {
            get
            {
                foreach (Hunt hunt in _hunts.Values)
                    if (hunt.Active) return true;
                return false;
            }
        }

        public static bool IsHunted(long playerId)
            => _hunts.TryGetValue(playerId, out Hunt hunt) && hunt.Active;

        public static bool IsPaused(long playerId)
            => _hunts.TryGetValue(playerId, out Hunt hunt) && hunt.Active && hunt.Paused;

        private static int OnlinePlayers => ZNet.instance != null ? ZNet.instance.GetNrOfPlayers() : 0;

        public static void Remaining(long playerId, out double pending, out double hunted)
        {
            pending = 0d;
            hunted = 0d;
            if (!_hunts.TryGetValue(playerId, out Hunt hunt)) return;
            if (hunt.Active) hunted = Mathf.Max(0f, (float)(hunt.HuntedUntil - Now));
            else pending = Mathf.Max(0f, (float)(hunt.PendingUntil - Now));
        }

        public static void Reset() => _hunts.Clear();

        public static void OnCommand(PvpPeer peer, string action)
        {
            action = (action ?? string.Empty).Trim().ToLowerInvariant();
            if (!PvpConfig.ChallengeEnabled.Value)
            {
                PvpNet.Message(peer.PeerId, "O desafio esta desligado neste servidor.");
                return;
            }

            if (action == "cancelar" || action == "cancel")
            {
                Cancel(peer);
                return;
            }

            if (action == "status" || action == "info")
            {
                Status(peer);
                return;
            }

            Start(peer);
        }

        private static void Start(PvpPeer peer)
        {
            if (_hunts.TryGetValue(peer.PlayerId, out Hunt existing))
            {
                PvpNet.Message(peer.PeerId, existing.Active ? "Voce ja esta sendo cacado." : "Seu desafio ja vai comecar.");
                return;
            }

            int minimum = PvpConfig.ChallengeMinPlayers.Value;
            if (OnlinePlayers < minimum)
            {
                PvpNet.Message(peer.PeerId, $"O desafio precisa de pelo menos {minimum} jogadores online (agora: {OnlinePlayers}).");
                return;
            }

            PvpPlayerRecord record = PvpStore.Player(peer.PlayerId, peer.Name);
            if (record.challengeReadyAt > Now)
            {
                PvpNet.Message(peer.PeerId, "Voce so pode aceitar outro desafio em " + PvpClient.FormatDuration(record.challengeReadyAt - Now) + ".");
                return;
            }

            float delay = Mathf.Max(0, PvpConfig.ChallengeDelaySeconds.Value);
            _hunts[peer.PlayerId] = new Hunt
            {
                PlayerId = peer.PlayerId,
                Name = peer.Name,
                PendingUntil = Now + delay,
            };
            PvpServer.SendState(peer);

            PvpConfig.Reward kill = PvpConfig.ParseReward(PvpConfig.ChallengeKillReward.Value);
            string bounty = kill.IsValid ? $" Quem matar ganha {kill.Amount} {kill.Prefab}." : string.Empty;
            PvpNet.Broadcast($"<color=#ff8c00>DESAFIO:</color> <color=#ffb347>{peer.Name}</color> aceitou o desafio! " +
                             $"Em {PvpClient.FormatDuration(delay)} aparece no mapa por {PvpConfig.ChallengeDurationMinutes.Value:0} min.{bounty}", true);
            Debug.Log($"[Deadheim PvP] Desafio aceito por {peer.Name} ({peer.PlayerId}).");
        }

        private static void Cancel(PvpPeer peer)
        {
            if (!_hunts.TryGetValue(peer.PlayerId, out Hunt hunt))
            {
                PvpNet.Message(peer.PeerId, "Voce nao tem desafio em andamento.");
                return;
            }
            if (hunt.Active)
            {
                PvpNet.Message(peer.PeerId, "Voce ja esta sendo cacado: nao da mais para desistir.");
                return;
            }

            Finish(hunt);
            PvpNet.Broadcast($"<color=#ff8c00>DESAFIO:</color> {hunt.Name} desistiu antes de comecar.");
        }

        private static void Status(PvpPeer peer)
        {
            List<string> lines = new List<string>();
            foreach (Hunt hunt in _hunts.Values)
                lines.Add(hunt.Active
                    ? $"{hunt.Name}: CACADO por mais {PvpClient.FormatDuration(hunt.HuntedUntil - Now)}"
                    : $"{hunt.Name}: comeca em {PvpClient.FormatDuration(hunt.PendingUntil - Now)}");
            PvpNet.Message(peer.PeerId, lines.Count == 0 ? "Nenhum desafio em andamento." : string.Join("\n", lines), false);
        }

        /// <summary>Encerra o desafio e aplica o cooldown, qualquer que seja o motivo.</summary>
        private static void Finish(Hunt hunt)
        {
            _hunts.Remove(hunt.PlayerId);
            PvpPlayerRecord record = PvpStore.Player(hunt.PlayerId, hunt.Name);
            record.challengeReadyAt = Now + PvpConfig.ChallengeCooldownMinutes.Value * 60d;
            PvpStore.MarkDirty();
            PvpServer.SendState(hunt.PlayerId);
        }

        public static void OnDeath(PvpPeer victim, long killerId, string killerName)
        {
            if (!_hunts.TryGetValue(victim.PlayerId, out Hunt hunt)) return;

            if (!hunt.Active)
            {
                Finish(hunt);
                PvpNet.Broadcast($"<color=#ff8c00>DESAFIO:</color> {hunt.Name} morreu antes do desafio comecar.");
                return;
            }

            Finish(hunt);
            if (killerId != 0L)
            {
                PvpNet.Broadcast($"<color=#ff8c00>DESAFIO:</color> <color=#ffb347>{killerName}</color> cacou <color=#ffb347>{hunt.Name}</color>!", true);
                if (PvpPeer.TryFindByPlayerId(killerId, out PvpPeer killer))
                    PvpServer.SendReward(killer.PeerId, PvpConfig.ParseReward(PvpConfig.ChallengeKillReward.Value), "Cacou " + hunt.Name);
            }
            else
            {
                PvpNet.Broadcast($"<color=#ff8c00>DESAFIO:</color> {hunt.Name} morreu durante o desafio.");
            }
        }

        /// <summary>
        /// O relogio do cacado so anda quando da para cacar: fora do proprio ward (paredes
        /// invulneraveis) e com gente online suficiente. Parado, o fim do desafio anda junto.
        /// </summary>
        private static void UpdatePause(Hunt hunt, PvpPeer peer)
        {
            double elapsed = Math.Max(0d, Now - hunt.LastTick);
            hunt.LastTick = Now;

            string reason = null;
            if (PvpConfig.ChallengePausesInOwnWard.Value && (FlagsOf(peer) & PvpFlags.InOwnWard) != 0)
                reason = "dentro do proprio ward";
            else if (OnlinePlayers < PvpConfig.ChallengeMinPlayers.Value)
                reason = $"menos de {PvpConfig.ChallengeMinPlayers.Value} jogadores online";

            bool paused = reason != null;
            if (paused) hunt.HuntedUntil += elapsed;
            bool changed = paused != hunt.Paused;
            if (changed)
            {
                hunt.Paused = paused;
                PvpNet.Message(peer.PeerId, paused
                    ? $"<color=#ff8c00>Desafio pausado:</color> {reason}. O tempo so conta fora dele."
                    : "<color=#ff8c00>Desafio retomado.</color>");
                Debug.Log($"[Deadheim PvP] Desafio de {hunt.Name} {(paused ? "pausado (" + reason + ")" : "retomado")}.");
            }
            // Parado, o cliente precisa do fim novo a cada segundo, senao acha que acabou; e na
            // troca, para o HUD sair do "pausado".
            if (paused || changed) PvpServer.SendState(peer);
        }

        private static PvpFlags FlagsOf(PvpPeer peer)
        {
            ZDO zdo = peer.CharacterId.IsNone() || ZDOMan.instance == null ? null : ZDOMan.instance.GetZDO(peer.CharacterId);
            return zdo != null ? (PvpFlags)zdo.GetInt(PvpState.ZdoFlags, 0) : PvpFlags.None;
        }

        public static void Tick()
        {
            if (_hunts.Count == 0) return;

            foreach (Hunt hunt in new List<Hunt>(_hunts.Values))
            {
                if (!PvpPeer.TryFindByPlayerId(hunt.PlayerId, out PvpPeer peer))
                {
                    Finish(hunt);
                    PvpNet.Broadcast($"<color=#ff8c00>DESAFIO:</color> {hunt.Name} fugiu do desafio (desconectou).");
                    continue;
                }

                if (hunt.Active) UpdatePause(hunt, peer);

                if (!hunt.Active && Now >= hunt.PendingUntil)
                {
                    hunt.LastTick = Now;
                    hunt.HuntedUntil = Now + PvpConfig.ChallengeDurationMinutes.Value * 60d;
                    PvpServer.SendState(peer);
                    PvpNet.Broadcast($"<color=#ff8c00>DESAFIO:</color> <color=#ffb347>{hunt.Name}</color> agora esta <color=#ff5050>CACADO</color>! " +
                                     $"Visivel no mapa por {PvpConfig.ChallengeDurationMinutes.Value:0} min.", true);
                    continue;
                }

                if (hunt.Active && Now >= hunt.HuntedUntil)
                {
                    Finish(hunt);
                    PvpNet.Broadcast($"<color=#ff8c00>DESAFIO:</color> <color=#ffb347>{hunt.Name}</color> sobreviveu ao desafio!", true);
                    PvpServer.SendReward(peer.PeerId, PvpConfig.ParseReward(PvpConfig.ChallengeSurviveReward.Value), "Sobreviveu ao desafio");
                }
            }
        }
    }
}
