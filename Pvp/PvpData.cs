using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Deadheim.Pvp
{
    // Os registros que o servidor guarda e o formato do arquivo. Nada aqui usa o Unity de
    // proposito: o formato e testado fora do jogo (Testing/PvpSemJogo).

    internal sealed class PvpPlayerRecord
    {
        public long id;
        public string name;
        public int kills;
        public int deaths;
        /// <summary>Segundos de PK que faltam. Com PkTimeOnlineOnly so descontam com o jogador online.</summary>
        public double pkLeft;
        /// <summary>PK permanente: so sai quando e morto por jogador.</summary>
        public bool pkPermanent;
        /// <summary>Abates que deram PK desde que a marca atual comecou: escolhe o nivel (PkTiers).</summary>
        public int pkStreak;
        /// <summary>Perda do nivel atual de PK (PvpConfig.PkPenalty).</summary>
        public int pkPenalty;
        /// <summary>Segundos UTC a partir de quando pode receber outra bounty.</summary>
        public double bountyReadyAt;
        /// <summary>Contador de PK: abates que deram PK (matar sem ser em defesa, arena ou castelo).</summary>
        public int pkKills;
        /// <summary>Deslogou em combate com CombatLogout=Death: morre ao voltar.</summary>
        public bool combatLogPending;
        /// <summary>Moedas a entregar quando ele voltar (bounty paga ou devolvida com ele offline).</summary>
        public int pendingCoins;
        /// <summary>Escolheu o PvE permanente (/pve confirmar). So admin desfaz.</summary>
        public bool pvePermanent;
        /// <summary>Segundos UTC de quando virou PvE.</summary>
        public double pveSince;
        /// <summary>Dia (UTC do servidor, dias desde 1970) do contador bountyPaidToday.</summary>
        public int bountyPaidDay;
        /// <summary>Moedas que este jogador ja pos em bounties no dia bountyPaidDay (BountyDailyCapPerPlayer).</summary>
        public int bountyPaidToday;

        public float Ratio => deaths <= 0 ? kills : (float)kills / deaths;

        /// <summary>
        /// Conta <paramref name="amount"/> moedas de bounty pagas hoje, se couber no teto diario
        /// (<paramref name="cap"/> 0 = sem teto). Dia novo zera o contador. Recusado, nada muda e
        /// <paramref name="left"/> diz quanto ainda cabe hoje.
        /// </summary>
        public bool TryAddBountyPaid(int day, int amount, int cap, out int left)
        {
            if (bountyPaidDay != day)
            {
                bountyPaidDay = day;
                bountyPaidToday = 0;
            }
            left = cap > 0 ? Math.Max(0, cap - bountyPaidToday) : int.MaxValue;
            if (amount <= 0) return false;
            if (cap > 0 && amount > left) return false;
            bountyPaidToday += amount;
            if (cap > 0) left = Math.Max(0, cap - bountyPaidToday);
            return true;
        }

        public bool IsPk => pkPermanent || pkLeft > 0d;

        /// <summary>Desconta tempo de PK. Devolve true se a marca (nao permanente) acabou agora.</summary>
        public bool TickPk(double seconds)
        {
            if (pkPermanent || pkLeft <= 0d || seconds <= 0d) return false;
            pkLeft -= seconds;
            if (pkLeft > 0d) return false;
            ClearPk();
            return true;
        }

        public void ClearPk()
        {
            pkLeft = 0d;
            pkPermanent = false;
            pkStreak = 0;
            pkPenalty = 0;
        }
    }

    internal sealed class PvpBountyContribution
    {
        public long id;
        public string name;
        public int amount;
    }

    /// <summary>Uma cabeca a premio. Os relogios sao segundos de alvo ONLINE, nao de parede.</summary>
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

    /// <summary>Contas de moeda que o teste sem o jogo confere.</summary>
    internal static class PvpCoinMath
    {
        /// <summary>
        /// Quanto o cliente devolve a si mesmo de um pedido de bounty: tirou <paramref name="taken"/>
        /// do inventario antes de pedir e o servidor ficou com <paramref name="kept"/>. O servidor
        /// nunca manda moeda numa recusa; so diz quanto ficou.
        /// </summary>
        public static int BountyRefund(int taken, int kept)
            => Math.Max(0, taken - Math.Min(Math.Max(0, kept), Math.Max(0, taken)));
    }

    internal sealed class PvpStoreData
    {
        public int version = PvpStoreFormat.Version;
        public List<PvpPlayerRecord> players = new List<PvpPlayerRecord>();
        public List<PvpBountyRecord> bounties = new List<PvpBountyRecord>();
    }

    /// <summary>
    /// Texto, uma linha por registro e campos "chave=valor" separados por TAB. Campo que nao
    /// existe vira o padrao e campo desconhecido e ignorado, entao acrescentar campo nao quebra
    /// arquivo antigo. Substitui o JsonUtility, que gravava so o "version" e perdia as listas:
    /// todo restart zerava K/D, PK, bounties e o PvE permanente.
    /// </summary>
    internal static class PvpStoreFormat
    {
        // 4: teto diario de bounty (bountyPaidDay/bountyPaidToday) e o que veio com o 7.3.1.
        public const int Version = 4;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(PvpStoreData data)
        {
            StringBuilder text = new StringBuilder();
            text.Append("# Deadheim PvP: estado deste mundo (K/D, PK, bounties, PvE permanente).\n");
            text.Append("# Uma linha por registro, campos chave=valor separados por TAB. Edite so com o servidor desligado.\n");
            text.Append("version\t").Append(Version.ToString(Inv)).Append('\n');

            foreach (PvpPlayerRecord p in data.players)
                Line(text, "player",
                    "id", L(p.id), "name", S(p.name), "kills", I(p.kills), "deaths", I(p.deaths),
                    "pkLeft", D(p.pkLeft), "pkPermanent", B(p.pkPermanent), "pkStreak", I(p.pkStreak),
                    "pkPenalty", I(p.pkPenalty), "pkKills", I(p.pkKills), "bountyReadyAt", D(p.bountyReadyAt),
                    "combatLogPending", B(p.combatLogPending), "pendingCoins", I(p.pendingCoins),
                    "pvePermanent", B(p.pvePermanent), "pveSince", D(p.pveSince),
                    "bountyPaidDay", I(p.bountyPaidDay), "bountyPaidToday", I(p.bountyPaidToday));

            foreach (PvpBountyRecord b in data.bounties)
            {
                Line(text, "bounty",
                    "target", L(b.targetId), "name", S(b.targetName), "pot", I(b.pot),
                    "delayLeft", D(b.delayLeft), "elapsed", D(b.elapsed));
                foreach (PvpBountyContribution c in b.contributions)
                    Line(text, "contrib", "target", L(b.targetId), "id", L(c.id), "name", S(c.name), "amount", I(c.amount));
            }
            return text.ToString();
        }

        /// <summary>Le o arquivo. Linha que nao da para entender e pulada e contada em <paramref name="skipped"/>.</summary>
        public static PvpStoreData Read(string text, out int skipped)
        {
            PvpStoreData data = new PvpStoreData();
            skipped = 0;
            Dictionary<long, PvpBountyRecord> bounties = new Dictionary<long, PvpBountyRecord>();
            HashSet<long> players = new HashSet<long>();

            foreach (string raw in (text ?? string.Empty).Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;

                string[] parts = line.Split('\t');
                Dictionary<string, string> f = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < parts.Length; i++)
                {
                    int eq = parts[i].IndexOf('=');
                    if (eq > 0) f[parts[i].Substring(0, eq).Trim()] = parts[i].Substring(eq + 1);
                }

                switch (parts[0].Trim().ToLowerInvariant())
                {
                    case "version":
                        if (parts.Length > 1 && int.TryParse(parts[1].Trim(), NumberStyles.Integer, Inv, out int version)) data.version = version;
                        break;

                    case "player":
                    {
                        long id = GetLong(f, "id");
                        if (id == 0L || !players.Add(id)) { skipped++; break; }
                        data.players.Add(new PvpPlayerRecord
                        {
                            id = id,
                            name = GetString(f, "name") ?? id.ToString(Inv),
                            kills = GetInt(f, "kills"),
                            deaths = GetInt(f, "deaths"),
                            pkLeft = GetDouble(f, "pkLeft"),
                            pkPermanent = GetBool(f, "pkPermanent"),
                            pkStreak = GetInt(f, "pkStreak"),
                            pkPenalty = GetInt(f, "pkPenalty"),
                            pkKills = GetInt(f, "pkKills"),
                            bountyReadyAt = GetDouble(f, "bountyReadyAt"),
                            combatLogPending = GetBool(f, "combatLogPending"),
                            pendingCoins = GetInt(f, "pendingCoins"),
                            pvePermanent = GetBool(f, "pvePermanent"),
                            pveSince = GetDouble(f, "pveSince"),
                            bountyPaidDay = GetInt(f, "bountyPaidDay"),
                            bountyPaidToday = GetInt(f, "bountyPaidToday"),
                        });
                        break;
                    }

                    case "bounty":
                    {
                        long target = GetLong(f, "target");
                        if (target == 0L || bounties.ContainsKey(target)) { skipped++; break; }
                        PvpBountyRecord bounty = new PvpBountyRecord
                        {
                            targetId = target,
                            targetName = GetString(f, "name") ?? target.ToString(Inv),
                            pot = GetInt(f, "pot"),
                            delayLeft = GetDouble(f, "delayLeft"),
                            elapsed = GetDouble(f, "elapsed"),
                        };
                        bounties[target] = bounty;
                        data.bounties.Add(bounty);
                        break;
                    }

                    case "contrib":
                    {
                        // id 0 e a casa (bounty de admin): valido.
                        if (!bounties.TryGetValue(GetLong(f, "target"), out PvpBountyRecord bounty)) { skipped++; break; }
                        bounty.contributions.Add(new PvpBountyContribution
                        {
                            id = GetLong(f, "id"),
                            name = GetString(f, "name") ?? string.Empty,
                            amount = GetInt(f, "amount"),
                        });
                        break;
                    }

                    default:
                        skipped++;
                        break;
                }
            }
            return data;
        }

        // ------------------------------------------------------------------ escrita

        private static void Line(StringBuilder text, string kind, params string[] fields)
        {
            text.Append(kind);
            for (int i = 0; i + 1 < fields.Length; i += 2)
                text.Append('\t').Append(fields[i]).Append('=').Append(fields[i + 1]);
            text.Append('\n');
        }

        private static string L(long value) => value.ToString(Inv);
        private static string I(int value) => value.ToString(Inv);
        private static string D(double value) => value.ToString("R", Inv);
        private static string B(bool value) => value ? "1" : "0";

        /// <summary>TAB e quebra de linha sao os separadores: nao podem ir dentro de um nome.</summary>
        private static string S(string value)
            => string.IsNullOrEmpty(value) ? string.Empty : value.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        // ------------------------------------------------------------------ leitura

        private static string GetString(Dictionary<string, string> f, string key)
            => f.TryGetValue(key, out string value) ? value : null;

        private static long GetLong(Dictionary<string, string> f, string key)
            => f.TryGetValue(key, out string value) && long.TryParse(value.Trim(), NumberStyles.Integer, Inv, out long n) ? n : 0L;

        private static int GetInt(Dictionary<string, string> f, string key)
            => f.TryGetValue(key, out string value) && int.TryParse(value.Trim(), NumberStyles.Integer, Inv, out int n) ? n : 0;

        private static double GetDouble(Dictionary<string, string> f, string key)
            => f.TryGetValue(key, out string value) && double.TryParse(value.Trim(), NumberStyles.Float, Inv, out double n) ? n : 0d;

        private static bool GetBool(Dictionary<string, string> f, string key)
            => f.TryGetValue(key, out string value) && (value.Trim() == "1" || value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
    }
}
