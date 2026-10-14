using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// O que todo personagem recebe ao chegar, doador ou nao: uma vez, os itens de
    /// [Server config] TokensIniciais (padrao: 1 Portal Token e 1 Territory Token), para o primeiro
    /// portal e a primeira Ward de Territorio nao dependerem de doacao.
    ///
    /// A marca de "ja recebeu" fica no proprio personagem (m_customData), que o ServerCharacters
    /// guarda no servidor: morrer, relogar ou jogar de outro PC nao entrega de novo. O inventario e
    /// do cliente, como em toda entrega do Deadheim (Entrega.ParaJogador): o que nao couber cai aos pes.
    /// </summary>
    public static class TokensIniciais
    {
        private const string Marca = "dh_tokensIniciais";

        public static ConfigEntry<string> Itens;

        public static void Bind(ConfigFile config)
        {
            Itens = Plugin.Synced(config.Bind("Server config", "TokensIniciais", "PortalToken:1,TerritoryToken:1",
                new ConfigDescription("Itens que cada personagem recebe uma unica vez ao entrar no servidor, no formato " +
                                      "Item:Quantidade,Item:Quantidade. Vazio = nada. Quem ja recebeu nao recebe de novo, " +
                                      "mesmo que a lista mude.")));
        }

        private static void Entregar(Player player)
        {
            if (player == null || player != Player.m_localPlayer || Itens == null || ObjectDB.instance == null) return;
            if (player.m_customData.ContainsKey(Marca)) return;

            List<string> recebidos = new List<string>();
            foreach (string entrada in (Itens.Value ?? string.Empty).Split(','))
            {
                string[] partes = entrada.Split(':');
                if (partes.Length != 2 || !int.TryParse(partes[1].Trim(), out int quantia) || quantia <= 0) continue;
                GameObject prefab = ObjectDB.instance.GetItemPrefab(partes[0].Trim());
                ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (item == null)
                {
                    Debug.LogWarning("[Deadheim] TokensIniciais: item desconhecido '" + partes[0].Trim() + "'.");
                    continue;
                }
                Entrega.ParaJogador(player, prefab, quantia);
                string nome = Localization.instance != null
                    ? Localization.instance.Localize(item.m_itemData.m_shared.m_name)
                    : prefab.name;
                recebidos.Add(quantia + " " + nome);
            }

            player.m_customData[Marca] = "1";
            if (recebidos.Count == 0) return;
            player.Message(MessageHud.MessageType.Center, "Boas-vindas ao Deadheim! Voce recebeu " + string.Join(" e ", recebidos) + ".");
            Debug.Log("[Deadheim] TokensIniciais entregues a " + player.GetPlayerName() + ": " + string.Join(", ", recebidos) + ".");
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class NoSpawn
        {
            // Depois das moedas fixas e de quem mexe em itens: o inventario ja esta arrumado.
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Player __instance) => Entregar(__instance);
        }
    }
}
