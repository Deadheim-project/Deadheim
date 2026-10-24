using Deadheim.Vanilla;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Moedas (Coins) do jogador: uma pilha so, sempre no primeiro slot da segunda linha do inventario (0,1, logo abaixo da hotbar), sem
    /// peso e sem limite pratico. Nao saem do jogador por vontade dele: nao se movem, nao se
    /// dividem, nao vao para bau, chao nem tumba, e na morte ficam com ele (so a parte do saque PvP,
    /// PvpCoinDropPercent / PkCoinDropPercent, cai no chao). Quem tira e poe moedas
    /// sao os sistemas do jogo e dos mods (mercador, craft, NPCs, bounty), pelo RemoveItem/AddItem
    /// do inventario. O slot fica reservado mesmo sem moedas (mostra a moeda apagada com 0): nenhum outro item entra nele.
    /// Tudo so vale para o inventario do jogador local; baus, tumbas e o servidor ficam vanilla.
    /// </summary>
    internal static class Moedas
    {
        public const string Prefab = "Coins";
        // Pilha que nunca enche. Fica abaixo de int.MaxValue / slots porque o CanAddItem vanilla
        // multiplica a pilha maxima pelos slots vazios.
        public const int PilhaMaxima = 100_000_000;
        private const string AvisoFixas = "As moedas ficam sempre com voce.";

        private static readonly Vector2i Slot = new Vector2i(0, 1);
        private static string _nome;
        // O save carrega cada item na posicao gravada: as regras so valem depois, no OnSpawned.
        private static bool _carregando;

        public static void Init()
        {
            Prefabs.PiecesReady += AplicarNoPrefab;
        }

        /// <summary>Sem peso e pilha enorme. O m_shared do prefab e o mesmo de toda moeda do mundo.</summary>
        public static void AplicarNoPrefab()
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(Prefab) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null || drop.m_itemData?.m_shared == null) return;
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            _nome = shared.m_name;
            shared.m_weight = 0f;
            shared.m_maxStackSize = PilhaMaxima;
        }

        public static bool EhMoeda(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null) return false;
            if (_nome != null) return item.m_shared.m_name == _nome;
            return item.m_dropPrefab != null && item.m_dropPrefab.name == Prefab;
        }

        private static Inventory InventarioDoJogador =>
            Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;

        private static bool Ativo(Inventory inventory) =>
            !_carregando && inventory != null && inventory == InventarioDoJogador;

        private static bool NoSlot(int x, int y) => x == Slot.x && y == Slot.y;

        /// <summary>
        /// Junta todas as pilhas de moeda em uma so, no slot. O que estava no slot troca de lugar
        /// com a pilha (troca sempre cabe). Retorna a pilha, ou null sem moedas.
        /// </summary>
        public static ItemDrop.ItemData Organizar(Inventory inventory)
        {
            List<ItemDrop.ItemData> itens = inventory.m_inventory;
            ItemDrop.ItemData pilha = null;
            long total = 0;
            foreach (ItemDrop.ItemData item in itens)
            {
                if (!EhMoeda(item)) continue;
                total += item.m_stack;
                if (pilha == null || NoSlot(item.m_gridPos.x, item.m_gridPos.y)) pilha = item;
            }
            if (pilha == null) return null;

            bool mudou = false;
            if (itens.RemoveAll(i => i != pilha && EhMoeda(i)) > 0) mudou = true;
            int soma = (int)System.Math.Min(total, PilhaMaxima);
            if (pilha.m_stack != soma)
            {
                pilha.m_stack = soma;
                mudou = true;
            }
            if (!NoSlot(pilha.m_gridPos.x, pilha.m_gridPos.y))
            {
                ItemDrop.ItemData ocupante = inventory.GetItemAt(Slot.x, Slot.y);
                if (ocupante != null) ocupante.m_gridPos = pilha.m_gridPos;
                pilha.m_gridPos = Slot;
                mudou = true;
            }
            if (mudou) inventory.Changed();
            return pilha;
        }

        /// <summary>
        /// Poe <paramref name="quantia"/> moedas de <paramref name="item"/> na pilha do slot. Sem
        /// pilha, o item vira a pilha (ou um clone, se for so parte dele) e o que ocupava o slot vai
        /// para um slot vazio; sem slot vazio, nao cabe.
        /// </summary>
        private static bool Guardar(Inventory inventory, ItemDrop.ItemData item, int quantia, bool clonar)
        {
            quantia = Mathf.Min(quantia, item.m_stack);
            if (quantia <= 0) return true;

            ItemDrop.ItemData pilha = Organizar(inventory);
            if (pilha != null)
            {
                pilha.m_stack = (int)System.Math.Min((long)pilha.m_stack + quantia, PilhaMaxima);
            }
            else
            {
                ItemDrop.ItemData ocupante = inventory.GetItemAt(Slot.x, Slot.y);
                if (ocupante != null)
                {
                    Vector2i livre = inventory.FindEmptySlot(true);
                    if (livre.x < 0) return false;
                    ocupante.m_gridPos = livre;
                }
                ItemDrop.ItemData nova = clonar ? item.Clone() : item;
                nova.m_stack = quantia;
                nova.m_gridPos = Slot;
                inventory.m_inventory.Add(nova);
                if (!clonar) return Mudou(inventory);
            }
            if (clonar) item.m_stack -= quantia;
            return Mudou(inventory);
        }

        private static bool Mudou(Inventory inventory)
        {
            inventory.Changed(true);
            return true;
        }

        private static List<ItemDrop.ItemData> Retirar(Inventory inventory)
        {
            List<ItemDrop.ItemData> moedas = inventory.m_inventory.FindAll(EhMoeda);
            if (moedas.Count > 0) inventory.m_inventory.RemoveAll(EhMoeda);
            return moedas;
        }

        private static void Devolver(Inventory inventory, List<ItemDrop.ItemData> moedas)
        {
            if (moedas == null || moedas.Count == 0) return;
            inventory.m_inventory.AddRange(moedas);
            inventory.Changed();
        }

        // ------------------------------------------------------------------ carga e spawn

        [HarmonyPatch]
        private static class CarregarPatch
        {
            private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.Load), new[] { typeof(ZPackage) });
                yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.Load), new[] { typeof(ZPackage), typeof(bool) });
            }

            private static void Prefix() => _carregando = true;
            private static void Finalizer() => _carregando = false;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class SpawnPatch
        {
            // Depois de mods que mexem em itens (ex.: ItemConfig), para a pilha e o peso valerem.
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                AplicarNoPrefab();
                Organizar(__instance.GetInventory());
            }
        }

        // ------------------------------------------------------------------ entrada de itens

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData))]
        private static class AddItemPatch
        {
            private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (!Ativo(__instance) || !EhMoeda(item)) return true;
                // O vanilla nao mexe no m_stack do item que entrou numa pilha: quem chamou destroi ou remove.
                __result = Guardar(__instance, item, item.m_stack, false);
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData), typeof(Vector2i))]
        private static class AddItemPosicaoPatch
        {
            private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, Vector2i pos, ref bool __result)
            {
                if (!Ativo(__instance)) return true;
                if (EhMoeda(item)) __result = Guardar(__instance, item, item.m_stack, false);
                else if (NoSlot(pos.x, pos.y)) __result = __instance.AddItem(item);
                else return true;
                return false;
            }
        }

        /// <summary>Arrastar, trocar e MoveAll: moeda vai sempre para a pilha; nada mais entra no slot.</summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem),
            typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool))]
        private static class AddItemGradePatch
        {
            private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, int amount, int x, int y, ref bool __result)
            {
                if (!Ativo(__instance)) return true;
                if (EhMoeda(item))
                {
                    __result = Guardar(__instance, item, amount, true);
                    return false;
                }
                if (!NoSlot(x, y)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.FindEmptySlot))]
        private static class SlotVazioPatch
        {
            private static void Postfix(Inventory __instance, bool topFirst, ref Vector2i __result)
            {
                if (!Ativo(__instance) || !NoSlot(__result.x, __result.y)) return;
                int w = __instance.GetWidth(), h = __instance.GetHeight();
                __result = new Vector2i(-1, -1);
                for (int n = 0; n < h; n++)
                {
                    int y = topFirst ? n : h - 1 - n;
                    for (int x = 0; x < w; x++)
                    {
                        if (NoSlot(x, y) || __instance.GetItemAt(x, y) != null) continue;
                        __result = new Vector2i(x, y);
                        return;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetEmptySlots))]
        private static class ContarVaziosPatch
        {
            private static void Postfix(Inventory __instance, ref int __result)
            {
                if (Ativo(__instance) && __instance.GetItemAt(Slot.x, Slot.y) == null)
                    __result = Mathf.Max(0, __result - 1);
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveEmptySlot))]
        private static class TemVazioPatch
        {
            private static void Postfix(Inventory __instance, ref bool __result)
            {
                if (__result && Ativo(__instance) && __instance.GetItemAt(Slot.x, Slot.y) == null)
                    __result = __instance.GetEmptySlots() > 0;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), typeof(ItemDrop.ItemData), typeof(int))]
        private static class CabePatch
        {
            private static void Postfix(Inventory __instance, ItemDrop.ItemData item, int stack, ref bool __result)
            {
                if (!Ativo(__instance) || item?.m_shared == null) return;
                if (EhMoeda(item))
                {
                    __result = __instance.GetItemAt(Slot.x, Slot.y) == null
                               || EhMoeda(__instance.GetItemAt(Slot.x, Slot.y))
                               || __instance.HaveEmptySlot();
                    return;
                }
                if (stack <= 0) stack = item.m_stack;
                long livre = __instance.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel)
                             + (long)__instance.GetEmptySlots() * item.m_shared.m_maxStackSize;
                __result = livre >= stack;
            }
        }

        // ------------------------------------------------------------------ saida de itens

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveInventoryToGrave))]
        private static class TumbaPatch
        {
            private static void Prefix(Inventory original, out List<ItemDrop.ItemData> __state)
                => __state = Ativo(original) ? Retirar(original) : null;

            private static void Postfix(Inventory original, List<ItemDrop.ItemData> __state)
                => Devolver(original, __state);
        }

        /// <summary>Modificadores de mundo que apagam o inventario na morte.</summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveUnequipped))]
        private static class ApagarNaMortePatch
        {
            private static void Prefix(Inventory __instance, out List<ItemDrop.ItemData> __state)
                => __state = Ativo(__instance) ? Retirar(__instance) : null;

            private static void Postfix(Inventory __instance, List<ItemDrop.ItemData> __state)
                => Devolver(__instance, __state);
        }

        /// <summary>Pegar tudo / guardar pilhas do jogador para um bau: as moedas ficam.</summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveAll))]
        private static class MoverTudoPatch
        {
            private static void Prefix(Inventory __instance, Inventory fromInventory, out List<ItemDrop.ItemData> __state)
                => __state = __instance != fromInventory && Ativo(fromInventory) ? Retirar(fromInventory) : null;

            private static void Postfix(Inventory fromInventory, List<ItemDrop.ItemData> __state)
                => Devolver(fromInventory, __state);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
        private static class EmpilharPatch
        {
            private static void Prefix(Inventory __instance, Inventory fromInventory, out List<ItemDrop.ItemData> __state)
                => __state = __instance != fromInventory && Ativo(fromInventory) ? Retirar(fromInventory) : null;

            private static void Postfix(Inventory fromInventory, List<ItemDrop.ItemData> __state)
                => Devolver(fromInventory, __state);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData))]
        private static class MoverItemPatch
        {
            private static bool Prefix(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item)
                => !(EhMoeda(item) && __instance != fromInventory && Ativo(fromInventory));
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis),
            typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int))]
        private static class MoverItemGradePatch
        {
            private static bool Prefix(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item, ref bool __result)
            {
                if (!(EhMoeda(item) && __instance != fromInventory && Ativo(fromInventory))) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem))]
        private static class ChaoPatch
        {
            private static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, ref bool __result)
            {
                if (__instance != Player.m_localPlayer || !EhMoeda(item)) return true;
                if (!Ativo(inventory ?? __instance.GetInventory())) return true;
                __instance.Message(MessageHud.MessageType.Center, AvisoFixas);
                __result = false;
                return false;
            }
        }

        /// <summary>Clique direito: moeda nao e usavel, e o aviso vanilla so atrapalha.</summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        private static class UsarPatch
        {
            private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
                => !(__instance == Player.m_localPlayer && EhMoeda(item));
        }

        // ------------------------------------------------------------------ interface

        /// <summary>
        /// Clique, arrasto, divisao, ctrl/shift-clique e gamepad passam todos por aqui. A pilha do
        /// slot nao e pega; nada e solto no slot; moeda arrastada de um bau vai direto para a pilha.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
        private static class GradePatch
        {
            private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos)
            {
                Inventory jogador = InventarioDoJogador;
                if (jogador == null || grid == null) return true;
                Inventory alvo = grid.GetInventory();

                if (__instance.m_dragGo == null)
                    return !(alvo == jogador && EhMoeda(item));

                ItemDrop.ItemData arrastado = __instance.m_dragItem;
                if (EhMoeda(arrastado))
                {
                    if (__instance.m_dragInventory == jogador)
                    {
                        __instance.SetupDragItem(null, null, 1);
                        return false;
                    }
                    if (alvo != jogador) return true;
                    if (__instance.m_dragInventory.ContainsItem(arrastado))
                        jogador.MoveItemToThis(__instance.m_dragInventory, arrastado, __instance.m_dragAmount, Slot.x, Slot.y);
                    __instance.SetupDragItem(null, null, 1);
                    return false;
                }
                if (alvo == jogador && NoSlot(pos.x, pos.y)) return false;
                // Item antigo que ficou no slot (save sem moedas): soltar em cima de outro faria o
                // vanilla trocar, mandando o outro para o slot -- recusado, e o arrastado se perderia.
                if (__instance.m_dragInventory == jogador && NoSlot(arrastado.m_gridPos.x, arrastado.m_gridPos.y))
                {
                    ItemDrop.ItemData ali = alvo.GetItemAt(pos.x, pos.y);
                    if (ali != null && ali != arrastado) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// A pilha maxima e so um teto tecnico: mostra so quantas moedas tem. Sem moedas, o slot do
        /// jogador mostra a moeda apagada com 0, para ficar claro que e o lugar delas.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
        private static class TextoGradePatch
        {
            private static readonly Color Apagada = new Color(1f, 1f, 1f, 0.35f);

            private static void Postfix(InventoryGrid __instance)
            {
                Inventory inventory = __instance.GetInventory();
                if (inventory == null) return;
                int largura = inventory.GetWidth();
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    if (!EhMoeda(item)) continue;
                    InventoryElement element = __instance.GetElement(item.m_gridPos.x, item.m_gridPos.y, largura);
                    if (element != null) element.m_amount.text = item.m_stack.ToString();
                }

                if (!Ativo(inventory) || inventory.GetItemAt(Slot.x, Slot.y) != null) return;
                InventoryElement vazio = __instance.GetElement(Slot.x, Slot.y, largura);
                Sprite icone = Icone();
                if (vazio == null || icone == null) return;
                vazio.m_icon.enabled = true;
                vazio.m_icon.sprite = icone;
                vazio.m_icon.color = Apagada;
                vazio.m_amount.enabled = true;
                vazio.m_amount.text = "0";
            }

            private static Sprite Icone()
            {
                GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(Prefab) : null;
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                return drop != null ? drop.m_itemData.GetIcon() : null;
            }
        }
    }
}
