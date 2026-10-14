using Deadheim.Shared;
using Deadheim.Vanilla;
using HarmonyLib;
using System;
using System.Text;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Comandos de chat do PvP: /pvp, /rank e /pvpadmin. Bounty e troca PvE/PvP nao sao mais
    /// comando: ficam nos NPCs Cacador de Recompensas e Juiz de Guerra (NpcValheim, pela PvpApi).
    /// </summary>
    [HarmonyPatch]
    internal static class PvpCommands
    {
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class RegisterPatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("pvp", "estado do PvP e ajuda", args => Status(args.Context));

                new Terminal.ConsoleCommand("rank", "ranking PvP (abates/mortes)",
                    args => RequireOnline(args.Context, PvpClient.SendRankRequest));

                new Terminal.ConsoleCommand("pvpadmin", "(admin) zona | imune <min> | limpar | pk <min> [jogador] | bounty <jogador> <moedas> | pve <jogador>", AdminCommand);
            }
        }

        [HarmonyPatch(typeof(Chat), nameof(Chat.Awake))]
        private static class ChatHintPatch
        {
            private static void Postfix(Chat __instance)
            {
                int index = Math.Max(0, __instance.m_chatBuffer.Count - 5);
                __instance.m_chatBuffer.Insert(index, "/pvp estado | /rank");
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
            text.AppendLine(PvpBosses.RangeText(PvpBosses.LocalTier));
            if (PvpClient.BountyPot > 0)
                text.AppendLine($"Bounty na sua cabeca: {PvpClient.BountyPot} moedas" +
                                (PvpBounty.BuyoutCost(PvpClient.BountyPot) > 0 ? $" (pagar: {PvpBounty.BuyoutCost(PvpClient.BountyPot)} no Cacador de Recompensas)." : "."));
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
                text.AppendLine($"Voce e PvE ({PvpPve.Title}): nao luta com jogadores; skill x{PvpConfig.PveSkillMultiplier.Value:0.##}. " +
                                "Para voltar ao PvP, fale com o Juiz de Guerra.");
            else if (PvpConfig.PveEnabled.Value)
                text.AppendLine("Para sair do PvP (virar PvE), fale com o Juiz de Guerra.");
            if (PvpState.IsAggressor)
                text.AppendLine("Voce e AGRESSOR: bateu primeiro. Quem te matar agora nao vira PK (voce nao perde nada a mais).");
            string castle = PvpBridge.Castle(player.transform.position);
            if (castle != null)
                text.AppendLine($"Castelo {castle}, dono: {PvpBridge.Owner(player.transform.position) ?? "ninguem"}.");
            if (PvpConfig.BountyEnabled.Value)
                text.AppendLine("Bounty (cabeca a premio): fale com o Cacador de Recompensas.");
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
            // Minutos com ponto ou virgula, sem depender do idioma do Windows ("1.5" virava 15 em pt-BR).
            // Sem numero nao ha padrao: "/pvpadmin pk Fulano" marcava o proprio admin por 1 min.
            float minutes = 0f;
            bool hasMinutes = args.Length > 2 && Numeros.TryLer(args[2], out minutes);

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
                    context.AddString($"Bandeiras: {PvpState.Current} pvp={player.IsPVPEnabled()} chefes={PvpBosses.LocalTier}");
                    break;
                }
                case "imune":
                    if (!hasMinutes) { context.AddString("pvpadmin imune <minutos> (0 tira a imunidade)"); break; }
                    PvpState.GrantImmunity(player, minutes * 60d);
                    context.AddString($"Imune por {minutes} min.");
                    break;
                case "limpar":
                    PvpState.ClearImmunity(player);
                    PvpState.ClearPk();
                    context.AddString("Imunidade e PK locais limpos.");
                    break;
                case "pk":
                {
                    // /pvpadmin pk <min> [jogador]: o servidor marca (min > 0), torna permanente (-1) ou limpa (0).
                    if (!hasMinutes)
                    {
                        context.AddString("pvpadmin pk <minutos> [jogador]: minutos > 0 marca, 0 limpa, -1 permanente. Sem jogador, e voce.");
                        break;
                    }
                    string target = args.Length > 3 ? string.Join(" ", args.Args, 3, args.Length - 3) : string.Empty;
                    PvpClient.SendAdmin("pk", target, minutes);
                    context.AddString($"PK de {(target.Length > 0 ? target : "voce")}: " +
                                      (minutes < 0f ? "permanente" : minutes == 0f ? "limpar" : minutes + " min online") + "...");
                    break;
                }
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
                    // /pvpadmin pve <jogador>: tira do PvE na hora, sem esperar o cooldown da troca.
                    if (args.Length < 3) { context.AddString("pvpadmin pve <jogador>"); break; }
                    string target = string.Join(" ", args.Args, 2, args.Length - 2);
                    PvpClient.SendPve("admin-off", target);
                    context.AddString($"Tirando {target} do PvE...");
                    break;
                }
                default:
                    context.AddString("pvpadmin zona | imune <min> | limpar | pk <min> [jogador] | bounty <jogador> <moedas> | pve <jogador>");
                    break;
            }
        }
    }
}
