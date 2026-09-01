using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RaidSystem
{
    /// <summary>
    /// <c>ShieldDomeImageEffect.Awake()</c> constrói um <see cref="Material"/> a partir
    /// de um shader que não existe num servidor dedicado (sem pipeline de render), e
    /// estoura <c>ArgumentNullException: Parameter name: shader</c> em todo boot —
    /// dispara no <c>Instantiate</c> do prefab da ward antes de qualquer remoção do
    /// componente conseguir rodar.
    ///
    /// O componente é de um mod de terceiro (não temos a fonte para guardar o Awake
    /// dele), então anulamos o método via Harmony, e só quando headless: <see cref="Prepare"/>
    /// garante que no cliente o patch nem é registrado. Nenhum efeito visual muda para
    /// quem joga.
    /// </summary>
    [HarmonyPatch]
    internal static class ShieldDomeHeadlessGuard
    {
        private static bool Prepare()
        {
            if (!Application.isBatchMode) return false;
            var type = AccessTools.TypeByName("ShieldDomeImageEffect");
            return type != null && AccessTools.Method(type, "Awake") != null;
        }

        private static MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.TypeByName("ShieldDomeImageEffect"), "Awake");

        private static bool Prefix() => false;
    }
}
