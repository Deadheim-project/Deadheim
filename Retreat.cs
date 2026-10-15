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
        ///
        /// Aesir 18 (14/10/2026, decisao do usuario): VIP tem um segundo ponto, nas chaves
        /// positionX2/Y2/Z2. <paramref name="ponto"/> 1 ou 2; fora disso cai no 1.
        /// </summary>
        public static Vector3 GetHearthStonePosition(int ponto = 1)
        {
            Player player = Player.m_localPlayer;
            string sufixo = ponto == 2 ? "2" : "";
            if (player == null
                || !player.m_customData.TryGetValue("positionX" + sufixo, out string rawX)
                || !player.m_customData.TryGetValue("positionY" + sufixo, out string rawY)
                || !player.m_customData.TryGetValue("positionZ" + sufixo, out string rawZ))
                return Vector3.zero;

            if (!Numeros.TryLer(rawX, out float x) || !Numeros.TryLer(rawY, out float y) || !Numeros.TryLer(rawZ, out float z))
            {
                Debug.LogWarning($"[Deadheim] Ponto de retreat {ponto} ilegivel ({rawX}; {rawY}; {rawZ}): defina de novo na cama.");
                return Vector3.zero;
            }
            return new Vector3(x, y, z);
        }

		[HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
		public class AddChatCommands
		{
			private static void Postfix()
			{
				new Terminal.ConsoleCommand("retreat", "go back home (Aesir: 'retreat 2' usa o segundo ponto)", (Terminal.ConsoleEvent)(args =>
				{
                    if (!VipList.VipListApi.IsLocalPlayerVip())
                    {
                       args.Context.AddString("Only Aesir can use this command");
                       return;
                    }

                    // Aesir 18: "retreat 2" usa o segundo ponto, se a chave estiver ligada.
                    int ponto = args.Length > 1 && args[1] == "2" ? 2 : 1;
                    if (ponto == 2 && (AesirConfig.Aesir18DoisPontos == null || !AesirConfig.Aesir18DoisPontos.Value))
                    {
                        args.Context.AddString("Segundo ponto desligado neste servidor.");
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

                    Vector3 teleportPosition = GetHearthStonePosition(ponto);

                    if (teleportPosition == Vector3.zero)
                    {
                        args.Context.AddString(ponto == 2 ? "Voce precisa definir o segundo ponto (Shift+P na cama)." : "You need to set hearthstone spawn point");
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
                    // Aesir 18: segundo ponto, com Shift+P.
                    if (AesirConfig.Aesir18DoisPontos != null && AesirConfig.Aesir18DoisPontos.Value && VipList.VipListApi.IsLocalPlayerVip())
                        __result += Localization.instance.Localize($"\n[<color=yellow><b>Shift+P</b></color>] Definir o segundo ponto de retreat");
                }
            }
        }


        /// <summary>Aesir 18: <paramref name="ponto"/> 1 ou 2 (fora disso cai no 1).</summary>
        public static void SetHearthStonePosition(int ponto = 1)
        {
            if (Player.m_localPlayer == null) return;

            string sufixo = ponto == 2 ? "2" : "";
            Vector3 position = Player.m_localPlayer.transform.position;
            Player.m_localPlayer.m_customData["positionX" + sufixo] = Numeros.Escrever(position.x);
            Player.m_localPlayer.m_customData["positionY" + sufixo] = Numeros.Escrever(position.y);
            Player.m_localPlayer.m_customData["positionZ" + sufixo] = Numeros.Escrever(position.z);
        }
    }
}
