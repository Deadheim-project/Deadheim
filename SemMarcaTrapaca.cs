using BepInEx.Configuration;
using HarmonyLib;

namespace Deadheim
{
    /// <summary>
    /// Desliga a marca "cheated" do Valheim. O jogo marca item feito em estacao posta sem custo
    /// (devcommands/Infinity Hammer dos admins) ou com material marcado, e a marca se espalha
    /// pelo que o item mata, corta ou fabrica. Em 26/10 isso deu 414 avisos [CheatFlag] do
    /// AzuAntiCheat em jogadores sem mod nenhum fora do launcher.
    ///
    /// Na ZDO (criatura, estrutura, estacao): o Set da marca vira false e o GetBool dela sempre
    /// le false -- a marca que ja esta gravada no mundo deixa de valer, inclusive para o
    /// AzuAntiCheat, que le pelo mesmo GetBool. No item: Inventory.Changed apaga m_cheated de
    /// tudo que ja esta na bolsa ou no bau, e as checagens do inventario nunca acham item marcado.
    /// Precisa rodar no servidor e nos clientes (o Character.Damage roda no dono da criatura).
    /// </summary>
    public static class SemMarcaTrapaca
    {
        public static ConfigEntry<bool> Ligado;

        public static void Bind(ConfigFile config)
        {
            Ligado = Plugin.Synced(config.Bind("Server config", "SemMarcaTrapaca", true,
                new ConfigDescription("Ignora e apaga a marca 'cheated' do Valheim em itens, estruturas e criaturas.")));
        }

        private static bool On => Ligado == null || Ligado.Value;

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.Set), typeof(int), typeof(bool))]
        private static class ZdoSet
        {
            private static void Prefix(int hash, ref bool value)
            {
                if (value && hash == ZDOVars.s_cheated && On) value = false;
            }
        }

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.GetBool), typeof(int), typeof(bool))]
        private static class ZdoGetBool
        {
            private static void Postfix(int hash, ref bool __result)
            {
                if (__result && hash == ZDOVars.s_cheated && On) __result = false;
            }
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.OnCreateNew), typeof(ItemDrop), typeof(bool))]
        private static class ItemNovo
        {
            private static void Prefix(ref bool cheated)
            {
                if (On) cheated = false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Changed))]
        private static class LimparBolsa
        {
            private static void Prefix(Inventory __instance, ref bool cheatedStateChanged)
            {
                if (!On) return;
                cheatedStateChanged = false;
                foreach (var item in __instance.GetAllItems())
                    item.m_cheated = false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CheatedDamagingItemEquipped))]
        private static class ArmaMarcada
        {
            private static void Postfix(ref bool __result)
            {
                if (On) __result = false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AnyCheatedItem))]
        private static class AlgumItemMarcado
        {
            private static void Postfix(ref bool __result)
            {
                if (On) __result = false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.ItemCheated), typeof(Piece.Requirement[]), typeof(int), typeof(bool))]
        private static class MaterialMarcado
        {
            private static void Postfix(ref bool __result)
            {
                if (On) __result = false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.ItemCheated), typeof(string), typeof(int), typeof(bool))]
        private static class MaterialMarcadoPorNome
        {
            private static void Postfix(ref bool __result)
            {
                if (On) __result = false;
            }
        }
    }
}
