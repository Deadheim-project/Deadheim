using BepInEx.Configuration;
using Deadheim.Pvp;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Fogo infinito (27/10/2026): fogueira, tocha, braseiro e lareira (tudo que e Fireplace) dentro de um
    /// territorio com a regra "fogo" ligada nao gasta lenha e fica sempre cheia. O admin liga na janela
    /// /territorio ou com: territorio fogo &lt;nome&gt; on.
    /// Opcional: tambem nas zonas seguras (ilha inicial, SafeZones e territorios seguros).
    /// </summary>
    public static class FogoInfinito
    {
        internal const string Secao = "Fogo Infinito";

        public static ConfigEntry<bool> NasZonasSeguras;

        // Fireplace.m_nview e privado no jogo: por reflexao (campo privado lido direto ja deu
        // FieldAccessException em runtime).
        private static readonly AccessTools.FieldRef<Fireplace, ZNetView> NView =
            AccessTools.FieldRefAccess<Fireplace, ZNetView>("m_nview");

        // Os que este mod ligou, para desligar quando a regra sai (o prefab pode ja vir infinito).
        private static readonly HashSet<int> _ligadosAqui = new HashSet<int>();

        public static void Bind(ConfigFile config)
        {
            NasZonasSeguras = Plugin.Synced(config.Bind(Secao, "NasZonasSeguras", false,
                "Liga: o fogo tambem e infinito nas zonas seguras (ilha inicial, SafeZones do PvP e territorios seguros). " +
                "Nos outros lugares, vale a regra 'fogo' de cada territorio (/territorio)."));
        }

        public static bool Cobre(Vector3 ponto)
        {
            Territorios.Territorio t = Territorios.Em(ponto);
            if (t != null && t.Fogo) return true;
            return NasZonasSeguras != null && NasZonasSeguras.Value && PvpZones.IsSafeArea(ponto);
        }

        [HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
        private static class AbastecerPatch
        {
            private static void Prefix(Fireplace __instance)
            {
                try
                {
                    int id = __instance.GetInstanceID();
                    if (!Cobre(__instance.transform.position))
                    {
                        if (_ligadosAqui.Remove(id)) __instance.m_infiniteFuel = false;
                        return;
                    }
                    if (!__instance.m_infiniteFuel)
                    {
                        __instance.m_infiniteFuel = true;
                        _ligadosAqui.Add(id);
                    }

                    // Cheia: o fogo aceso fica aceso e uma tocha posta vazia acende.
                    ZNetView nview = NView(__instance);
                    if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
                    ZDO zdo = nview.GetZDO();
                    if (zdo.GetFloat(ZDOVars.s_fuel) < __instance.m_maxFuel) zdo.Set(ZDOVars.s_fuel, __instance.m_maxFuel);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Deadheim] Fogo infinito: " + e.Message);
                }
            }
        }
    }
}
