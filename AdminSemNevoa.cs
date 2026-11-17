using Deadheim.Vanilla;
using HarmonyLib;
using System;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Comando de admin (console F5): semnevoa [on|off] tira a nevoa (ParticleMist) das Mistlands,
    /// so no cliente do admin. Sem argumento alterna. Nao persiste entre sessoes.
    /// </summary>
    [HarmonyPatch]
    internal static class AdminSemNevoa
    {
        private static bool _ligado;

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class ComandosPatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("semnevoa", "[on|off] - (admin) remove a nevoa das Mistlands so para voce",
                    args =>
                    {
                        try
                        {
                            if (!Admin.LocalPlayerIsAdmin()) { args.Context.AddString("So admin."); return; }
                            bool novo = args.Length < 2 ? !_ligado : args[1].ToLowerInvariant() != "off";
                            _ligado = novo;
                            if (novo)
                            {
                                // m_ps e privado em ParticleMist (conferido no assembly_valheim.dll).
                                var ps = ParticleMist.instance == null ? null
                                    : AccessTools.Field(typeof(ParticleMist), "m_ps")?.GetValue(ParticleMist.instance) as ParticleSystem;
                                if (ps != null) ps.Clear();
                                foreach (var em in UnityEngine.Object.FindObjectsOfType<MistEmitter>())
                                    if (em != null && em.m_psystem != null) em.m_psystem.Clear();
                            }
                            args.Context.AddString("Nevoa das Mistlands: " + (novo ? "removida" : "normal"));
                        }
                        catch (Exception e) { args.Context.AddString("semnevoa falhou: " + e.Message); }
                    });
            }
        }

        // Update e quem emite as particulas; parado, a nevoa some e nao volta.
        [HarmonyPatch(typeof(ParticleMist), "Update")]
        private static class SemEmissao
        {
            private static bool Prefix() => !(_ligado && Admin.LocalPlayerIsAdmin());
        }

        // MistEmitter (particulas de ambiente do EnvMan, nao do ParticleMist) tambem para de emitir.
        [HarmonyPatch(typeof(MistEmitter), "Update")]
        private static class SemEmissaoAmbiente
        {
            private static bool Prefix() => !(_ligado && Admin.LocalPlayerIsAdmin());
        }
    }
}
