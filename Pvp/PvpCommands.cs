using Deadheim.Vanilla;
using HarmonyLib;
using System;
using System.Text;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>Comandos de chat do PvP: /pvp, /bounty, /rank, /pve e /pvpadmin.</summary>
    [HarmonyPatch]
    internal static class PvpCommands
    {
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class RegisterPatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("pvp", "estado do PvP e ajuda", args => Status(args.Context));

                new Terminal.ConsoleCommand("bounty", "<jogador> <moedas> | lista | pagar - cabeca a premio",
                    args => RequireOnline(args.Context, () => Bounty(args)));

                new Terminal.ConsoleCommand("rank", "ranking PvP (abates/mortes)",
                    args => RequireOnline(args.Context, PvpClient.SendRankRequest));

                new Terminal.ConsoleCommand("pve", "[confirmar] - sair do PvP para sempre",
                    args => RequireOnline(args.Context, () => Pve(args)));

                new Terminal.ConsoleCommand("pvpadmin", "(admin) zona | imune <min> | limpar | pk <min> | bounty <jogador> <moedas> | pve <jogador>", AdminCommand);
            }
        }

        [HarmonyPatch(typeof(Chat), nameof(Chat.Awake))]
        private static class ChatHintPatch
        {
            private static void Postfix(Chat __instance)
            {
                int index = Math.Max(0, __instance.m_chatBuffer.Count - 5);
                __instance.m_chatBuffer.Insert(index, "/pvp estado | /bounty | /rank");
                __instance.UpdateChat();
            }
        }

        private static void RequireOnline(Terminal context, Action action)
        {
            if (!PvpConfig.Active)
            {
                context?.AddString("O modulo de PvP esta desligado neste servidor.");
                return;
            }
            if (ZNet.instance == null || Player.m_localPlayer == null)
            {
                context?.AddString("Entre no mundo primeiro.");
                return;
            }
            action();
        }

        /// <summary>
        /// /bounty &lt;jogador&gt; &lt;moedas&gt; coloca (as moedas saem do inventario); /bounty pagar
        /// compra a propria cabeca; /bounty ou /bounty lista mostra as bounties.
        /// </summary>
        private static void Bounty(Terminal.ConsoleEventArgs args)
        {
            Terminal context = args.Context;
            if (!PvpConfig.BountyEnabled.Value)
            {
                context?.AddString("A bounty esta desligada neste servidor.");
                return;
            }

            string first = args.Length > 1 ? args[1].ToLowerInvariant() : "lista";
            string refusal;
            if (args.Length <= 2 && (first == "lista" || first == "list" || first == "status"))
            {
                PvpClient.SendBounty("list", null, 0, out _);
                context?.AddString($"/bounty <jogador> <moedas> (minimo {PvpConfig.BountyMinimum.Value}); quem matar leva " +
                                   $"{PvpConfig.BountyKillerSharePercent.Value:0}%. /bounty pagar para comprar a sua cabeca.");
                return;
            }

            if (args.Length <= 2 && (first == "pagar" || first == "pay"))
            {
                int cost = PvpBounty.BuyoutCost(PvpClient.BountyPot);
                if (PvpClient.BountyPot <= 0) { context?.AddString("Nao ha bounty na sua cabeca."); return; }
                if (cost <= 0) { context?.AddString("Pagar a propria bounty esta desligado neste servidor."); return; }
                if (!PvpClient.SendBounty("pay", null, cost, out refusal)) context?.AddString(refusal);
                else context?.AddString($"Pagando {cost} moedas para encerrar a sua bounty...");
                return;
            }

            if (args.Length < 3 || !PvpBounty.TryParseAmount(args[args.Length - 1], out int amount))
            {
                context?.AddString("Uso: /bounty <jogador> <moedas> | /bounty lista | /bounty pagar");
                return;
            }
            string target = string.Join(" ", args.Args, 1, args.Length - 2);
            if (amount < PvpConfig.BountyMinimum.Value)
            {
                context?.AddString($"A bounty minima e {PvpConfig.BountyMinimum.Value} moedas.");
                return;
            }
            if (!PvpClient.SendBounty("place", target, amount, out refusal)) context?.AddString(refusal);
            else context?.AddString($"Colocando {amount} moedas na cabeca de {target}...");
        }

        /// <summary>
        /// /pve explica e pede confirmacao; /pve confirmar pede ao servidor. Sem volta: o
        /// servidor guarda e so um admin desfaz.
        /// </summary>
        private static void Pve(Terminal.ConsoleEventArgs args)
        {
            Terminal context = args.Context;
            if (!PvpConfig.PveEnabled.Value) { context?.AddString("O PvE permanente esta desligado neste servidor."); return; }
            if (PvpPve.IsLocal) { context?.AddString($"Voce ja e PvE permanente ({PvpPve.Title})."); return; }

            bool confirm = args.Length > 1 && (args[1].ToLowerInvariant() == "confirmar" || args[1].ToLowerInvariant() == "confirm");
            if (!confirm)
            {
                string bonus = PvpConfig.PveSkillBonus.Value;
                context?.AddString($"PvE permanente: voce vira {PvpPve.Title} e nunca mais luta com jogadores (nem na arena ou no castelo).");
                context?.AddString($"Ganha uma vez: {(string.IsNullOrWhiteSpace(bonus) ? "nada" : bonus.Replace(",", ", "))}.");
                context?.AddString($"Skill sobe x{PvpConfig.PveSkillMultiplier.Value:0.##}" +
                                   (PvpConfig.PveResourceRate.Value > 0f ? $" e a coleta fica sem o bonus do mundo (x{PvpConfig.PveResourceRate.Value:0.##})." : "."));
                context?.AddString("<color=#ff5050>NAO TEM VOLTA.</color> Para confirmar: /pve confirmar");
                return;
            }

            if (PvpState.IsPk) { context?.AddString("PK nao pode virar PvE: espere a marca sair."); return; }
            if (PvpState.IsHunted || PvpState.IsHuntPending || PvpClient.BountyPot > 0) { context?.AddString("Com bounty na cabeca nao da para virar PvE."); return; }
            if (PvpState.InCombat) { context?.AddString("Em combate nao da para virar PvE."); return; }
            PvpClient.SendPve("join", null);
            context?.AddString("Pedindo o PvE permanente ao servidor...");
        }

        private static void Status(Terminal context)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                context?.AddString("Entre no mundo primeiro.");
                return;
            }

            StringBuilder text = new StringBuilder();
            text.AppendLine("Estado: " + PvpHud.Compose(player));
            text.AppendLine($"Dano PvP x{PvpConfig.DamageMultiplier.Value:0.##}; no seu territorio x{PvpConfig.DamageMultiplier.Value * PvpConfig.WardDefenseMultiplier.Value:0.##}.");
            if (PvpClient.BountyPot > 0)
                text.AppendLine($"Bounty na sua cabeca: {PvpClient.BountyPot} moedas" +
                                (PvpBounty.BuyoutCost(PvpClient.BountyPot) > 0 ? $" (pagar: {PvpBounty.BuyoutCost(PvpClient.BountyPot)} com /bounty pagar)." : "."));
            else if (PvpClient.BountyCooldownRemaining > 0d)
                text.AppendLine("Ninguem pode colocar bounty em voce por " + PvpClient.FormatDuration(PvpClient.BountyCooldownRemaining) + ".");
            string guild = PvpGuilds.GuildOf(player);
            text.AppendLine(guild != null
                ? $"Guilda: {guild} (sem fogo amigo entre membros)"
                : "Sem guilda: entre numa pelo mod Guilds para ter aliados sem fogo amigo.");
            if (PvpGroups.IsAvailable)
                text.AppendLine("Grupo (Groups): quem esta no seu grupo tambem e aliado, sem fogo amigo.");
            text.AppendLine(PvpState.PkCount > 0
                ? $"Contador de PK: {PvpState.PkCount} abate(s) que deram PK."
                : "Contador de PK: 0.");
            if (PvpState.IsPk)
                text.AppendLine((PvpState.IsPkPermanent ? "Voce e PK PERMANENTE (so sai morto por jogador)" : "Voce e PK")
                                + $": se morrer, perde {PvpConfig.PkSkillLossMultiplier.Value:0.#}x skill{PvpServer.PenaltyText(PvpState.PkPenalty)}.");
            if (PvpPve.IsLocal)
                text.AppendLine($"Voce e PvE permanente ({PvpPve.Title}): nao luta com jogadores; skill x{PvpConfig.PveSkillMultiplier.Value:0.##}.");
            else if (PvpConfig.PveEnabled.Value)
                text.AppendLine("/pve - sair do PvP para sempre (sem volta).");
            if (PvpState.IsAggressor)
                text.AppendLine("Voce e AGRESSOR: bateu primeiro. Quem te matar agora nao vira PK (voce nao perde nada a mais).");
            string castle = PvpBridge.Castle(player.transform.position);
            if (castle != null)
                text.AppendLine($"Castelo {castle}, dono: {PvpBridge.Owner(player.transform.position) ?? "ninguem"}.");
            text.AppendLine("/bounty <jogador> <moedas> - cabeca a premio; /bounty lista");
            text.Append("/rank - ranking K/D");
            foreach (string line in text.ToString().Split('\n')) context?.AddString(line.TrimEnd('\r'));
        }

        private static void AdminCommand(Terminal.ConsoleEventArgs args)
        {
            Terminal context = args.Context;
            if (!Vanilla.Admin.LocalPlayerIsAdmin())
            {
                context.AddString("So admin.");
                return;
            }

            Player player = Player.m_localPlayer;
            string action = args.Length > 1 ? args[1].ToLowerInvariant() : "zona";
            float minutes = args.Length > 2 && float.TryParse(args[2], out float value) ? value : 1f;

            switch (action)
            {
                case "zona":
                {
                    context.AddString("Ilha inicial: " + PvpZones.DescribeIsland());
                    if (player == null) break;
                    Vector3 pos = player.transform.position;
                    context.AddString($"Voce: x={pos.x:F0} z={pos.z:F0} bioma={WorldGenerator.instance?.GetBiome(pos)} " +
                                      $"segura={PvpZones.SafeAreaName(pos) ?? "-"} arena={PvpZones.ArenaName(pos) ?? "-"} " +
                                      $"castelo={PvpBridge.Castle(pos) ?? "-"}/{PvpBridge.Owner(pos) ?? "-"} guilda={PvpGuilds.GuildOf(player) ?? "-"} " +
                                      $"transporte={PvpZones.IsOnTransport(player)} territorio={PvpRules.InOwnTerritory(player.GetPlayerID(), pos)}");
                    context.AddString($"Bandeiras: {PvpState.Current} pvp={player.IsPVPEnabled()}");
                    break;
                }
                case "imune":
                    PvpState.GrantImmunity(player, minutes * 60d);
                    context.AddString($"Imune por {minutes} min.");
                    break;
                case "limpar":
                    PvpState.ClearImmunity(player);
                    PvpState.ClearPk();
                    context.AddString("Imunidade e PK locais limpos.");
                    break;
                case "pk":
                    PvpState.ApplyServerTimers(minutes * 60d, PvpState.HuntPendingRemaining, PvpState.HuntedRemaining);
                    context.AddString($"PK local por {minutes} min (so neste cliente).");
                    break;
                case "bounty":
                {
                    // /pvpadmin bounty <jogador> <moedas>: a casa paga; pode ser em si mesmo.
                    if (args.Length < 4 || !PvpBounty.TryParseAmount(args[args.Length - 1], out int amount))
                    {
                        context.AddString("pvpadmin bounty <jogador> <moedas>");
                        break;
                    }
                    string target = string.Join(" ", args.Args, 2, args.Length - 3);
                    PvpClient.SendBounty("admin", target, 0, out _, amount);
                    context.AddString($"Bounty de {amount} (paga pela casa) em {target}.");
                    break;
                }
                case "pve":
                {
                    // /pvpadmin pve <jogador>: tira do PvE permanente (o jogador nao consegue sozinho).
                    if (args.Length < 3) { context.AddString("pvpadmin pve <jogador>"); break; }
                    string target = string.Join(" ", args.Args, 2, args.Length - 2);
                    PvpClient.SendPve("admin-off", target);
                    context.AddString($"Tirando {target} do PvE permanente...");
                    break;
                }
                default:
                    context.AddString("pvpadmin zona | imune <min> | limpar | pk <min> | bounty <jogador> <moedas> | pve <jogador>");
                    break;
            }
        }
    }
}
