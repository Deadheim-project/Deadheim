using HarmonyLib;
using System;
using System.Reflection;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Deadheim.world
{
	internal class World
	{
		[HarmonyPrefix]
		[HarmonyPatch(typeof(DungeonGenerator), "Generate", typeof(ZoneSystem.SpawnMode))]
		private static void ApplyGeneratorSettings(ref DungeonGenerator __instance)
		{
			__instance.m_minRooms = 15;
			__instance.m_maxRooms = 30;
			__instance.m_campRadiusMin = 20;
			__instance.m_campRadiusMax = 40;
		}

        [HarmonyPatch(typeof(ZNet), "SaveWorldThread")]
        internal class SaveWorldThread
        {
            private static void Prefix(ref ZNet __instance)
            {
				if (Plugin.Age.Value == "stone" && Plugin.ResetWorldDay.Value == true)  __instance.m_netTime = 2040.0;
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.UpdateSaving))]
		private static class PatchGameUpdateSaving
		{
			private static readonly MethodInfo getAutoSaveInterval = AccessTools.DeclaredMethod(typeof(PatchGameUpdateSaving), nameof(getAutoSaveIntervalSetting));

			[UsedImplicitly]
			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				foreach (CodeInstruction instruction in instructions)
				{
					if (instruction.opcode == OpCodes.Ldc_R4 && instruction.OperandIs(Game.m_saveInterval))
					{
						yield return new CodeInstruction(OpCodes.Call, getAutoSaveInterval);
					}
					else
					{
						yield return instruction;
					}
				}
			}

			private static float getAutoSaveIntervalSetting() => 1800;
		}
	}
}
