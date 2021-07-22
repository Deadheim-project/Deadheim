using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;

namespace Deadheim.AntiCheat
{
    [BepInPlugin("KGAnticheat", "KGAmegame", "1.0.3")]
    public class KGAntiCheat : BaseUnityPlugin
    {
        private static string MESSAGE = "ATUALIZE SUA INSTALAÇÃO DO DEADHEIM";
        private static string hhh = "loh";
        private static Harmony HARM = new Harmony("KGAnticheat");
        private static List<string> ExcludePlugins = new List<string>();
        private static List<string> ExcludeConfigs = new List<string>();
        private string InfoDebug = string.Empty;
        private static MethodInfo ILsearch = AccessTools.Method(typeof(ZPackage), "Write", new System.Type[1]
        {
      typeof (byte[])
        }, (System.Type[])null);
        private static MethodInfo ILwrite = AccessTools.Method(typeof(ZPackage), "Write", new System.Type[1]
        {
      typeof (string)
        }, (System.Type[])null);

        private string hhhPLget(bool Server)
        {
            string path = (string)null;
            if (Server)
                path = Path.Combine(Paths.BepInExRootPath, "ONLYCLIENT_plugins");
            if (!Server)
                path = Path.Combine(Paths.BepInExRootPath, "plugins");
            List<string> list = ((IEnumerable<string>)Directory.GetFiles(path, ".", SearchOption.AllDirectories)).OrderBy<string, string>((Func<string, string>)(p => p)).ToList<string>();
            if (KGAntiCheat.ExcludePlugins.Count > 0)
            {
                foreach (string str in list.ToArray())
                {
                    foreach (string excludePlugin in KGAntiCheat.ExcludePlugins)
                    {
                        if (str.EndsWith(excludePlugin))
                            list.Remove(str);
                    }
                }
            }
            StringBuilder stringBuilder = new StringBuilder();
            for (int index = 0; index < list.Count; ++index)
            {
                MD5.Create();
                byte[] buffer = File.ReadAllBytes(list[index]);
                string lower = BitConverter.ToString(MD5.Create().ComputeHash(buffer)).Replace("-", "").ToLower();
                this.InfoDebug = this.InfoDebug + "Filename: " + list[index] + " hash: " + lower + "\n";
                stringBuilder.Append(lower);
            }
            return BitConverter.ToString(MD5.Create().ComputeHash(Encoding.ASCII.GetBytes(stringBuilder.ToString()))).Replace("-", "").ToLower();
        }

        private string hhhCFget(bool Server)
        {
            string path = (string)null;
            if (Server)
                path = Path.Combine(Paths.BepInExRootPath, "ONLYCLIENT_config");
            if (!Server)
                path = Path.Combine(Paths.BepInExRootPath, "config");
            List<string> list = ((IEnumerable<string>)Directory.GetFiles(path, ".", SearchOption.AllDirectories)).OrderBy<string, string>((Func<string, string>)(p => p)).ToList<string>();
            if (KGAntiCheat.ExcludeConfigs.Count > 0)
            {
                foreach (string str in list.ToArray())
                {
                    foreach (string excludeConfig in KGAntiCheat.ExcludeConfigs)
                    {
                        if (str.EndsWith(excludeConfig))
                            list.Remove(str);
                    }
                }
            }
            StringBuilder stringBuilder = new StringBuilder();
            for (int index = 0; index < list.Count; ++index)
            {
                MD5.Create();
                byte[] bytes = Encoding.ASCII.GetBytes(File.ReadAllText(list[index]).Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("#", ""));
                string lower = BitConverter.ToString(MD5.Create().ComputeHash(bytes)).Replace("-", "").ToLower();
                this.InfoDebug = this.InfoDebug + "Filename: " + list[index] + " hash: " + lower + "\n";
                stringBuilder.Append(lower);
            }
            return BitConverter.ToString(MD5.Create().ComputeHash(Encoding.ASCII.GetBytes(stringBuilder.ToString()))).Replace("-", "").ToLower();
        }

        private string hhhCombine(string one, string two)
        {
            string str1 = (string)null;
            char ch;
            for (int index = 0; index < 32; ++index)
            {
                if (index % 2 == 0)
                {
                    string str2 = str1;
                    ch = one[index];
                    string str3 = ch.ToString();
                    str1 = str2 + str3;
                }
                else
                {
                    string str2 = str1;
                    ch = two[index];
                    string str3 = ch.ToString();
                    str1 = str2 + str3;
                }
            }
            this.InfoDebug = this.InfoDebug + "\n\n\nFINAL PLUGIN HASH: " + one + "\nFINAL CONFIG HASH: " + two + "\nCOMBINED: " + str1;
            File.WriteAllText(Path.Combine(Paths.BepInExRootPath + "Hash.txt"), this.InfoDebug);
            return str1;
        }

