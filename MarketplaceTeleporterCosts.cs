using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Deadheim
{
    public static class MarketplaceTeleporterCosts
    {
        private static readonly Regex RichTextRegex = new Regex("</?(?!color\\b)\\w+[^>]*>", RegexOptions.Compiled);
        private static string _currentTeleporterNpcName = "";
        private static Harmony _harmony;
        private static bool _npcPatchApplied;
        private static bool _clickPatchApplied;
        private static bool _waitingLogged;
        private static string _lastPatchError = "";

        public static void StartPatchRetry(MonoBehaviour host, Harmony harmony)
        {
            if (host == null || harmony == null) return;

            _harmony = harmony;
            host.StartCoroutine(PatchWhenMarketplaceReady());
        }

        private static IEnumerator PatchWhenMarketplaceReady()
        {
            while (!_npcPatchApplied || !_clickPatchApplied)
            {
                TryPatchMarketplace();
                if (_npcPatchApplied && _clickPatchApplied)
                {
                    Debug.Log("[Deadheim] Marketplace teleporter cost patches applied.");
                    yield break;
                }

                if (!_waitingLogged)
                {
                    Debug.Log("[Deadheim] Waiting for Marketplace to load before applying teleporter cost patches.");
                    _waitingLogged = true;
                }

                yield return new WaitForSeconds(2f);
            }
        }

        private static void TryPatchMarketplace()
        {
            if (_harmony == null) return;

            try
            {
                if (!_npcPatchApplied)
                {
                    MethodBase npcTarget = FindRememberTeleporterNpcTarget();
                    if (npcTarget != null)
                    {
                        _harmony.Patch(npcTarget, prefix: new HarmonyMethod(typeof(MarketplaceTeleporterCosts), nameof(RememberTeleporterNpcPrefix)));
                        _npcPatchApplied = true;
                    }
                }

                if (!_clickPatchApplied)
                {
                    MethodBase clickTarget = FindTeleporterPinClickTarget();
                    if (clickTarget != null)
                    {
                        _harmony.Patch(clickTarget, prefix: new HarmonyMethod(typeof(MarketplaceTeleporterCosts), nameof(ChargeOnTeleporterPinClickPrefix)));
                        _clickPatchApplied = true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_lastPatchError != ex.Message)
                {
                    _lastPatchError = ex.Message;
                    Debug.LogWarning("[Deadheim] Marketplace teleporter patch retry failed: " + ex.Message);
                }
            }
        }

        private static MethodBase FindRememberTeleporterNpcTarget()
        {
            Type npcComponent = AccessTools.TypeByName("Marketplace.Modules.NPC.Market_NPC+NPCcomponent");
            if (npcComponent != null)
            {
                MethodBase knownTarget = FindOpenUIForType(npcComponent);
                if (knownTarget != null) return knownTarget;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = Array.FindAll(ex.Types, t => t != null);
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type.FullName.IndexOf("Marketplace", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    MethodBase target = FindOpenUIForType(type);
                    if (target != null) return target;
                }
            }

            return null;
        }

        private static MethodBase FindOpenUIForType(Type type)
        {
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name != "OpenUIForType") continue;
                if (parameters.Length < 3) continue;
                if (parameters[1].ParameterType != typeof(string)) continue;
                if (parameters[2].ParameterType != typeof(string)) continue;
                return method;
            }

            return null;
        }

        private static void RememberTeleporterNpcPrefix(object[] __args)
        {
            if (__args == null || __args.Length < 3 || __args[0] == null) return;

            string npcType = __args[0].ToString();
            if (!npcType.Equals("Teleporter", StringComparison.OrdinalIgnoreCase)) return;

            _currentTeleporterNpcName = CleanName(__args[2] as string);
        }

        private static MethodBase FindTeleporterPinClickTarget()
        {
            Type clickPatch = AccessTools.TypeByName("Marketplace.Modules.Teleporter.Teleporter_Main_Client+PatchClickIconMinimap");
            MethodBase knownTarget = clickPatch == null ? null : AccessTools.Method(clickPatch, "Prefix", new[] { typeof(Minimap) });
            if (knownTarget != null) return knownTarget;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = Array.FindAll(ex.Types, t => t != null);
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type.FullName.IndexOf("Marketplace", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (type.FullName.IndexOf("Teleporter", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    MethodInfo method = AccessTools.Method(type, "Prefix", new[] { typeof(Minimap) });
                    if (method != null) return method;
                }
            }

            return null;
        }

        private static bool ChargeOnTeleporterPinClickPrefix(Minimap __0)
        {
            try
            {
                if (__0 == null || Player.m_localPlayer == null) return true;
                if (!TryGetClickedTeleporterPin(__0, out _)) return true;

                int cost = GetCostForNpc(_currentTeleporterNpcName);
                if (cost <= 0) return true;

                if (!CanUseMarketplaceTeleporterWithCurrentInventory()) return true;

                Inventory inventory = Player.m_localPlayer.GetInventory();
                string itemName = Plugin.MarketplaceTeleporterCostItem.Value;
                int available = inventory.CountItems(itemName);

                if (available < cost)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Voce precisa de {cost} moedas para teleportar com {_currentTeleporterNpcName}.");
                    __0.SetMapMode(Minimap.MapMode.Small);
                    return false;
                }

                inventory.RemoveItem(itemName, cost);
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Teleporte pago: {cost} moedas.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Deadheim] Marketplace teleporter cost check failed: " + ex);
                return true;
            }
        }

        private static int GetCostForNpc(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName) || Plugin.MarketplaceTeleporterCosts == null) return 0;

            Dictionary<string, int> costs = ParseCosts(Plugin.MarketplaceTeleporterCosts.Value);
            return costs.TryGetValue(NormalizeName(npcName), out int cost) ? cost : 0;
        }

        private static Dictionary<string, int> ParseCosts(string raw)
        {
            Dictionary<string, int> costs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw)) return costs;

            string[] entries = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string entry in entries)
            {
                string[] parts = entry.Split(new[] { ':' }, 2);
                if (parts.Length != 2) continue;

                string name = NormalizeName(parts[0]);
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (int.TryParse(parts[1].Trim(), out int cost) && cost > 0)
                    costs[name] = cost;
            }

            return costs;
        }

        private static bool TryGetClickedTeleporterPin(Minimap minimap, out Minimap.PinData clickedPin)
        {
            clickedPin = null;

            Type teleporterClient = AccessTools.TypeByName("Marketplace.Modules.Teleporter.Teleporter_Main_Client");
            FieldInfo pinsField = teleporterClient == null ? null : AccessTools.Field(teleporterClient, "CurrentTeleporterObjects");
            if (!(pinsField?.GetValue(null) is IEnumerable pins)) return false;

            Vector3 mouseWorldPosition = minimap.ScreenToWorldPoint(Input.mousePosition);
            float radius = minimap.m_removeRadius * minimap.m_largeZoom * 2f;

            foreach (object pinObject in pins)
            {
                if (!(pinObject is Minimap.PinData pin)) continue;
                if (Vector3.Distance(pin.m_pos, mouseWorldPosition) > radius) continue;

                clickedPin = pin;
                return true;
            }

            return false;
        }

        private static bool CanUseMarketplaceTeleporterWithCurrentInventory()
        {
            if (Player.m_localPlayer.IsTeleportable()) return true;
            return MarketplaceAllowsTeleportWithOre();
        }

        private static bool MarketplaceAllowsTeleportWithOre()
        {
            try
            {
                Type globalConfigs = AccessTools.TypeByName("Marketplace.Modules.Global_Options.Global_Configs");
                FieldInfo syncedOptionsField = globalConfigs == null ? null : AccessTools.Field(globalConfigs, "SyncedGlobalOptions");
                object syncedOptions = syncedOptionsField?.GetValue(null);
                object value = syncedOptions?.GetType().GetProperty("Value")?.GetValue(syncedOptions, null);
                FieldInfo canTeleportWithOre = value == null ? null : AccessTools.Field(value.GetType(), "_canTeleportWithOre");
                return canTeleportWithOre != null && (bool)canTeleportWithOre.GetValue(value);
            }
            catch
            {
                return false;
            }
        }

        private static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            return RichTextRegex.Replace(name, "").Trim();
        }

        private static string NormalizeName(string name)
        {
            return CleanName(name).ToLowerInvariant();
        }
    }
}
