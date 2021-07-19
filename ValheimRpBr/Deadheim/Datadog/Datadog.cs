using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Net;
using UnityEngine;
using System;
using System.Net.NetworkInformation;
using Ping = System.Net.NetworkInformation.Ping;

namespace Deadheim.Datadog
{
    public class Datadog
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
            SendLog("-- WardLog: " + message);
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
                SendLog("-- PingLog: " + __instance.GetPlayerName() + " steamId: " + Plugin.steamId + " is laggy " + reply.RoundtripTime + "ms");
            }
        }

        public static void SendPlayerPosition(Player __instance)
        {
            TimeSpan span = DateTime.Now - lastPlayerPositionExecution;
            if (span.Minutes < 1) return;

            lastPlayerPositionExecution = DateTime.Now;
            SendLog("-- LocationLog: " + __instance.GetPlayerName() + " steamId: " + Plugin.steamId + " is located at: " + __instance.transform.position);
        }

        public static void verifyIfPlayerIsCheating(Player __instance)
        {
            string str = "";
            if (!cheatInfstam && ((double)__instance.m_runStaminaDrain == 0.0 || (double)__instance.GetMaxStamina() > 600.0))
            {
                cheatInfstam = true;
                str = "Infinite Stamina";
            }
            if (!cheatGhost && __instance.InGhostMode())
            {
                cheatGhost = true;
                str = "Ghost";
            }
            if (!cheatNocost && __instance.NoCostCheat())
            {
                cheatNocost = true;
                str = "NoCost";
            }
            if (!cheatGod && __instance.InGodMode())
            {
                cheatGod = true;
                str = "God";
            }
            if (!cheatFly && __instance.InDebugFlyMode())
            {
                cheatFly = true;
                str = "FlyMode";
            }

            string prohibitedProcess = GetActiveProhibitedProcesses();

            if (!String.IsNullOrEmpty(prohibitedProcess))
            {
                str = prohibitedProcess;
            }

            if (str == "")
                return;

            if (BetterWards.BetterWardsPlugin.admin) return;

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

        public static void SendLog(string msg)
        {
            ZPackage zpackage = new ZPackage();
            zpackage.Write(msg);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "SendLog", zpackage);
        }

        public static void RPC_SendLog(long sender, ZPackage pkg)
        {
            if (!ZNet.m_isServer) return;

            using (StreamWriter streamWriter1 = new StreamWriter(Utils.GetSaveDataPath() + "/moderation.txt", true))
            {
                StreamWriter streamWriter2 = streamWriter1;
                DateTime dateTime = DateTime.Now;
                dateTime = dateTime.ToUniversalTime();
                string str = dateTime.ToString() + " " + pkg.ReadString();
                streamWriter2.WriteLine(str);
            }
        }
    }
}