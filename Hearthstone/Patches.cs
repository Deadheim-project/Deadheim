using HarmonyLib;
using System;
using UnityEngine;

namespace Hearthstone
    {
        class Patches
        {
            [HarmonyPatch(typeof(Player), "ConsumeItem")]
            public static class ConsumePatch
            {
                // Adicionamos 'ref bool __result' para dizer ao jogo base se o consumo deu certo.
                // __runOriginal: no HarmonyX um prefixo que devolve false NAO impede os seguintes de
                // rodar, so faz um E nesse valor. O Deadheim (prioridade First) recusa a pedra em
                // combate ou cacado; sem olhar isto a pedra sumia e o teleporte saia mesmo assim.
                private static bool Prefix(Player __instance, ref bool __result, ItemDrop.ItemData item, bool __runOriginal)
                {
                    if (!__runOriginal) return false;

                    // Usando IndexOf ignorando maiúsculas para não ter erro de digitação no nome do item
                    if (item?.m_shared?.m_name != null && item.m_shared.m_name.IndexOf("hearthstone", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!__instance.IsTeleportable(allowAllItems: false) && !Hearthstone.allowTeleportWithoutRestriction.Value)
                        {
                            __instance.Message(MessageHud.MessageType.Center, "You can't teleport carrying those items");
                            __result = false; // Diz pro jogo que falhou em consumir
                            return false;     // Pula o método original
                        }

                        Vector3 teleportPosition = Hearthstone.GetHearthStonePosition();

                        if (teleportPosition == Vector3.zero)
                        {
                            __instance.Message(MessageHud.MessageType.Center, "You need to set hearthstone spawn point");
                            __result = false;
                            return false;
                        }

                        // 1. Teleporta o jogador. TeleportTo devolve false se ja estiver teleportando,
                        //    na recarga do vanilla, ou se outro mod recusar (Deadheim: combate, cacado).
                        if (!__instance.TeleportTo(teleportPosition, __instance.transform.rotation, true))
                        {
                            __result = false;
                            return false;
                        }

                        // 2. So agora a pedra sai do inventario: teleporte recusado nao gasta a pedra
                        __instance.GetInventory().RemoveItem(item, 1);

                        // 3. Força o jogo a entender que o item foi consumido com sucesso
                        __result = true;
                        return false; // Cancela o método original do jogo para evitar bugs
                    }

                    // Se for uma comida normal, deixa o jogo rodar o código padrão dele
                    return true;
                }
            }

            [HarmonyPatch(typeof(Bed), "GetHoverText")]
            static class Bed_GetHoverText_Patch
            {
                static void Postfix(Bed __instance, ref string __result, ZNetView ___m_nview)
                {
                    // 1. CORREÇÃO DO ERRO: Ignora se a cama for um holograma de construção ou sem rede
                    if (___m_nview == null || !___m_nview.IsValid())
                        return;

                    if (Player.m_localPlayer == null)
                        return;

                    // Lê o dono usando o ZDOVars (mais seguro e atualizado)
                    long ownerId = ___m_nview.GetZDO().GetLong(ZDOVars.s_owner, 0L);

                    // 2. Adiciona o texto de P se for a cama do jogador
                    if (ownerId == Player.m_localPlayer.GetPlayerID() || Traverse.Create(__instance).Method("IsCurrent").GetValue<bool>())
                    {
                        __result += Localization.instance.Localize($"\n[<color=yellow><b>P</b></color>] Set hearthstone");
                    }
                }
            }
        }
    }

