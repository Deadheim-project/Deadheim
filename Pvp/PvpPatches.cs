using Deadheim.Vanilla;
using HarmonyLib;
using System;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>Todos os ganchos do modulo de PvP no jogo. Um patch por metodo.</summary>
    [HarmonyPatch]
    internal static class PvpPatches
    {
        private static float _nextRefusalMessage;

        private static void Refuse(Player player, string text)
        {
            if (player == null || string.IsNullOrEmpty(text) || Time.time < _nextRefusalMessage) return;
            _nextRefusalMessage = Time.time + 2f;
            player.Message(MessageHud.MessageType.Center, text);
        }

        // ------------------------------------------------------------------- dano

        // Golpe de jogador em jogador sendo processado agora no RPC_Damage da vitima.
        private static HitData _pvpHit;
        private static float _pvpHitMultiplier = 1f;
        private static bool _pvpHitScaled;

        /// <summary>
        /// Vitima: roda no dono da ZDO dela, que e o proprio cliente do jogador atingido.
        /// E a decisao que vale. Prefix antes do vanilla porque o vanilla ja cambaleia o
        /// alvo antes de olhar o PvP.
        /// </summary>
        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        private static class VictimDamagePatch
        {
            [HarmonyPriority(Priority.High)]
            private static bool Prefix(Character __instance, HitData hit)
            {
                try
                {
                    if (!PvpConfig.Active || hit == null) return true;
                    if (!(__instance is Player victim) || !victim.m_nview.IsValid() || !victim.m_nview.IsOwner()) return true;
                    if (!(hit.GetAttacker() is Player attacker) || attacker == victim) return true;

                    PvpRules.Verdict verdict = PvpRules.Check(attacker, victim);
                    if (verdict != PvpRules.Verdict.Allow) return false;

                    // O corte entra depois da armadura (PvpAfterArmorPatch), nao aqui.
                    _pvpHit = hit;
                    _pvpHitMultiplier = PvpRules.DamageMultiplier(victim);
                    _pvpHitScaled = false;
                    if (victim == Player.m_localPlayer) PvpState.RecordPvpHit(hit.m_attacker);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Deadheim PvP] Regra de dano falhou: " + ex);
                    return true;
                }
            }

            /// <summary>
            /// Veneno e fogo viram dano continuo sem atacante (SE_Poison/SE_Burning chamam
            /// ApplyDamage direto). O credito da morte tem que durar enquanto o efeito durar.
            /// </summary>
            private static void Postfix(Character __instance, HitData hit)
            {
                if (_pvpHit == null || !ReferenceEquals(hit, _pvpHit)) return;
                if (__instance is Player victim && victim == Player.m_localPlayer)
                    PvpState.CoverDamageOverTime(victim);
            }

            private static void Finalizer()
            {
                _pvpHit = null;
                _pvpHitMultiplier = 1f;
                _pvpHitScaled = false;
            }
        }

        /// <summary>
        /// Reducao de dano PvP depois da armadura. A armadura do Valheim e quadratica
        /// (dano^2 / 4*armadura quando a armadura passa de metade do golpe): cortar o golpe
        /// cru pela metade tirava 70-75% do dano de quem usa armadura, nao os 50% da config.
        /// Aqui o jogador perde exatamente DamageMultiplier da vida que perderia no vanilla.
        /// </summary>
        [HarmonyPatch(typeof(HitData), nameof(HitData.ApplyArmor))]
        private static class PvpAfterArmorPatch
        {
            private static void Postfix(HitData __instance)
            {
                if (_pvpHitScaled || _pvpHit == null || !ReferenceEquals(__instance, _pvpHit)) return;
                __instance.ApplyModifier(_pvpHitMultiplier);
                _pvpHitScaled = true;
            }
        }

        /// <summary>
        /// Atacante: Character.Damage roda no cliente de quem bateu, antes de mandar o RPC.
        /// Barrar aqui evita o golpe fantasma (efeito sem dano) e marca combate em quem ataca.
        /// </summary>
        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        private static class AttackerDamagePatch
        {
            private static bool Prefix(Character __instance, HitData hit)
            {
                try
                {
                    if (!PvpConfig.Active || hit == null) return true;
                    Player local = Player.m_localPlayer;
                    if (local == null || !(__instance is Player victim) || victim == local) return true;
                    if (hit.GetAttacker() != local) return true;

                    PvpRules.Verdict verdict = PvpRules.Check(local, victim);
                    if (verdict != PvpRules.Verdict.Allow)
                    {
                        Refuse(local, PvpRules.Explain(verdict));
                        return false;
                    }

                    PvpState.MarkAttack(victim);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Deadheim PvP] Checagem do atacante falhou: " + ex);
                    return true;
                }
            }
        }

        /// <summary>Barco e carroca nao tomam dano de jogador.</summary>
        [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
        private static class TransportDamagePatch
        {
            [HarmonyPriority(Priority.High)]
            private static bool Prefix(WearNTear __instance, HitData hit)
            {
                if (!PvpConfig.Active || !PvpConfig.TransportsInvulnerable.Value || hit == null) return true;
                if (!(hit.GetAttacker() is Player)) return true;
                if (__instance.GetComponent<Ship>() == null && __instance.GetComponent<Vagon>() == null) return true;
                return false;
            }
        }

        // ----------------------------------------------------------------- morte

        private static float _pendingSkillMultiplier = 1f;
        private static float _keepTimeSinceDeath = -1f;

        /// <summary>
        /// Antes do vanilla: m_lastHit ainda diz quem matou e Skills.OnDeath ainda nao rodou.
        /// Decide perda de skill, imunidade e avisa o servidor.
        ///
        /// O vanilla tem a janela "sem perda de skill" (m_hardDeathCooldown): quem morre de
        /// novo pouco depois de morrer nao perde skill. Duas regras mexem nela:
        /// - morte sem perda (arena, castelo) nao abre a janela, senao morrer na arena de
        ///   proposito daria minutos de morte gratis no PvE e no PvP;
        /// - morte de PK sempre perde, mesmo dentro da janela, senao o PK nao pagaria nada
        ///   se tivesse morrido ha pouco.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
        private static class DeathPatch
        {
            private static void Prefix(Player __instance)
            {
                _pendingSkillMultiplier = 1f;
                _keepTimeSinceDeath = -1f;
                try
                {
                    if (!PvpConfig.Active) return;
                    if (__instance != Player.m_localPlayer || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner()) return;

                    Vector3 pos = __instance.transform.position;

                    PvpRules.DeathCause cause = PvpRules.ClassifyDeath(__instance, __instance.m_lastHit, out ZDOID killer, out long killerId);
                    bool byPlayer = cause != PvpRules.DeathCause.Pve;

                    bool arena = PvpZones.IsArena(pos);
                    // Castelo so conta em morte por jogador: um mob dentro do castelo e PvE comum.
                    string castle = byPlayer && !arena ? PvpBridge.Castle(pos) : null;
                    bool killerDefendingCastle = castle != null
                                                 && PvpRules.IsDefendingCastle(Player.GetPlayer(killerId), __instance, pos);
                    bool wasPk = PvpState.IsPk;

                    if (arena && PvpConfig.ArenaNoSkillLoss.Value) _pendingSkillMultiplier = 0f;
                    else if (castle != null && PvpConfig.CastleNoSkillLoss.Value) _pendingSkillMultiplier = 0f;
                    else if (wasPk) _pendingSkillMultiplier = Mathf.Max(0f, PvpConfig.PkSkillLossMultiplier.Value);

                    if (_pendingSkillMultiplier <= 0f) _keepTimeSinceDeath = __instance.m_timeSinceDeath;
                    else if (wasPk) __instance.ClearHardDeath();

                    bool warZone = arena || (castle != null && PvpConfig.CastleIgnoresImmunity.Value);
                    if (byPlayer && !warZone && PvpConfig.ImmunityMinutes.Value > 0f)
                        PvpState.GrantImmunity(__instance, PvpConfig.ImmunityMinutes.Value * 60d);
                    if (wasPk && PvpConfig.PkClearsOnDeath.Value) PvpState.ClearPk();

                    bool wasAggressor = PvpState.IsAggressor;
                    // Antes do vanilla criar a tumba: a parte das moedas que fica para quem matou.
                    int coinsDropped = byPlayer && !arena ? DropCoins(__instance, pos) : 0;

                    PvpState.ForgetAttacker();
                    PvpState.ClearAggressor();
                    PvpState.ClearCombat();
                    PvpClient.SendDeath(killer, arena, castle, killerDefendingCastle, pos, wasAggressor, coinsDropped);

                    Debug.Log($"[Deadheim PvP] Morri: causa={cause} matador={killerId} ultimoGolpe={__instance.m_lastHit?.m_hitType} " +
                              $"arena={arena} castelo={castle ?? "-"} defesaDoMatador={killerDefendingCastle} PK={wasPk} " +
                              $"agressor={wasAggressor} moedasNoChao={coinsDropped} multiplicadorSkill={_pendingSkillMultiplier}");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Deadheim PvP] Tratamento da morte falhou: " + ex);
                }
            }

            /// <summary>
            /// PvpCoinDropPercent das moedas vao para o chao, fora da tumba (que so o dono abre):
            /// e o saque de quem matou.
            /// </summary>
            private static int DropCoins(Player player, Vector3 pos)
            {
                float percent = Mathf.Clamp(PvpConfig.PvpCoinDropPercent.Value, 0f, 100f);
                if (percent <= 0f) return 0;
                GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Coins") : null;
                ItemDrop coins = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                Inventory inventory = player.GetInventory();
                if (coins == null || inventory == null) return 0;

                string name = coins.m_itemData.m_shared.m_name;
                int amount = Mathf.FloorToInt(inventory.CountItems(name) * percent / 100f);
                if (amount <= 0) return 0;
                inventory.RemoveItem(name, amount);

                int maxStack = Mathf.Max(1, coins.m_itemData.m_shared.m_maxStackSize);
                for (int left = amount; left > 0; left -= maxStack)
                {
                    Vector3 at = pos + Vector3.up * 0.7f + UnityEngine.Random.insideUnitSphere * 0.4f;
                    ItemDrop drop = UnityEngine.Object.Instantiate(prefab, at, Quaternion.identity).GetComponent<ItemDrop>();
                    if (drop != null) drop.SetStack(Mathf.Min(left, maxStack));
                }
                return amount;
            }

            private static void Postfix(Player __instance)
            {
                if (_keepTimeSinceDeath >= 0f && __instance == Player.m_localPlayer)
                    __instance.m_timeSinceDeath = _keepTimeSinceDeath;
            }

            private static void Finalizer()
            {
                _pendingSkillMultiplier = 1f;
                _keepTimeSinceDeath = -1f;
            }
        }

        [HarmonyPatch(typeof(Skills), nameof(Skills.OnDeath))]
        private static class SkillLossPatch
        {
            private static bool Prefix(Skills __instance)
            {
                float multiplier = _pendingSkillMultiplier;
                if (Mathf.Approximately(multiplier, 1f)) return true;

                if (multiplier <= 0f)
                {
                    __instance.m_player?.Message(MessageHud.MessageType.TopLeft, "Sem perda de skill nesta morte.");
                    return false;
                }

                float factor = Mathf.Clamp01(__instance.m_DeathLowerFactor * Game.m_skillReductionRate * multiplier);
                __instance.LowerAllSkills(factor);
                __instance.m_player?.Message(MessageHud.MessageType.TopLeft,
                    $"<color=#ff5050>PK: perda de skill x{multiplier:0.#}</color>");
                return false;
            }
        }

        // ------------------------------------------------------------ bandeira de PvP

        /// <summary>
        /// O vanilla re-aplica o toggle de PvP do inventario toda vez que a tela abre
        /// (player.SetPVP(m_pvp.isOn)). Alinhamos o toggle com o estado que o modulo decidiu
        /// antes, para o vanilla re-aplicar a mesma coisa, e travamos o botao.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), "UpdateCharacterStats")]
        private static class PvpTogglePatch
        {
            private static void Prefix(InventoryGui __instance, Player player)
            {
                if (!PvpConfig.Active || player == null || __instance.m_pvp == null) return;
                __instance.m_pvp.SetIsOnWithoutNotify(player.m_pvp);
            }

            private static void Postfix(InventoryGui __instance)
            {
                if (!PvpConfig.Active || __instance.m_pvp == null) return;
                __instance.m_pvp.interactable = false;
            }
        }

        /// <summary>Nome sobre a cabeca: marcas de PK, cacado, imune e seguro. A guilda e o Guilds que mostra.</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.GetHoverName))]
        private static class HoverNamePatch
        {
            private static void Postfix(Player __instance, ref string __result)
            {
                if (!PvpConfig.Active || __instance == null || __instance == Player.m_localPlayer) return;

                PvpFlags flags = PvpState.FlagsOf(__instance);
                string suffix = string.Empty;
                if ((flags & PvpFlags.Hunted) != 0) suffix += " <color=#ff8c00>[CACADO]</color>";
                int pkCount = PvpState.PkCountOf(__instance);
                if ((flags & PvpFlags.Pk) != 0) suffix += pkCount > 1 ? $" <color=#ff3030>[PK x{pkCount}]</color>" : " <color=#ff3030>[PK]</color>";
                if ((flags & PvpFlags.Aggressor) != 0) suffix += " <color=#ff7a3d>[AGRESSOR]</color>";
                if ((flags & PvpFlags.Immune) != 0) suffix += " <color=#7fd4ff>[IMUNE]</color>";
                else if ((flags & PvpFlags.Protected) != 0) suffix += " <color=#7CFC00>[SEGURO]</color>";
                __result += suffix;
            }
        }

        // ------------------------------------------------------------------- tumba

        [HarmonyPatch(typeof(TombStone), nameof(TombStone.Interact))]
        private static class TombstoneLockPatch
        {
            [HarmonyPriority(Priority.High)]
            private static bool Prefix(TombStone __instance, Humanoid character, bool hold, ref bool __result)
            {
                if (hold || !PvpConfig.Active || !PvpConfig.TombstoneOwnerOnly.Value) return true;
                if (!(character is Player player) || player != Player.m_localPlayer) return true;
                if (Admin.LocalPlayerIsAdmin()) return true;

                long owner = __instance.GetOwner();
                long me = player.GetPlayerID();
                if (owner == 0L || owner == me) return true;
                if (PvpConfig.TombstoneGuildAccess.Value && PvpGuilds.SameGuild(Player.GetPlayer(owner), player)) return true;

                player.Message(MessageHud.MessageType.Center, "Esta tumba pertence a " + __instance.GetOwnerName() + ".");
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(TombStone), nameof(TombStone.GetHoverText))]
        private static class TombstoneHoverPatch
        {
            private static void Postfix(TombStone __instance, ref string __result)
            {
                if (!PvpConfig.Active || !PvpConfig.TombstoneOwnerOnly.Value) return;
                Player player = Player.m_localPlayer;
                if (player == null || __instance.GetOwner() == player.GetPlayerID()) return;
                if (PvpConfig.TombstoneGuildAccess.Value && PvpGuilds.SameGuild(Player.GetPlayer(__instance.GetOwner()), player)) return;
                __result += "\n<color=#ff5050>Trancada: so o dono abre</color>";
            }
        }

        // ------------------------------------------------------------------ teleporte

        private static bool _teleportByAdmin;

        /// <summary>
        /// Teleporte longo (portal, NPC teleportador, pedra de retorno, retreat) e fuga: o
        /// cacado nao usa (HuntedCanUsePortals) e ninguem usa em combate (CombatBlocksTeleport).
        /// Um lugar so, em vez de um patch por mod que teleporta. Porta de masmorra e
        /// distantTeleport=false e passa.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
        private static class TeleportEscapePatch
        {
            [HarmonyPriority(Priority.High)]
            private static bool Prefix(Player __instance, bool distantTeleport, ref bool __result)
            {
                if (!PvpConfig.Active || !distantTeleport || __instance != Player.m_localPlayer) return true;
                if (_teleportByAdmin || Admin.LocalPlayerIsAdmin()) return true;

                string refusal = null;
                if (PvpState.IsHunted && !PvpConfig.HuntedCanUsePortals.Value)
                    refusal = "Cacado nao pode teleportar.";
                else if (PvpState.InCombat && PvpConfig.CombatBlocksTeleport.Value)
                    refusal = $"Em combate! Teleporte liberado em {Mathf.CeilToInt(PvpState.CombatRemaining)}s.";
                if (refusal == null) return true;

                Refuse(__instance, refusal);
                __result = false;
                return false;
            }
        }

        /// <summary>Admin puxando alguem (RPC_TeleportTo) nao e fuga.</summary>
        [HarmonyPatch(typeof(Character), "RPC_TeleportTo")]
        private static class AdminTeleportPatch
        {
            private static void Prefix() => _teleportByAdmin = true;
            private static void Finalizer() => _teleportByAdmin = false;
        }

        // ----------------------------------------------------- pedra do retorno (Hearthstone)

        /// <summary>
        /// O item do mod Hearthstone teleporta ao ser consumido. Em combate ou cacado ele
        /// teria o mesmo efeito do /retreat que o modulo bloqueia, entao vale a mesma regra.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem))]
        private static class HearthstoneCombatPatch
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Player __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (!PvpConfig.Active || __instance != Player.m_localPlayer || item?.m_shared?.m_name == null) return true;
                if (item.m_shared.m_name.IndexOf("hearthstone", StringComparison.OrdinalIgnoreCase) < 0) return true;

                string refusal = PvpModule.TeleportRefusal();
                if (refusal == null) return true;
                Refuse(__instance, refusal);
                __result = false;
                return false;
            }
        }

        // ------------------------------------------------------ raids aleatorias do jogo

        /// <summary>
        /// Zera os relogios antes de cada passo do sorteio: nenhum ataque aleatorio de
        /// monstros comeca, mas o resto do metodo (envio do evento atual, eventos forcados
        /// por admin) continua rodando como no vanilla.
        /// </summary>
        [HarmonyPatch(typeof(RandEventSystem), "UpdateRandomEvent")]
        private static class NoRandomRaidsPatch
        {
            private static void Prefix(RandEventSystem __instance)
            {
                if (!PvpConfig.Active || !PvpConfig.DisableRandomEvents.Value) return;
                __instance.m_eventTimer = 0f;
                // m_time do evento ATIVO e a duracao dele (RandomEvent.Update): nao mexe,
                // senao um evento chamado por admin nunca terminaria.
                foreach (RandomEvent ev in __instance.m_events)
                    if (ev != null && ev != __instance.m_activeEvent) ev.m_time = 0f;
            }
        }
    }
}
