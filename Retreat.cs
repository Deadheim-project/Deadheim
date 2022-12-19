using HarmonyLib;
using System;
using UnityEngine;

namespace Deadheim
{
    [HarmonyPatch]
    public class Retreat
    {
        unsafe public static Vector3 GetHearthStonePosition()
        {
            if (!Player.m_localPlayer.m_knownTexts.ContainsKey("positionX"))
            {
                return Vector3.zero;
            }

            return new Vector3
            {
                x = float.Parse(Player.m_localPlayer.m_knownTexts["positionX"]),
                y = float.Parse(Player.m_localPlayer.m_knownTexts["positionY"]),
                z = float.Parse(Player.m_localPlayer.m_knownTexts["positionZ"])
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
    }
}
