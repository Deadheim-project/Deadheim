using Deadheim.Vanilla;
using HarmonyLib;
using System;
using System.Text;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>Comandos de chat do PvP: /pvp, /cla, /desafio, /rank e /pvpadmin.</summary>
    [HarmonyPatch]
    internal static class PvpCommands
    {
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class RegisterPatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("pvp", "estado do PvP e ajuda", args => Status(args.Context));

                new Terminal.ConsoleCommand("desafio", "[cancelar|status] vira CACADO: aparece no mapa e perde a zona segura",
                    args => RequireOnline(args.Context, () => PvpClient.SendChallenge(args.Length > 1 ? args[1] : "start")));

                new Terminal.ConsoleCommand("rank", "ranking PvP (abates/mortes)",
                    args => RequireOnline(args.Context, PvpClient.SendRankRequest));

                new Terminal.ConsoleCommand("pvpadmin", "(admin) zona | imune <min> | limpar | pk <min>", AdminCommand);
            }
        }

        [HarmonyPatch(typeof(Chat), nameof(Chat.Awake))]
        private static class ChatHintPatch
        {
            private static void Postfix(Chat __instance)
            {
                int index = Math.Max(0, __instance.m_chatBuffer.Count - 5);
                __instance.m_chatBuffer.Insert(index, "/pvp estado | /desafio | /rank");
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
            if (PvpClient.ChallengeCooldownRemaining > 0d)
                text.AppendLine("Proximo desafio em " + PvpClient.FormatDuration(PvpClient.ChallengeCooldownRemaining) + ".");
            string guild = PvpGuilds.GuildOf(player);
            text.AppendLine(guild != null
                ? $"Guilda: {guild} (sem fogo amigo entre membros)"
                : "Sem guilda: entre numa pelo mod Guilds para ter aliados sem fogo amigo.");
            string castle = PvpBridge.Castle(player.transform.position);
            if (castle != null)
                text.AppendLine($"Castelo {castle}, dono: {PvpBridge.Owner(player.transform.position) ?? "ninguem"}.");
            text.AppendLine("/desafio - vira CACADO por " + PvpConfig.ChallengeDurationMinutes.Value.ToString("0") + " min, com recompensa");
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
                default:
                    context.AddString("pvpadmin zona | imune <min> | limpar | pk <min>");
                    break;
            }
        }
    }
}
