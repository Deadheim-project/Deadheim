using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// A porta do PvP para os NPCs do NpcValheim: o Cacador de Recompensas (bounty) e o Juiz de
    /// Guerra (troca PvE/PvP e os atributos de cada lado). O NpcValheim liga por reflexao, sem
    /// referencia de compilacao, igual faz com a MontariasApi: por isso so tipos primitivos,
    /// arrays de string e Version. Mudou o formato de algum metodo, sobe Version, e o NpcValheim
    /// que nao conhece a versao nova trata o Deadheim como ausente em vez de chamar as cegas.
    ///
    /// Tudo aqui roda no cliente e so pede. Quem decide e o servidor do Deadheim (PvpBounty,
    /// PvpPve, AjustesRede), com as mesmas regras de antes; os pedidos que tiram moeda tiram do
    /// inventario aqui e o servidor diz quanto ficou, como o /bounty fazia.
    /// </summary>
    public static class PvpApi
    {
        public const int Version = 1;

        /// <summary>O modulo de PvP esta ligado e ha um mundo com o canal do PvP aberto.</summary>
        public static bool Active() => PvpConfig.Active && PvpNet.Ready && Player.m_localPlayer != null;

        public static string FormatDuration(double seconds) => PvpClient.FormatDuration(seconds);

        // ------------------------------------------------------------------ bounty

        public static bool BountyEnabled() => PvpConfig.Active && PvpConfig.BountyEnabled.Value;

        public static int BountyMinimum() => PvpConfig.BountyMinimum.Value;

        public static float BountyKillerSharePercent() => Mathf.Clamp(PvpConfig.BountyKillerSharePercent.Value, 0f, 100f);

        /// <summary>Teto de moedas que cada jogador poe em bounties por dia. 0 = sem teto.</summary>
        public static int BountyDailyCap() => Math.Max(0, PvpConfig.BountyDailyCapPerPlayer.Value);

        /// <summary>Segundos entre a bounty ser colocada e o alvo virar cacado.</summary>
        public static int BountyDelaySeconds() => Math.Max(0, PvpConfig.BountyDelaySeconds.Value);

        /// <summary>Moedas na cabeca do jogador local (0 = sem bounty).</summary>
        public static int LocalBountyPot() => PvpClient.BountyPot;

        /// <summary>Quanto o jogador local paga para encerrar a propria bounty. 0 = nao ha ou esta desligado.</summary>
        public static int LocalBuyoutCost() => PvpClient.BountyPot > 0 ? PvpBounty.BuyoutCost(PvpClient.BountyPot) : 0;

        public static bool LocalHunted() => PvpState.IsHunted;

        public static bool LocalHuntPending() => PvpState.IsHuntPending;

        /// <summary>Segundos em que ninguem pode por bounty no jogador local (acabou de sair de uma).</summary>
        public static double LocalBountyCooldownSeconds() => PvpClient.BountyCooldownRemaining;

        /// <summary>Pede o quadro de bounties; chega em BountyBoard e sobe BountyBoardRevision.</summary>
        public static bool RequestBountyBoard()
        {
            if (!Active()) return false;
            return PvpClient.SendBounty("board", null, 0, out _);
        }

        public static int BountyBoardRevision() => PvpClient.BountyBoardRevision;

        /// <summary>
        /// O ultimo quadro: uma linha por bounty, a maior primeiro. Cada linha:
        /// { alvo, moedas para quem matar, estado em texto, "1" se o alvo esta online, "1" se ja e cacado }.
        /// </summary>
        public static string[][] BountyBoard()
        {
            List<PvpClient.BountyBoardRow> rows = PvpClient.BountyBoard;
            string[][] result = new string[rows.Count][];
            for (int i = 0; i < rows.Count; i++)
                result[i] = new[]
                {
                    rows[i].Target ?? string.Empty,
                    rows[i].Payout.ToString(),
                    rows[i].State ?? string.Empty,
                    rows[i].Online ? "1" : "0",
                    rows[i].Hunted ? "1" : "0",
                };
            return result;
        }

        /// <summary>
        /// Poe <paramref name="amount"/> moedas na cabeca de <paramref name="target"/>. As moedas saem
        /// do inventario agora; a resposta do servidor (LastBountyMessage) diz se ficou e devolve o
        /// resto. Devolve o motivo de nao ter pedido, ou null se pediu.
        /// </summary>
        public static string PlaceBounty(string target, int amount)
        {
            if (!Active()) return "Entre no mundo primeiro.";
            if (!PvpConfig.BountyEnabled.Value) return "A bounty esta desligada neste servidor.";
            target = (target ?? string.Empty).Trim();
            if (target.Length == 0) return "Escreva o nome do alvo.";
            if (amount <= 0) return "Valor invalido.";
            if (amount < PvpConfig.BountyMinimum.Value) return $"A bounty minima e {PvpConfig.BountyMinimum.Value} moedas.";
            Player local = Player.m_localPlayer;
            if (local != null && string.Equals(local.GetPlayerName(), target, StringComparison.OrdinalIgnoreCase))
                return "Voce nao pode colocar bounty em si mesmo.";
            return PvpClient.SendBounty("place", target, amount, out string refusal) ? null : refusal;
        }

        /// <summary>Paga para encerrar a propria bounty. Devolve o motivo de nao ter pedido, ou null.</summary>
        public static string PayOwnBounty()
        {
            if (!Active()) return "Entre no mundo primeiro.";
            if (PvpClient.BountyPot <= 0) return "Nao ha bounty na sua cabeca.";
            int cost = PvpBounty.BuyoutCost(PvpClient.BountyPot);
            if (cost <= 0) return "Pagar a propria bounty esta desligado neste servidor.";
            return PvpClient.SendBounty("pay", null, cost, out string refusal) ? null : refusal;
        }

        public static string LastBountyMessage() => PvpClient.LastBountyMessage;

        public static int BountyMessageRevision() => PvpClient.BountyMessageRevision;

        // -------------------------------------------------------------- PvE / PvP

        /// <summary>A troca para o PvE esta liberada (PveEnabled).</summary>
        public static bool PveEnabled() => PvpConfig.Active && PvpConfig.PveEnabled.Value;

        public static bool LocalIsPve() => PvpPve.IsLocal;

        public static string PveTitle() => PvpPve.Title;

        public static float PveSwitchCooldownHours() => Math.Max(0f, PvpConfig.PveSwitchCooldownHours.Value);

        public static float PveSkillMultiplier() => Math.Max(0f, PvpConfig.PveSkillMultiplier.Value);

        public static float PvpSkillMultiplier() => Math.Max(0f, PvpConfig.PvpSkillMultiplier.Value);

        /// <summary>Taxa de coleta do PvE (0 = a do mundo).</summary>
        public static float PveResourceRate() => Math.Max(0f, PvpConfig.PveResourceRate.Value);

        /// <summary>Taxa de coleta do PvP (0 = a do mundo).</summary>
        public static float PvpResourceRate() => Math.Max(0f, PvpConfig.PvpResourceRate.Value);

        /// <summary>Segundos ate o jogador local poder trocar PvE/PvP de novo. 0 = pode agora.</summary>
        public static double LocalPveSwitchSeconds() => PvpClient.PveSwitchReadyIn;

        /// <summary>Por que a troca seria recusada agora, ou null se da para pedir.</summary>
        public static string PveSwitchRefusal(bool toPve) => PvpPve.LocalSwitchRefusal(toPve);

        /// <summary>
        /// Pede a troca: <paramref name="toPve"/> true vira PvE, false volta ao PvP. A resposta chega em
        /// LastPveMessage. Devolve o motivo de nao ter pedido, ou null.
        /// </summary>
        public static string RequestPveSwitch(bool toPve)
        {
            string refusal = PvpPve.LocalSwitchRefusal(toPve);
            if (refusal != null) return refusal;
            if (!PvpNet.Ready) return "Entre no mundo primeiro.";
            PvpClient.SendPve("set", toPve ? "pve" : "pvp");
            return null;
        }

        public static string LastPveMessage() => PvpClient.LastPveMessage;

        public static bool LastPveOk() => PvpClient.LastPveOk;

        public static int PveMessageRevision() => PvpClient.PveMessageRevision;

        // ------------------------------------------------------------------ admin

        /// <summary>Admin pela adminlist (o servidor confere de novo em cada pedido).</summary>
        public static bool LocalIsAdmin() => Vanilla.Admin.LocalPlayerIsAdmin();

        /// <summary>Os atributos do PvE e do PvP que o admin ajusta no menu do Juiz de Guerra, nesta ordem.</summary>
        private static IEnumerable<(ConfigEntryBase Entry, string Label)> PveSettingEntries()
        {
            yield return (PvpConfig.PveEnabled, "Troca para o PvE liberada");
            yield return (PvpConfig.PveSwitchCooldownHours, "Espera entre trocas (horas)");
            yield return (PvpConfig.PveTitle, "Título de quem é PvE");
            yield return (PvpConfig.PveSkillMultiplier, "Ganho de skill no PvE (x)");
            yield return (PvpConfig.PvpSkillMultiplier, "Ganho de skill no PvP (x)");
            yield return (PvpConfig.PveResourceRate, "Coleta no PvE (x, 0 = a do mundo)");
            yield return (PvpConfig.PvpResourceRate, "Coleta no PvP (x, 0 = a do mundo)");
        }

        /// <summary>
        /// Os atributos editaveis, com o valor que vale agora (o do servidor, pelo ServerSync).
        /// Cada linha: { chave, rotulo, valor no formato do .cfg, tipo ("bool", "number" ou "text"), descricao }.
        /// </summary>
        public static string[][] PveSettings()
        {
            List<string[]> rows = new List<string[]>();
            foreach ((ConfigEntryBase entry, string label) in PveSettingEntries())
            {
                if (entry == null) continue;
                Type type = entry.SettingType;
                string kind = type == typeof(bool) ? "bool" : type == typeof(string) ? "text" : "number";
                rows.Add(new[]
                {
                    entry.Definition.Key,
                    label,
                    TomlTypeConverter.ConvertToString(entry.BoxedValue, type),
                    kind,
                    entry.Description?.Description ?? string.Empty,
                });
            }
            return rows.ToArray();
        }

        /// <summary>
        /// Pede ao servidor para trocar um atributo (chave de PveSettings; valor no formato do .cfg,
        /// "true"/"false" ou numero com ponto). O servidor confere a adminlist pela conexao, grava
        /// o cfg dele e o ServerSync manda o valor novo a todos. Resposta em LastSettingMessage.
        /// Devolve o motivo de nao ter pedido, ou null.
        /// </summary>
        public static string SetPveSetting(string key, string value)
        {
            if (!PvpNet.Ready) return "Entre no mundo primeiro.";
            if (!LocalIsAdmin()) return "So admin.";
            foreach ((ConfigEntryBase entry, string _) in PveSettingEntries())
            {
                if (entry == null || !string.Equals(entry.Definition.Key, key, StringComparison.Ordinal)) continue;
                try
                {
                    TomlTypeConverter.ConvertToValue((value ?? string.Empty).Trim(), entry.SettingType);
                }
                catch (Exception)
                {
                    return "Valor invalido para " + key + ".";
                }
                return Ajustes.AjustesRede.Definir(Plugin.PluginGUID, entry.Definition.Section, key, (value ?? string.Empty).Trim())
                    ? null
                    : "Entre no mundo primeiro.";
            }
            return "Atributo desconhecido: " + key + ".";
        }

        public static string LastSettingMessage() => _lastSettingMessage;

        public static bool LastSettingOk() => _lastSettingOk;

        public static int SettingMessageRevision() => _settingRevision;

        private static string _lastSettingMessage = string.Empty;
        private static bool _lastSettingOk;
        private static int _settingRevision;

        /// <summary>AjustesRede: resposta do servidor a um Definir (de qualquer tela).</summary>
        internal static void OnSettingResult(bool ok, string key, string value, string message)
        {
            _lastSettingOk = ok;
            _lastSettingMessage = ok ? $"{key} = {value} (ja valendo para todos)." : "O servidor recusou: " + message;
            _settingRevision++;
        }
    }
}
