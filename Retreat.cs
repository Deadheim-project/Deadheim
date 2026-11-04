using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
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
        /// Fase 3: ponto 0 = Hearthstone (positionXh/Yh/Zh; sem ele, cai no ponto 1 de antes).
        /// </summary>
        public static Vector3 GetHearthStonePosition(int ponto = 1)
        {
            Player player = Player.m_localPlayer;
            if (ponto == 0 && player != null && !player.m_customData.ContainsKey("positionXh")) ponto = 1;
            string sufixo = ponto == 2 ? "2" : ponto == 0 ? "h" : "";
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
					Usar(args.Length > 1 && args[1] == "2" ? 2 : 1, args.Context.AddString)));
			}
		}

        /// <summary>
        /// O /retreat inteiro, para o comando, o botao da Central Deadheim (hub do inventario) e a tecla
        /// (AesirTeclas). Devolve null quando o teleporte saiu, ou o motivo de nao ter saido. Cada recusa
        /// vai ao log do Aesir 18 (e ao do servidor).
        /// </summary>
        public static string TentarRetreat(int ponto)
        {
            try
            {
                Player player = Player.m_localPlayer;
                if (player == null) return "Entre no mundo primeiro.";
                if (!VipList.VipListApi.IsLocalPlayerVip())
                {
                    AesirLog.D(18, "retreat negado: nao e VIP.");
                    return "Only Aesir can use this command";
                }

                // Aesir 18: o ponto 2 so com a chave ligada.
                if (ponto == 2 && (AesirConfig.Aesir18DoisPontos == null || !AesirConfig.Aesir18DoisPontos.Value))
                {
                    AesirLog.D(18, "retreat 2 negado: Aesir18DoisPontos=false.");
                    return "Segundo ponto desligado neste servidor.";
                }

                // allowAllItems e' novo no Valheim 1.0. false mantem a regra de sempre --
                // minerio e outros itens marcados continuam impedindo o teleporte; true
                // ignoraria essa restricao, que nao e' o que este comando fazia.
                if (!player.IsTeleportable(false))
                {
                    AesirLog.D(18, "retreat " + ponto + " negado: carrega item nao teleportavel.");
                    return "Can't teleport";
                }

                // Cooldown, combate e cacado: o retreat nao pode ser a saida de uma luta.
                Pvp.PvpModule.Cooldown cooldown = ponto == 2 ? Pvp.PvpModule.Cooldown.Retreat2 : Pvp.PvpModule.Cooldown.Retreat1;
                string refusal = Pvp.PvpModule.RetreatRefusal(player, cooldown);
                if (refusal != null)
                {
                    AesirLog.D(18, "retreat " + ponto + " negado pelo PvP: " + refusal);
                    player.Message(MessageHud.MessageType.Center, refusal);
                    return refusal;
                }

                Vector3 teleportPosition = GetHearthStonePosition(ponto);
                if (teleportPosition == Vector3.zero)
                {
                    AesirLog.D(18, "retreat " + ponto + " negado: ponto " + ponto + " nao gravado (ou ilegivel).");
                    return ponto == 2 ? "Voce precisa definir o segundo ponto (Shift+" + TeclaDoPonto + " na cama)." : "You need to set hearthstone spawn point";
                }

                // TeleportTo devolve false se ja estiver teleportando, na recarga do vanilla ou se o
                // PvP recusar: a recarga do retreat so conta quando o teleporte sai (B3).
                if (!player.TeleportTo(teleportPosition, player.transform.rotation, true))
                {
                    AesirLog.D(18, "retreat " + ponto + ": TeleportTo recusou (ja teleportando ou recarga).");
                    return "O teleporte nao saiu agora; tente de novo em instantes.";
                }

                Pvp.PvpModule.MarkRetreatUsed(player, cooldown);
                AesirLog.D(18, "retreat " + ponto + ": teleportou.");
                return null;
            }
            catch (Exception e)
            {
                AesirLog.Erro(18, e);
                return "Erro no retreat; tente de novo.";
            }
        }

        /// <summary>O TentarRetreat com a recusa no console ou na tela (<paramref name="dizer"/>).</summary>
        internal static void Usar(int ponto, Action<string> dizer)
        {
            string recusa = TentarRetreat(ponto);
            if (recusa != null) dizer?.Invoke(recusa);
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
                    // Fase 3: uma linha so, com a tecla do menu e o estado de cada ponto.
                    bool vip = VipList.VipListApi.IsLocalPlayerVip();
                    bool dois = AesirConfig.Aesir18DoisPontos != null && AesirConfig.Aesir18DoisPontos.Value;
                    string r1 = vip ? Estado(1) : "so Aesir";
                    string r2 = vip && dois ? Estado(2) : (vip ? "desligado" : "so Aesir");
                    __result += Localization.instance.Localize(
                        $"\n[<color=yellow><b>{TeclaDoPonto}</b></color>] definir: Hearthstone ({Estado(0)}) · Retreat 1 ({r1}) · Retreat 2 ({r2})");
                    // A volta ao ponto sai por tecla de qualquer lugar (AesirTeclas): a cama lembra quais.
                    if (vip)
                    {
                        __result += AesirTeclas.Dica(AesirTeclas.Retreat, "Voltar ao ponto de retreat (de qualquer lugar)");
                        if (dois) __result += AesirTeclas.Dica(AesirTeclas.Retreat2, "Voltar ao segundo ponto");
                    }
                }
            }
        }

        /// <summary>"definido" ou "nao" para o ponto (0 Hearthstone, 1 Retreat 1, 2 Retreat 2).</summary>
        internal static string Estado(int ponto) => GetHearthStonePosition(ponto) != Vector3.zero ? "definido" : "não";


        private const string HearthstoneGuid = "Detalhes.Hearthstone";
        private static ConfigEntry<KeyCode> _teclaHearthstone;

        public static bool HearthstoneCarregado => Chainloader.PluginInfos.ContainsKey(HearthstoneGuid);

        /// <summary>
        /// Tecla da cama: a setPointKey do mod Hearthstone (de cada jogador), lida pelo Config dele
        /// porque o Deadheim nao referencia aquele assembly. Sem a pedra (ou antes da 2.1.2), P.
        /// </summary>
        public static KeyCode TeclaDoPonto
        {
            get
            {
                if (_teclaHearthstone == null
                    && Chainloader.PluginInfos.TryGetValue(HearthstoneGuid, out PluginInfo info)
                    && info?.Instance?.Config != null)
                    info.Instance.Config.TryGetEntry("Client", "setPointKey", out _teclaHearthstone);
                return _teclaHearthstone?.Value ?? KeyCode.P;
            }
        }

        /// <summary>Aesir 18: <paramref name="ponto"/> 1 ou 2 (fora disso cai no 1).</summary>
        public static void SetHearthStonePosition(int ponto = 1)
        {
            if (Player.m_localPlayer == null) return;

            string sufixo = ponto == 2 ? "2" : ponto == 0 ? "h" : "";
            Vector3 position = Player.m_localPlayer.transform.position;
            Player.m_localPlayer.m_customData["positionX" + sufixo] = Numeros.Escrever(position.x);
            Player.m_localPlayer.m_customData["positionY" + sufixo] = Numeros.Escrever(position.y);
            Player.m_localPlayer.m_customData["positionZ" + sufixo] = Numeros.Escrever(position.z);
        }
    }
}