        private void Awake()
        {
            File.Exists(Path.Combine(Paths.PluginPath, "KGExclude.cfg"));
            KGAntiCheat.ExcludeConfigs.Add("BepInEx.cfg");
            KGAntiCheat.ExcludeConfigs.Add("valheim_plus.cfg");
            KGAntiCheat.ExcludeConfigs.Add("HackShardGaming.WorldofValheimZones.cfg");
            KGAntiCheat.ExcludeConfigs.Add("RewardSystemKG.cfg");
            KGAntiCheat.ExcludeConfigs.Add("Koosemose.MountUp.cfg");
            KGAntiCheat.ExcludeConfigs.Add("org.bepinex.plugins.creaturelevelcontrol.cfg");
            KGAntiCheat.ExcludePlugins.Add("ValheimFPSBoost.dll");
            bool Server = Paths.ProcessName.Equals("valheim_server", StringComparison.OrdinalIgnoreCase);
            KGAntiCheat.hhh = this.hhhCombine(this.hhhPLget(Server), this.hhhCFget(Server));
            if (Server)
                return;
            MethodInfo methodInfo = AccessTools.Method(typeof(ZNet), "SendPeerInfo", new System.Type[2]
            {
        typeof (ZRpc),
        typeof (string)
            }, (System.Type[])null);
            KGAntiCheat.HARM.Patch(methodInfo, null, null, new HarmonyMethod(AccessTools.Method(typeof(Trans), "ZpeerTranspiler", new Type[1]
            {
                typeof (IEnumerable<CodeInstruction>)
            }, null)), null, null);
        }


        [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
        public static class Znet_Patch
        {
            private static bool Prefix(ref ZNet __instance, ZRpc rpc, ZPackage pkg)
            {
                if (__instance.IsServer())
                {
                    string str1 = "";
                    if (pkg.Size() > 32)
                    {
                        pkg.SetPos(pkg.Size() - 32 - 1);
                        if (pkg.ReadByte() == (byte)32)
                        {
                            pkg.SetPos(pkg.GetPos() - 1);
                            str1 = pkg.ReadString();
                        }
                    }
                    pkg.SetPos(0);

                    string steamId = rpc.GetSocket().GetHostName();

                    bool flag = false;
                    if (__instance.m_adminList.Contains(steamId))
                        flag = true;
                    ZLog.Log((object)("CLIENTs HASH = " + str1 + ", SERVER HASH = " + KGAntiCheat.hhh));
                    if (!str1.Equals(KGAntiCheat.hhh) && !flag)
                    {
                        int num = Utility.IsNullOrWhiteSpace(str1) ? 3 : 99;
                        return InvokeError(rpc, num);
                    }
                }
                return true;
            }
        }

        private static bool InvokeError(ZRpc rpc, int num)
        {
            rpc.Invoke("Error", new object[1]
                {
                   num
                });

            return false;
        }

        public static class Trans
        {
            private static IEnumerable<CodeInstruction> ZpeerTranspiler(
              IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codeInstructionList = new List<CodeInstruction>(instructions);
                int lastIndex = codeInstructionList.FindLastIndex((Predicate<CodeInstruction>)(x => CodeInstructionExtensions.Calls(x, KGAntiCheat.ILsearch)));
                codeInstructionList.InsertRange(lastIndex + 1, (IEnumerable<CodeInstruction>)new CodeInstruction[3]
                {
          new CodeInstruction(OpCodes.Ldloc, (object) 0),
          new CodeInstruction(OpCodes.Ldstr, (object) KGAntiCheat.hhh),
          new CodeInstruction(OpCodes.Callvirt, (object) KGAntiCheat.ILwrite)
                });
                return (IEnumerable<CodeInstruction>)codeInstructionList;
            }
        }

        [HarmonyPatch(typeof(FejdStartup), "ShowConnectError")]
        [HarmonyPriority(800)]
        public static class FedjStartupMessage
        {
            public static void Postfix(FejdStartup __instance)
            {
                if ((int)ZNet.GetConnectionStatus() != 99)
                    return;
                ((UnityEngine.UI.Text)__instance.m_connectionFailedError).text = MESSAGE;
            }
        }
    }
}
