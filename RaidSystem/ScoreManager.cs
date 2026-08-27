using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RaidSystem
{
    public static class ScoreManager
    {
        public static void RecordConquest(string pid, string nick, string team) => DataStore.Modify(d => GetOrCreate(d, pid, nick, team).Conquests++);

        /// <summary>
        /// Abate, morte e defesa de um mesmo PvP num Modify so. Separados eram tres
        /// serializacoes do JSON inteiro e tres gravacoes em disco por kill.
        /// </summary>
        public static void RecordPvpOutcome(
            string killerPid, string killerNick, string killerTeam,
            string deadPid, string deadNick, string deadTeam,
            bool defended)
        {
            DataStore.Modify(d =>
            {
                PlayerScore killer = GetOrCreate(d, killerPid, killerNick, killerTeam);
                killer.Kills++;
                if (defended) killer.Defenses++;

                GetOrCreate(d, deadPid, deadNick, deadTeam).Deaths++;
            });
        }

        public static List<PlayerScore> GetTopPlayers(int count = 10) =>
            DataStore.Load().Scores.OrderByDescending(s => s.TotalPoints).Take(count).ToList();

        public static List<(string TeamId, int TotalPoints, int Members)> GetTeamRanking() =>
            DataStore.Load().Scores.Where(s => !string.IsNullOrEmpty(s.TeamId))
                .GroupBy(s => s.TeamId, StringComparer.OrdinalIgnoreCase)
                .Select(g => (g.Key, g.Sum(s => s.TotalPoints), g.Count()))
                .OrderByDescending(r => r.Item2).ToList();

        public static string SerializeScores()
        {
            var sb = new StringBuilder();
            foreach (var s in DataStore.Load().Scores)
                sb.Append(Clean(s.PlayerId)).Append('\t').Append(Clean(s.Nick)).Append('\t').Append(Clean(s.TeamId)).Append('\t')
                  .Append(s.Kills).Append('\t').Append(s.Deaths).Append('\t').Append(s.Conquests).Append('\t').Append(s.Defenses).Append('\n');
            return sb.ToString();
        }

        public static List<PlayerScore> DeserializeScores(string raw)
        {
            var list = new List<PlayerScore>();
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (string line in raw.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] p = line.Split('\t');
                if (p.Length < 7) continue;
                list.Add(new PlayerScore { PlayerId = p[0], Nick = p[1], TeamId = p[2],
                    Kills = int.TryParse(p[3], out int k) ? k : 0, Deaths = int.TryParse(p[4], out int d) ? d : 0,
                    Conquests = int.TryParse(p[5], out int c) ? c : 0, Defenses = int.TryParse(p[6], out int df) ? df : 0 });
            }
            return list;
        }

        public static string FormatLeaderboardForWebhook()
        {
            var top = GetTopPlayers(10);
            if (top.Count == 0) return "No scores yet.";
            var sb = new StringBuilder();
            sb.AppendLine("**Leaderboard**\n```");
            int r = 1;
            foreach (var s in top)
            { sb.AppendLine($"#{r,-3} {s.Nick,-16} [{s.TeamId}] K:{s.Kills} D:{s.Deaths} C:{s.Conquests} Def:{s.Defenses} = {s.TotalPoints}pts"); r++; }
            sb.AppendLine("```\n**Guilds:**");
            foreach (var (t, pts, m) in GetTeamRanking()) sb.AppendLine($"> {t} ({m} members): **{pts}** pts");
            return sb.ToString();
        }

        /// <summary>Tab e newline sao os separadores do formato: nao podem vir dentro de um campo.</summary>
        private static string Clean(string value)
            => string.IsNullOrEmpty(value) ? string.Empty : value.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        private static PlayerScore GetOrCreate(RaidData data, string pid, string nick, string team)
        {
            var e = data.Scores.FirstOrDefault(s => s.PlayerId == pid);
            if (e != null) { e.Nick = nick; e.TeamId = team; return e; }
            var n = new PlayerScore { PlayerId = pid, Nick = nick, TeamId = team };
            data.Scores.Add(n); return n;
        }
    }
}
