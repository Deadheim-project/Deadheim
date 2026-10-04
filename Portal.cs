using HarmonyLib;
using Deadheim.Vanilla;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    [HarmonyPatch]
    class Portal
    {
        [HarmonyPatch(typeof(Player), "PlacePiece")]
        public static class NoBuild_Patch
        {
            public static void UpdatePortalMaterials()
            {
                GameObject portalwood = Prefabs.Get("portal_wood");
                if (portalwood == null) return;

                var portalwoodPiece = portalwood.GetComponent<Piece>();

                // Lista temporária para guardar os materiais que formos encontrando
                List<Piece.Requirement> newRequirements = new List<Piece.Requirement>();

                // Quebra a string "Item:1,Item:2" em pedaços separados pela vírgula
                string[] matEntries = Plugin.PortalMaterials.Value.Split(',');

                foreach (string entry in matEntries)
                {
                    // Quebra cada pedaço pelo ":" para separar o Nome da Quantidade
                    string[] parts = entry.Split(':');

                    if (parts.Length == 2)
                    {
                        // Tenta converter a quantidade para número
                        if (int.TryParse(parts[1].Trim(), out int amount) && amount > 0)
                        {
                            // Token antigo no cfg ("PortalToken:1") vale como Dead Token.
                            string prefabName = DeadToken.Custo(parts[0].Trim(), ref amount);

                            // Busca o prefab no jogo
                            GameObject prefab = Prefabs.Get(prefabName);
                            if (prefab != null)
                            {
                                ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
                                if (itemDrop != null)
                                {
                                    newRequirements.Add(new Piece.Requirement
                                    {
                                        m_resItem = itemDrop,
                                        m_amount = amount,
                                        m_recover = true // Permite recuperar ao quebrar
                                    });
                                }
                                else
                                {
                                    Debug.LogWarning($"[Deadheim] O prefab '{prefabName}' não é um item válido.");
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"[Deadheim] Prefab '{prefabName}' não encontrado no jogo.");
                            }
                        }
                    }
                }

                // Se encontrou pelo menos 1 material válido, aplica no portal
                if (newRequirements.Count > 0)
                {
                    portalwoodPiece.m_resources = newRequirements.ToArray();
                    Debug.Log("[Deadheim] Materiais do portal atualizados dinamicamente!");
                }
                else
                {
                    Debug.LogWarning("[Deadheim] Nenhum material válido na config. Mantendo os materiais originais.");
                }
            }

            private static int GetPortalCount()
            {
                if (Admin.LocalPlayerIsAdmin()) return 0;

                ZPackage pkg = new();
                pkg.Write(Player.m_localPlayer.GetPlayerID());
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "DeadheimPortalAndTotemCountServer", pkg);

                return Plugin.PlayerPortalCount;
            }
        }
    }
}