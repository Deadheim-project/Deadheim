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
                context?.AddString(L.T("pvpcmd.modulo_off"));
                return;
            }
            if (ZNet.instance == null || Player.m_localPlayer == null)
            {
                context?.AddString(L.T("pvpcmd.entre"));
                return;
            }
            action();
        }

        private static void Status(Terminal context)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                context?.AddString(L.T("pvpcmd.entre"));
                return;
            }

            StringBuilder text = new StringBuilder();
            text.AppendLine(L.T("pvpcmd.estado", PvpHud.Compose(player)));
            text.AppendLine(L.T("pvpcmd.dano", PvpConfig.DamageMultiplier.Value.ToString("0.##"), (PvpConfig.DamageMultiplier.Value * PvpConfig.WardDefenseMultiplier.Value).ToString("0.##")));
            text.AppendLine(PvpBosses.RangeText(PvpBosses.LocalTier));
            if (PvpClient.BountyPot > 0)
                text.AppendLine(L.T("pvpcmd.bounty_cab", PvpClient.BountyPot, PvpBounty.BuyoutCost(PvpClient.BountyPot) > 0 ? L.T("pvpcmd.pagar", PvpBounty.BuyoutCost(PvpClient.BountyPot)) : "."));
            else if (PvpClient.BountyCooldownRemaining > 0d)
                text.AppendLine(L.T("pvpcmd.ninguem_bounty", PvpClient.FormatDuration(PvpClient.BountyCooldownRemaining)));
            string guild = PvpGuilds.GuildOf(player);
            text.AppendLine(guild != null
                ? L.T("pvpcmd.guilda", guild)
                : L.T("pvpcmd.sem_guilda"));
            if (PvpGroups.IsAvailable)
                text.AppendLine(L.T("pvpcmd.grupo"));
            text.AppendLine(PvpState.PkCount > 0
                ? L.T("pvpcmd.contador", PvpState.PkCount)
                : L.T("pvpcmd.contador0"));
            if (PvpState.IsPk)
                text.AppendLine((PvpState.IsPkPermanent ? L.T("pvpcmd.voce_pk_perm") : L.T("pvpcmd.voce_pk"))
                                + L.T("pvpcmd.se_morrer", PvpConfig.PkSkillLossMultiplier.Value.ToString("0.#"), L.Resolve(PvpServer.PenaltyText(PvpState.PkPenalty))));
            if (PvpPve.IsLocal)
                text.AppendLine(L.T("pvpcmd.voce_pve", PvpPve.Title, PvpConfig.PveSkillMultiplier.Value.ToString("0.##")));
            else if (PvpConfig.PveEnabled.Value)
                text.AppendLine(L.T("pvpcmd.sair_pvp"));
            if (PvpState.IsAggressor)
                text.AppendLine(L.T("pvpcmd.agressor"));
            string castle = PvpBridge.Castle(player.transform.position);
            if (castle != null)
                text.AppendLine(L.T("pvpcmd.castelo", castle, PvpBridge.Owner(player.transform.position) ?? L.T("pvpcmd.ninguem")));
            if (PvpConfig.BountyEnabled.Value)
                text.AppendLine(L.T("pvpcmd.bounty_info"));
            text.Append(L.T("pvpcmd.rank_cmd"));
            foreach (string line in text.ToString().Split('\n')) context?.AddString(line.TrimEnd('\r'));
        }

        private static void AdminCommand(Terminal.ConsoleEventArgs args)
        {
            Terminal context = args.Context;
            if (!Vanilla.Admin.LocalPlayerIsAdmin())
            {
                context.AddString(L.T("pvpcmd.so_admin"));
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
