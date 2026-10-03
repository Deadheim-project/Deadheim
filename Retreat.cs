using Deadheim.Shared;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    [HarmonyPatch]
    public class Retreat
    {
        /// <summary>
        /// Ponto de retorno (o mesmo da pedra do mod Hearthstone: chaves positionX/Y/Z). Lido com
        /// ponto ou virgula: o formato antigo gravava com a cultura do PC, e o personagem no
        /// servidor (ServerCharacters) aberto num Windows de outro idioma lia "123,45" como 12345.
        /// Ilegivel = sem ponto.
        /// </summary>
        public static Vector3 GetHearthStonePosition()
        {
            Player player = Player.m_localPlayer;
            if (player == null
                || !player.m_customData.TryGetValue("positionX", out string rawX)
                || !player.m_customData.TryGetValue("positionY", out string rawY)
                || !player.m_customData.TryGetValue("positionZ", out string rawZ))
                return Vector3.zero;

            if (!Numeros.TryLer(rawX, out float x) || !Numeros.TryLer(rawY, out float y) || !Numeros.TryLer(rawZ, out float z))
            {
                Debug.LogWarning($"[Deadheim] Ponto de retreat ilegivel ({rawX}; {rawY}; {rawZ}): defina de novo na cama.");
                return Vector3.zero;
            }
            return new Vector3(x, y, z);
        }

		[HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
		public class AddChatCommands
		{
			private static void Postfix()
			{
				new Terminal.ConsoleCommand("retreat", "go back home", (Terminal.ConsoleEvent)(args =>
				{
                    if (!VipList.VipListApi.IsLocalPlayerVip())
                    {
                       args.Context.AddString("Only Aesir can use this command");
                       return;
                    }

                    // allowAllItems e' novo no Valheim 1.0. false mantem a regra de sempre --
                    // minerio e outros itens marcados continuam impedindo o teleporte; true
                    // ignoraria essa restricao, que nao e' o que este comando fazia.
                    if (!Player.m_localPlayer.IsTeleportable(false))
                    {
                        args.Context.AddString("Can't teleport");
                        return;
                    }

                    // Cooldown, combate e cacado: o retreat nao pode ser a saida de uma luta.
                    string refusal = Pvp.PvpModule.RetreatRefusal(Player.m_localPlayer);
                    if (refusal != null)
                    {
                        args.Context.AddString(refusal);
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, refusal);
                        return;
                    }

                    Vector3 teleportPosition = GetHearthStonePosition();

                    if (teleportPosition == Vector3.zero)
                    {
                        args.Context.AddString( "You need to set hearthstone spawn point");
                        return;
                    }

                    // TeleportTo devolve false se ja estiver teleportando, na recarga do vanilla ou se o
                    // PvP recusar: a recarga do retreat so conta quando o teleporte sai (B3).
                    if (Player.m_localPlayer.TeleportTo(teleportPosition, Player.m_localPlayer.transform.rotation, true))
                        Pvp.PvpModule.MarkRetreatUsed(Player.m_localPlayer);
                    else
                        args.Context.AddString("O teleporte nao saiu agora; tente de novo em instantes.");

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
            Player.m_localPlayer.m_customData["positionX"] = Numeros.Escrever(position.x);
            Player.m_localPlayer.m_customData["positionY"] = Numeros.Escrever(position.y);
            Player.m_localPlayer.m_customData["positionZ"] = Numeros.Escrever(position.z);
        }
    }
}
