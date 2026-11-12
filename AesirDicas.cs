using HarmonyLib;
using System;
using System.Collections.Generic;

namespace Deadheim
{
    /// <summary>
    /// Dicas de tecla dos Aesir no texto do objeto na mira, no formato do jogo ("[Shift + E] ...").
    /// Decisao do usuario (02/11/2026): o que o VIP faz por tecla aparece no proprio objeto, como
    /// o "[E] Usar" do Valheim. A dica so aparece para quem pode usar (VIP ou item liberado a todos)
    /// e com a chave do item ligada. O segundo ponto de retreat (18) ja tem a dica na cama (Retreat.cs);
    /// o reparo em area (12) nao tem objeto na mira com texto, entao fica sem dica.
    ///
    /// O que antes era comando digitado (bau, retreat) virou tecla em AesirTeclas; a dica vem de la.
    ///
    /// GetHoverText roda a cada frame com o objeto na mira: nada de log aqui, so erro (agrupado).
    /// </summary>
    [HarmonyPatch]
    internal static class AesirDicas
    {
        private const string Aesir = " <color=#9be564>(Aesir)</color>";

        // Shift literal, nao $KEY_AltPlace: o MassPickUp e o abastecer olham a tecla Shift em si.
        private static string Tecla(string acao) =>
            Localization.instance.Localize("\n[<color=yellow><b>Shift + $KEY_Use</b></color>] ") + acao;

        // ------------------------------------------------------------------ 5: colher/extrair em massa

        private static bool Pode5(out bool soVip)
        {
            soVip = AesirConfig.Aesir5SoVip == null || AesirConfig.Aesir5SoVip.Value;
            return !soVip || VipList.VipListApi.IsLocalPlayerVip();
        }

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
        private static class ColherPatch
        {
            private static void Postfix(Pickable __instance, ref string __result)
            {
                try
                {
                    if (__instance == null || string.IsNullOrEmpty(__result) || !__instance.CanBePicked()) return;
                    if (Pode5(out bool soVip)) __result += Tecla(L.T("aesirmsg.d_colher") + (soVip ? Aesir : ""));
                }
                catch (Exception e) { AesirLog.Erro(5, e); }
            }
        }

        [HarmonyPatch(typeof(Beehive), nameof(Beehive.GetHoverText))]
        private static class ColmeiaPatch
        {
            private static void Postfix(ref string __result)
            {
                try
                {
                    if (string.IsNullOrEmpty(__result)) return;
                    if (Pode5(out bool soVip)) __result += Tecla(L.T("aesirmsg.d_extrair") + (soVip ? Aesir : ""));
                }
                catch (Exception e) { AesirLog.Erro(5, e); }
            }
        }

        // ------------------------------------------------------------------ 7: abastecer em area

        private static void Dica7(ref string texto)
        {
            if (string.IsNullOrEmpty(texto) || AesirConfig.Aesir7AbastecerAreaAtivo == null || !AesirConfig.Aesir7AbastecerAreaAtivo.Value) return;
            if (!VipList.VipListApi.IsLocalPlayerVip()) return;
            texto += Tecla(L.T("aesirmsg.d_abastecer", AesirConfig.Aesir7AbastecerAreaRaio.Value.ToString("0")) + Aesir);
        }

        [HarmonyPatch(typeof(Smelter), "OnHoverAddFuel")]
        private static class CombustivelPatch
        {
            private static void Postfix(ref string __result)
            {
                try { Dica7(ref __result); }
                catch (Exception e) { AesirLog.Erro(7, e); }
            }
        }

        [HarmonyPatch(typeof(Smelter), "OnHoverAddOre")]
        private static class MinerioPatch
        {
            private static void Postfix(ref string __result)
            {
                try { Dica7(ref __result); }
                catch (Exception e) { AesirLog.Erro(7, e); }
            }
        }

        // ------------------------------------------------------------------ 14, 15, 16: comandos do bau

        /// <summary>As teclas do bau (AesirTeclas), no lugar dos comandos /depositar, /ordenar e /etiqueta.</summary>
        [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
        private static class BauPatch
        {
            private static void Postfix(ref string __result)
            {
                try
                {
                    if (string.IsNullOrEmpty(__result) || !VipList.VipListApi.IsLocalPlayerVip()) return;
                    if (AesirConfig.Aesir14DepositarAtivo?.Value == true)
                        __result += AesirTeclas.Dica(AesirTeclas.Depositar, L.T("aesirmsg.d_guardar") + Aesir);
                    if (AesirConfig.Aesir15OrdenarAtivo?.Value == true)
                        __result += AesirTeclas.Dica(AesirTeclas.Ordenar, L.T("aesirmsg.d_ordenar") + Aesir);
                    if (AesirConfig.Aesir16EtiquetaAtivo?.Value == true)
                        __result += AesirTeclas.Dica(AesirTeclas.Etiqueta, L.T("aesirmsg.d_nome") + Aesir);
                }
                catch (Exception e) { AesirLog.Erro(14, e); }
            }
        }
    }
}
