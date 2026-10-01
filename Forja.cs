using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Forja de Potencial do Valheim 1.0 (prefab UpgradeStation, CraftingStation.m_upgrader) e a
    /// Garantia de Refino, item do servidor vendido na Loja Deadcoins.
    ///
    /// No jogo, cada tentativa de refino gasta 1 idolo do tier do item e rola a chance do idolo:
    /// os 16 idolos vem com 65% de sucesso e m_breakChance 1.0, entao o resto (35%) DESTROI o item
    /// (o ramo "perde um nivel" do InventoryGui.DoCrafting nunca acontece). O jogo tambem nao tem
    /// teto de nivel na forja.
    ///
    /// Aqui: com garantias no inventario a tentativa usa ChanceComGarantia (100% por padrao) e gasta
    /// as garantias do nivel (GarantiasPorNivel) so se a tentativa aconteceu de fato. O idolo
    /// continua sendo exigido. NivelMaximo trava a forja para todos.
    ///
    /// O refino roda no cliente (como todo craft do Valheim): o gasto da garantia e conferido no
    /// cliente, igual ao do idolo. A compra e conferida pelo servidor (Loja Deadcoins).
    /// </summary>
    internal static class Forja
    {
        public const string GarantiaPrefab = "GarantiaRefino";
        public const string GarantiaNome = "Garantia de Refino";

        public static ConfigEntry<bool> GarantiaAtiva;
        public static ConfigEntry<float> ChanceComGarantia;
        public static ConfigEntry<string> GarantiasPorNivel;
        public static ConfigEntry<int> NivelMaximo;
        // Do jogador (Meus ajustes), nao sincronizada.
        public static ConfigEntry<bool> UsarGarantia;

        private static List<KeyValuePair<int, int>> _custos;

        public static void Bind(ConfigFile config)
        {
            const string section = "Forja de Potencial";
            GarantiaAtiva = Plugin.Synced(config.Bind(section, "GarantiaAtiva", true,
                "Liga a Garantia de Refino na Forja de Potencial. Desligada, a forja e a do jogo e ninguem gasta garantia."));
            ChanceComGarantia = Plugin.Synced(config.Bind(section, "ChanceComGarantia", 1f,
                new ConfigDescription("Chance de sucesso de uma tentativa com garantia. 1 = 100%. Abaixo de 1, a falha segue a regra do " +
                    "idolo (no jogo, o item quebra).", new AcceptableValueRange<float>(0f, 1f))));
            GarantiasPorNivel = Plugin.Synced(config.Bind(section, "GarantiasPorNivel", "1:1",
                "Quantas garantias cada tentativa gasta, pelo nivel que o item vai alcancar. Lista Nivel:Quantidade: vale a do maior " +
                "Nivel que nao passe do nivel alvo. Ex.: 1:1,15:2,21:3 = 1 garantia ate o nivel 14, 2 do 15 ao 20, 3 do 21 em diante. " +
                "Quantidade 0 = a garantia nao vale nesses niveis. Vazio = 1 para todos."));
            NivelMaximo = Plugin.Synced(config.Bind(section, "NivelMaximo", 25,
                new ConfigDescription("Nivel maximo que a Forja de Potencial alcanca, com ou sem garantia. 0 = sem teto (o jogo nao tem).",
                    new AcceptableValueRange<int>(0, 1000))));
            UsarGarantia = config.Bind(section, "UsarGarantia", true,
                "Usa a Garantia de Refino na Forja de Potencial quando voce tiver garantias no inventario. Desligue para arriscar " +
                "sem gastar (65% sobe, 35% quebra).");

            GarantiasPorNivel.SettingChanged += (_, __) => _custos = null;
        }

        // ------------------------------------------------------------------ regras

        /// <summary>Garantias que uma tentativa para o nivel alvo gasta (0 = a garantia nao vale nesse nivel).</summary>
        public static int GarantiasPara(int nivelAlvo)
        {
            List<KeyValuePair<int, int>> custos = _custos ??= LerCustos(GarantiasPorNivel?.Value);
            int quantidade = 1;
            foreach (KeyValuePair<int, int> custo in custos)
                if (custo.Key <= nivelAlvo) quantidade = custo.Value;
            return quantidade;
        }

        /// <summary>"1:1,15:2,21:3" em ordem de nivel. Entradas quebradas sao ignoradas com aviso.</summary>
        public static List<KeyValuePair<int, int>> LerCustos(string texto)
        {
            var custos = new List<KeyValuePair<int, int>>();
            if (string.IsNullOrWhiteSpace(texto)) return custos;
            foreach (string entrada in texto.Split(','))
            {
                if (string.IsNullOrWhiteSpace(entrada)) continue;
                string[] partes = entrada.Split(':');
                if (partes.Length != 2
                    || !int.TryParse(partes[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int nivel)
                    || !int.TryParse(partes[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantidade)
                    || quantidade < 0)
                {
                    Debug.LogWarning($"[Deadheim] GarantiasPorNivel: entrada ignorada '{entrada}'. Formato: Nivel:Quantidade");
                    continue;
                }
                custos.Add(new KeyValuePair<int, int>(nivel, quantidade));
            }
            custos.Sort((a, b) => a.Key.CompareTo(b.Key));
            return custos;
        }

        public static bool AcimaDoTeto(int nivelAlvo) => NivelMaximo.Value > 0 && nivelAlvo > NivelMaximo.Value;

        public static int GarantiasNoInventario(Player player)
            => player == null ? 0 : player.GetInventory().CountItems(GarantiaNome, -1, false);

        internal enum Uso
        {
            /// <summary>Forja do jogo: sem garantia (desligada, nao vale no nivel, ou o jogador nao tem).</summary>
            Nenhum,
            /// <summary>Desligada nos ajustes do jogador, mas ele tem garantias.</summary>
            DesligadaPeloJogador,
            /// <summary>Tem garantias, mas menos que o nivel pede: a tentativa e barrada.</summary>
            Faltam,
            Usa,
        }

        /// <summary>
        /// O que a proxima tentativa faz com as garantias. Com alguma garantia no inventario e menos
        /// do que o nivel pede, a tentativa e barrada: quem comprou garantia nao perde o item porque
        /// o preco do nivel mudou ou porque contou errado.
        /// </summary>
        internal static Uso Avaliar(Player player, int nivelAlvo, out int custo, out int tem)
        {
            custo = GarantiasPara(nivelAlvo);
            tem = GarantiasNoInventario(player);
            if (!GarantiaAtiva.Value || custo <= 0 || tem <= 0) return Uso.Nenhum;
            if (!UsarGarantia.Value) return Uso.DesligadaPeloJogador;
            return tem < custo ? Uso.Faltam : Uso.Usa;
        }

        /// <summary>O idolo da receita: o requisito que so a forja pede (m_upgraderResource).</summary>
        private static ItemDrop.ItemData.SharedData IdoloDa(Recipe recipe)
        {
            if (recipe?.m_resources == null) return null;
            foreach (Piece.Requirement requirement in recipe.m_resources)
                if (requirement.m_upgraderResource && requirement.m_resItem != null)
                    return requirement.m_resItem.m_itemData.m_shared;
            return null;
        }

        private static string Porcento(float chance) => Mathf.RoundToInt(Mathf.Clamp01(chance) * 100f) + "%";

        /// <summary>
        /// Chances do jogo para o idolo: o InventoryGui sorteia r em [0,1]; sobe se r &lt;= chance,
        /// quebra se r &gt;= 1 - breakChance, senao perde um nivel.
        /// </summary>
        private static string ChancesSemGarantia(ItemDrop.ItemData.SharedData idolo)
        {
            if (idolo == null) return "pode quebrar";
            float sobe = Mathf.Clamp01(idolo.m_upgradeChance);
            float quebra = 1f - Mathf.Max(sobe, 1f - Mathf.Clamp01(idolo.m_breakChance));
            float perde = 1f - sobe - quebra;
            string texto = $"{Porcento(sobe)} sobe, {Porcento(quebra)} quebra";
            if (perde > 0.005f) texto += $", {Porcento(perde)} perde nivel";
            return texto;
        }

        private static bool NaForja(Player player, out CraftingStation station)
        {
            station = player != null ? player.GetCurrentCraftingStation() : null;
            return station != null && station.m_upgrader;
        }

        // ---------------------------------------------------------------- patches

        private sealed class Tentativa
        {
            public ItemDrop.ItemData.SharedData Idolo;
            public float ChanceDoJogo;
            public ItemDrop.ItemData Item;
            public int Custo;
            public int NivelAlvo;
        }

        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static class DoCraftingPatch
        {
            private static bool Prefix(InventoryGui __instance, Player player, out Tentativa __state)
            {
                __state = null;
                ItemDrop.ItemData item = __instance.m_craftUpgradeItem;
                if (item == null || __instance.m_craftRecipe == null || !NaForja(player, out _)) return true;

                int alvo = item.m_quality + 1;
                if (AcimaDoTeto(alvo))
                {
                    player.Message(MessageHud.MessageType.Center, $"A Forja de Potencial não passa do nível {NivelMaximo.Value}.");
                    return false;
                }

                switch (Avaliar(player, alvo, out int custo, out int tem))
                {
                    case Uso.Faltam:
                        player.Message(MessageHud.MessageType.Center,
                            $"Faltam garantias: o nível {alvo} pede {custo}, você tem {tem}. Para arriscar sem, desligue UsarGarantia em ESC > Deadheim.");
                        return false;
                    case Uso.Usa:
                        ItemDrop.ItemData.SharedData idolo = IdoloDa(__instance.m_craftRecipe);
                        if (idolo == null) return true; // o jogo recusa sozinho (sem idolo na receita)
                        __state = new Tentativa { Idolo = idolo, ChanceDoJogo = idolo.m_upgradeChance, Item = item, Custo = custo, NivelAlvo = alvo };
                        // O DoCrafting le a chance do idolo na hora do sorteio; volta no Finalizer.
                        idolo.m_upgradeChance = ChanceComGarantia.Value;
                        return true;
                    default:
                        return true;
                }
            }

            private static void Finalizer(Player player, Tentativa __state)
            {
                if (__state == null) return;
                __state.Idolo.m_upgradeChance = __state.ChanceDoJogo;

                // Tentativa feita = o jogo tirou o item do inventario (no sucesso entra outro, na
                // quebra some). Se ele voltou antes (sem idolo, sem espaco), nada e gasto.
                Inventory inventory = player != null ? player.GetInventory() : null;
                if (inventory == null || inventory.ContainsItem(__state.Item)) return;

                inventory.RemoveItem(GarantiaNome, __state.Custo, -1, false);
                int restam = inventory.CountItems(GarantiaNome, -1, false);
                player.Message(MessageHud.MessageType.TopLeft,
                    $"{GarantiaNome}: {__state.Custo} usada{(__state.Custo > 1 ? "s" : "")} no nível {__state.NivelAlvo}. Restam {restam}.");
                Debug.Log($"[Deadheim] Forja: {player.GetPlayerName()} usou {__state.Custo} garantia(s) em {__state.Item.m_shared.m_name} " +
                    $"para o nivel {__state.NivelAlvo} (chance {__state.ChanceDoJogo:0.##} -> {ChanceComGarantia.Value:0.##}); restam {restam}.");
            }
        }

        /// <summary>Texto e botao do painel de receita na forja: teto, garantia e chances reais.</summary>
        [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
        private static class UpdateRecipePatch
        {
            private static void Postfix(InventoryGui __instance, Player player)
            {
                ItemDrop.ItemData item = __instance.m_selectedRecipe.ItemData;
                Recipe recipe = __instance.m_selectedRecipe.Recipe;
                if (item == null || recipe == null || !NaForja(player, out _) || __instance.m_itemCraftType == null) return;

                int alvo = item.m_quality + 1;
                string botao = null;
                if (AcimaDoTeto(alvo))
                {
                    __instance.m_itemCraftType.text = $"Nível máximo da Forja de Potencial: {NivelMaximo.Value}";
                    __instance.m_craftButton.interactable = false;
                    return;
                }

                ItemDrop.ItemData.SharedData idolo = IdoloDa(recipe);
                switch (Avaliar(player, alvo, out int custo, out int tem))
                {
                    case Uso.Usa:
                        __instance.m_itemCraftType.text = $"<color=#9be564>{GarantiaNome}: {Porcento(ChanceComGarantia.Value)} de sucesso</color> (usa {custo}, tem {tem})";
                        botao = "Refinar com garantia";
                        break;
                    case Uso.Faltam:
                        __instance.m_itemCraftType.text = $"<color=#f0a040>Faltam garantias: o nível {alvo} pede {custo}, você tem {tem}</color>";
                        __instance.m_craftButton.interactable = false;
                        break;
                    case Uso.DesligadaPeloJogador:
                        __instance.m_itemCraftType.text = $"Garantia desligada nos seus ajustes: {ChancesSemGarantia(idolo)}";
                        break;
                    default:
                        __instance.m_itemCraftType.text = $"Sem garantia: {ChancesSemGarantia(idolo)}";
                        break;
                }

                if (botao != null)
                {
                    TMP_Text texto = __instance.m_craftButton.GetComponentInChildren<TMP_Text>();
                    if (texto != null) texto.text = botao;
                }
            }
        }
    }
}
