using HarmonyLib;
using System;

namespace Deadheim.Patches
{ 
    [HarmonyPatch]
    public class ServerPatches
    {
        static bool alreadyRouted = false;

        [HarmonyPatch(typeof(Game), "Start")]
        [HarmonyPrefix]
        public static void Prefix()
        {
            if (alreadyRouted == false)
            {
                alreadyRouted = true;
                ZRoutedRpc.instance.Register<ZPackage>("SendLog", new Action<long, ZPackage>(Datadog.Datadog.RPC_SendLog));
            }
        }

    }
}
