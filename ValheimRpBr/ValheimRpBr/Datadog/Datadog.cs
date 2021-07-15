using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Net;
using UnityEngine;
using System;
using System.Net.NetworkInformation;
using System.Net.Http;
using Ping = System.Net.NetworkInformation.Ping;
using Deadheim.Server;

namespace ValheimRpBr.Discord
{
    [BepInPlugin("com.mod.Discord", "DiscordLogger", "1.0.1")]
    [BepInProcess("valheim.exe")]
    public class Discord : BaseUnityPlugin
    {
        public static Harmony harmony = new Harmony("mod.discord");

        public class DiscordBot
        {
            public static string WebhookUrl = "https://discord.com/api/webhooks/862492469466759168/tgv79xCNVnAydXXOHB-QEW6nn6d291I2WhknGyrYCsVlqT25aORl2JE5EVQl7skerzHR";
            public static bool cheatInfstam = false;
            public static bool cheatGhost = false;
            public static bool cheatNocost = false;
            public static bool cheatGod = false;
            public static bool cheatFly = false;
            public static DateTime lastExecution;
            public static DateTime lastPingExecution;
            public static DateTime lastPlayerPositionExecution;

            public static void postBetterWardsMessage(string message)
            {
                ServerPatches.SendLog("-- WardLog: " + message);               
            }

            public static void PostMessage(string message, string username, string url)
            {
                Dictionary<string, string> dictionary = new Dictionary<string, string>()
                    {
                        {
                            "content",
                            message
                        }
                    };

                if (username != null)
                    dictionary.Add(nameof(username), username);

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";
                using (StreamWriter streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {


                    streamWriter.Write(LitJson.JsonMapper.ToJson((object)dictionary));
                }
                httpWebRequest.GetResponseAsync();
            }

            [HarmonyPatch(typeof(Player), "FixedUpdate")]
            public static class PlayerCheck
            {
                private static void Postfix(Player __instance)
                {
                    if (String.IsNullOrEmpty(__instance.GetPlayerName())) return;
                    if (String.IsNullOrEmpty(Plugin.steamId)) return;

                    SendPlayerPosition(__instance);
                    SendPlayerPing(__instance);
                    verifyIfPlayerIsCheating(__instance);
                }
            }

            public static void SendPlayerPing(Player __instance)
            {
                TimeSpan span = DateTime.Now - lastPingExecution;
                if (span.Minutes < 5) return;

                lastPingExecution = DateTime.Now;

                Ping pingSender = new Ping();
                PingOptions options = new PingOptions();

                PingReply reply = pingSender.Send("159.89.188.149");

                if (reply.RoundtripTime > 250)
                {
                    ServerPatches.SendLog("-- PingLog: " +  __instance.GetPlayerName() + " steamId: " + Plugin.steamId + " is laggy " + reply.RoundtripTime + "ms");
                }
            }

            public static void SendPlayerPosition(Player __instance)
            {
                TimeSpan span = DateTime.Now - lastPlayerPositionExecution;
                if (span.Minutes < 5) return;

                lastPlayerPositionExecution = DateTime.Now;
                ServerPatches.SendLog("-- LocationLog: " + __instance.GetPlayerName() + " steamId: " + Plugin.steamId + " is located at: " + __instance.transform.position);
            }

            public static void verifyIfPlayerIsCheating(Player __instance)
            {
                string str = "";
                if (!Discord.DiscordBot.cheatInfstam && ((double)__instance.m_runStaminaDrain == 0.0 || (double)__instance.GetMaxStamina() > 600.0))
                {
                    Discord.DiscordBot.cheatInfstam = true;
                    str = "Infinite Stamina";
                }
                if (!Discord.DiscordBot.cheatGhost && __instance.InGhostMode())
                {
                    Discord.DiscordBot.cheatGhost = true;
                    str = "Ghost";
                }
                if (!Discord.DiscordBot.cheatNocost && __instance.NoCostCheat())
                {
                    Discord.DiscordBot.cheatNocost = true;
                    str = "NoCost";
                }
                if (!Discord.DiscordBot.cheatGod && __instance.InGodMode())
                {
                    Discord.DiscordBot.cheatGod = true;
                    str = "God";
                }
                if (!Discord.DiscordBot.cheatFly && __instance.InDebugFlyMode())
                {
                    Discord.DiscordBot.cheatFly = true;
                    str = "FlyMode";
                }

                string prohibitedProcess = GetActiveProhibitedProcesses();

                if (!String.IsNullOrEmpty(prohibitedProcess))
                {
                    str = prohibitedProcess;
                }

                if (str == "")
                    return;

                if (Plugin.adminList.Contains(Plugin.steamId)) return;

                PostMessage("**" + __instance.GetPlayerName() + "** steamId: **" + Plugin.steamId + "** is using Cheat**" + str + "**", "DetailsBotLog", WebhookUrl);
            }

            private static string GetActiveProhibitedProcesses()
            {
                TimeSpan span = DateTime.Now - lastExecution;
                if (span.Minutes < 5) return null;
                lastExecution = DateTime.Now;

                List<string> prohibitedProcessList = new List<string> { "smi", "smi_gui", "wemod", "trainer", "pitch", "cheat" };
                foreach (string processName in prohibitedProcessList)
                {
                    Process[] processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0) return processName;
                }

                return null;
            }

            [HarmonyPatch(typeof(Chat), "OnNewChatMessage")]
            internal class OnNewChatMessage
            {
                private static void Prefix(GameObject go, long senderID, Vector3 pos, Talker.Type type, string user, string text)
                {
                    ServerPatches.SendLog("-- Chatlog: " + text + " steamId: " + Plugin.steamId + " user:" + user + " LocalChat");
                }
            }
        }
    }
}

