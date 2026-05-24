using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    [HarmonyPatch]
    public class Retreat
    {
        public static Vector3 GetHearthStonePosition()
        {
            if (Player.m_localPlayer == null || !Player.m_localPlayer.m_customData.ContainsKey("positionX"))
            {
                return Vector3.zero;
            }

            return new Vector3
            {
                x = float.Parse(Player.m_localPlayer.m_customData["positionX"]),
                y = float.Parse(Player.m_localPlayer.m_customData["positionY"]),
                z = float.Parse(Player.m_localPlayer.m_customData["positionZ"])
            };
        }

		[HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
		public class AddChatCommands
		{
			private static void Postfix()
			{
				new Terminal.ConsoleCommand("retreat", "go back home", (Terminal.ConsoleEvent)(args =>
				{
                    if (!Plugin.Vip.Value.Contains(Plugin.steamId))
                    {
                       args.Context.AddString("Only Aesir can use this command");
                       return;
                    }

                    if (!Player.m_localPlayer.IsTeleportable())
                    {
                        args.Context.AddString("Can't teleport");
                        return;
                    }

                    Vector3 teleportPosition = GetHearthStonePosition();

                    if (teleportPosition == Vector3.zero)
                    {
                        args.Context.AddString( "You need to set hearthstone spawn point");
                        return;
                    }

                    Player.m_localPlayer.TeleportTo(teleportPosition, Player.m_localPlayer.transform.rotation, true);

                }));			
			}
		}


        [HarmonyPatch(typeof(Chat), nameof(Chat.Awake))]
        public class AddGroupChat
        {
            private static void Postfix(Chat __instance)
            {
                int index = Math.Max(0, __instance.m_chatBuffer.Count - 5);
                __instance.m_chatBuffer.Insert(index, "/retreat go back home");
                __instance.UpdateChat();
            }
        }



        [HarmonyPatch(typeof(Bed), "GetHoverText")]
        static class Bed_GetHoverText_Patch
        {
            static void Postfix(Bed __instance, ref string __result, ZNetView ___m_nview)
            {
                if (__instance.IsMine() && (___m_nview.GetZDO().GetLong("owner", 0L) != 0) || Traverse.Create(__instance).Method("IsCurrent").GetValue<bool>())
                {
                    __result += Localization.instance.Localize($"\n[<color=yellow><b>P</b></color>] Definir ponto de retreat");
                }
            }
        }


        public static void SetHearthStonePosition()
        {
            if (Player.m_localPlayer == null) return;

            Vector3 position = Player.m_localPlayer.transform.position;
            Player.m_localPlayer.m_customData["positionX"] = position.x.ToString();
            Player.m_localPlayer.m_customData["positionY"] = position.y.ToString();
            Player.m_localPlayer.m_customData["positionZ"] = position.z.ToString();
        }
    }
}
