using BepInEx.Configuration;
using HarmonyLib;

namespace Deadheim
{
    /// <summary>
    /// Desliga a marca "cheated" do Valheim nos itens. O jogo marca o que e fabricado ou construido
    /// quando qualquer pilha do material na bolsa esta marcada, e a marca se espalha pelo que o
    /// item mata, corta ou fabrica. Em 26/10 isso deu 414 avisos [CheatFlag] do AzuAntiCheat em
    /// jogadores sem mod nenhum fora do launcher; em 27/10 o mundo tinha 1984 objetos marcados,
    /// todos construidos ou atingidos por jogadores comuns.
    ///
    /// So o item: Inventory.Changed apaga m_cheated de tudo que esta na bolsa ou no bau, item novo
    /// nasce sem marca e as checagens do inventario nunca acham item marcado. A marca na ZDO fica
    /// como no vanilla: criatura atingida por quem esta em voo, god ou ghost continua marcada, e o
    /// Guard Cheated Objects do AzuAntiCheat ainda avisa disso. Arma marcada deixa de marcar
    /// criatura, arvore ou pedra, e a marca que ja esta no mundo nao passa para o item.
    /// Precisa rodar no servidor e nos clientes (o Character.Damage roda no dono da criatura).
    /// </summary>
    public static class SemMarcaTrapaca
    {
        public static ConfigEntry<bool> Ligado;

        public static void Bind(ConfigFile config)
        {
            Ligado = Plugin.Synced(config.Bind("Server config", "SemMarcaTrapaca", true,
                new ConfigDescription("Ignora e apaga a marca 'cheated' do Valheim nos itens. A marca de voo, god e ghost em criatura continua.")));
        }

        private static bool On => Ligado == null || Ligado.Value;

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
