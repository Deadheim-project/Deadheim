using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace Deadheim.Wards
{
    /// <summary>
    /// Implementacao unica de acesso, combustivel, limite e protecao de ward.
    ///
    /// Substitui o que estava duplicado em Deadheim/Ward.cs, Deadheim/Util.cs,
    /// Deadheim/CraftingStations.cs e RaidSystem/PlayerWard.cs.
    /// </summary>
    public static class WardCore
    {
        private const string ZdoGuild = "dh_wardGuild";
        private const string ZdoFuel = "dh_wardFuel";
        private const string ZdoFuelTick = "dh_wardFuelTick";

        private static float _nextFuelTick;
        private static float _nextCountRefresh;

        // ------------------------------------------------------------------ acesso

        private static ZNetView NView(PrivateArea area)
        {
            ZNetView nview = area != null ? area.m_nview : null;
            return nview != null && nview.IsValid() ? nview : null;
        }

        public static string GetWardGuild(PrivateArea area)
        {
            ZNetView nview = NView(area);
            return nview == null ? null : nview.GetZDO().GetString(ZdoGuild, string.Empty);
        }

        /// <summary>
        /// Acesso por guild. Comparacao pura: e chamado do postfix de PrivateArea.IsPermitted,
        /// entao nao pode chamar IsPermitted de volta sob pena de recursao infinita.
        /// </summary>
        public static bool HasGuildAccess(PrivateArea area, long playerId)
        {
            if (!WardProfiles.GuildAccessEnabled.Value) return false;

            WardProfile profile = WardProfiles.For(area);
            if (profile == null || !profile.GuildAccess) return false;

            string wardGuild = GetWardGuild(area);
            if (string.IsNullOrEmpty(wardGuild)) return false;

            string playerGuild = WardBridge.GuildOf(playerId);
            return !string.IsNullOrEmpty(playerGuild)
                   && string.Equals(playerGuild, wardGuild, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Acesso completo. IsPermitted ja traz a guild embutida pelo postfix.</summary>
        public static bool IsPermittedIn(PrivateArea area, long playerId)
        {
            if (area == null || playerId == 0L) return false;
            if (area.m_piece != null && area.m_piece.GetCreator() == playerId) return true;
            return area.IsPermitted(playerId);
        }

        public static bool IsPermittedIn(PrivateArea area, Player player)
            => player != null && IsPermittedIn(area, player.GetPlayerID());

        /// <summary>Grava a guild do dono na ZDO. Roda no dono da ZDO, quando o ward nasce.</summary>
        public static void StampGuild(PrivateArea area)
        {
            ZNetView nview = NView(area);
            if (nview == null || !nview.IsOwner()) return;
            if (!string.IsNullOrEmpty(nview.GetZDO().GetString(ZdoGuild, string.Empty))) return;

            Player lp = Player.m_localPlayer;
            if (lp == null) return;

            long creator = area.m_piece != null ? area.m_piece.GetCreator() : 0L;
            if (creator == 0L || creator != lp.GetPlayerID()) return;

            string guild = WardBridge.GuildOf(creator);
            if (!string.IsNullOrEmpty(guild)) nview.GetZDO().Set(ZdoGuild, guild);
        }

        // ------------------------------------------------------------- combustivel

        // O combustivel vive na ZDO do proprio ward. A versao antiga enxertava um Fireplace
        // no guard_stone, o que trazia junto fumaca, chuva, cobertura e objetos de fogo, e
        // registrava o RPC com o nome errado ("AddFuel" em vez de "RPC_AddFuel"), deixando o
        // abastecimento quebrado em silencio. Nada disso existe mais.

        public static float MaxFuel => Mathf.Max(1, WardProfiles.MaxCharges.Value);

        public static bool UsesFuel(PrivateArea area)
        {
            WardProfile profile = WardProfiles.For(area);
            return profile != null && profile.Fuel;
        }

        public static float GetFuel(PrivateArea area)
        {
            ZNetView nview = NView(area);
            return nview == null ? 0f : nview.GetZDO().GetFloat(ZdoFuel, 0f);
        }

        public static bool HasFuel(PrivateArea area)
            => !UsesFuel(area) || GetFuel(area) > 0f;

        /// <summary>
        /// Primeira carga do ward. Wards que ja existiam com o sistema antigo herdam o
        /// valor guardado pelo Fireplace, entao ninguem perde protecao na atualizacao.
        /// </summary>
        public static void InitFuel(PrivateArea area)
        {
            if (!UsesFuel(area)) return;

            ZNetView nview = NView(area);
            if (nview == null || !nview.IsOwner()) return;

            ZDO zdo = nview.GetZDO();
            if (zdo.GetLong(ZdoFuelTick, 0L) != 0L) return;

            float legacy = zdo.GetFloat(ZDOVars.s_fuel, -1f);
            zdo.Set(ZdoFuel, legacy >= 0f ? Mathf.Clamp(legacy, 0f, MaxFuel) : 1f);
            zdo.Set(ZdoFuelTick, ZNet.instance.GetTime().Ticks);
        }

        /// <summary>
        /// Desconta o tempo decorrido. Usa o relogio do mundo (ZNet.GetTime), entao o
        /// combustivel so queima com o servidor de pe, nao em tempo de parede.
        /// </summary>
        public static void TickFuel(PrivateArea area)
        {
            if (!UsesFuel(area)) return;

            ZNetView nview = NView(area);
            if (nview == null || !nview.IsOwner()) return;

            ZDO zdo = nview.GetZDO();
            long nowTicks = ZNet.instance.GetTime().Ticks;
            long lastTicks = zdo.GetLong(ZdoFuelTick, 0L);
            if (lastTicks <= 0L || lastTicks > nowTicks)
            {
                zdo.Set(ZdoFuelTick, nowTicks);
                return;
            }

            double elapsed = new TimeSpan(nowTicks - lastTicks).TotalSeconds;
            if (elapsed <= 0d) return;

            float perCharge = Mathf.Max(1, Plugin.WardChargeDurationInSec.Value);
            float fuel = zdo.GetFloat(ZdoFuel, 0f) - (float)(elapsed / perCharge);

            zdo.Set(ZdoFuel, Mathf.Clamp(fuel, 0f, MaxFuel));
            zdo.Set(ZdoFuelTick, nowTicks);
        }

        /// <summary>
        /// Abastece com o item configurado. Chamado do postfix de PrivateArea.UseItem, que e
        /// o gancho vanilla de "usar item no que estou olhando", igual smelter e fermentador.
        /// </summary>
        public static bool AddFuel(PrivateArea area, Humanoid user, ItemDrop.ItemData item)
        {
            if (!UsesFuel(area) || user == null || item == null || item.m_shared == null) return false;
            if (item.m_shared.m_name != FuelItemName()) return false;

            ZNetView nview = NView(area);
            if (nview == null) return false;

            if (!nview.IsOwner()) nview.ClaimOwnership();
            TickFuel(area);

            if (GetFuel(area) >= MaxFuel)
            {
                user.Message(MessageHud.MessageType.Center, "Ward ja esta cheio.");
                return true;
            }

            Inventory inventory = user.GetInventory();
            if (inventory == null || !inventory.RemoveOneItem(item)) return false;

            ZDO zdo = nview.GetZDO();
            zdo.Set(ZdoFuel, Mathf.Clamp(GetFuel(area) + 1f, 0f, MaxFuel));
            zdo.Set(ZdoFuelTick, ZNet.instance.GetTime().Ticks);

            user.Message(MessageHud.MessageType.Center,
                "Ward abastecido: " + Mathf.FloorToInt(GetFuel(area)) + "/" + Mathf.FloorToInt(MaxFuel));
            return true;
        }

        private static string FuelItemName()
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(WardProfiles.FuelItem.Value);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return drop != null ? drop.m_itemData.m_shared.m_name : null;
        }

        // -------------------------------------------------------------- protecao

        /// <summary>
        /// Devolve o ward que protege este ponto contra este jogador, ou null se nao ha.
        /// Sai fora onde outro plugin governa, por exemplo zona de raid do RaidSystem.
        /// </summary>
        public static PrivateArea GetProtectingWard(Vector3 point, Player player)
        {
            if (WardBridge.Governed(point)) return null;

            long playerId = player != null ? player.GetPlayerID() : 0L;

            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null) continue;

                WardProfile profile = WardProfiles.For(area);
                if (profile == null || !profile.Protects) continue;
                if (!area.IsEnabled() || !area.IsInside(point, 0f)) continue;
                if (playerId != 0L && IsPermittedIn(area, playerId)) continue;

                return area;
            }
            return null;
        }

        /// <summary>Atalho dos patches: bloqueia, pisca o escudo e avisa o jogador local.</summary>
        public static bool IsBlocked(Vector3 point, Player player, string message = null)
        {
            PrivateArea ward = GetProtectingWard(point, player);
            if (ward == null) return false;

            ward.FlashShield(false);
            if (!string.IsNullOrEmpty(message) && player != null && player == Player.m_localPlayer)
                player.Message(MessageHud.MessageType.Center, message);
            return true;
        }

        // ---------------------------------------------------------------- limites

        public static bool HasWardTooClose(Vector3 point, Player player, out PrivateArea blocking)
        {
            blocking = null;
            float spacing = WardProfiles.Spacing.Value;
            if (spacing <= 0f) return false;

            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null) continue;

                WardProfile profile = WardProfiles.For(area);
                if (profile == null || !profile.CountsToLimit) continue;
                if (IsPermittedIn(area, player)) continue;
                if (Utils.DistanceXZ(point, area.transform.position) > area.m_radius * spacing) continue;

                blocking = area;
                return true;
            }
            return false;
        }

        public static int GetWardLimit()
        {
            bool isVip = !string.IsNullOrEmpty(Plugin.steamId)
                         && Plugin.Vip.Value.Contains(Plugin.steamId);
            return isVip ? Plugin.WardLimitVip.Value : Plugin.WardLimit.Value;
        }

        /// <summary>
        /// Servidor: conta wards criados por este jogador. Varre as ZDOs uma unica vez
        /// somando todos os prefabs que contam para o limite, em vez de uma varredura
        /// por prefab como fazia o Util.GetCreatorPrefabCount.
        /// </summary>
        public static int CountWardsOf(long playerId)
        {
            HashSet<int> hashes = new HashSet<int>();
            hashes.Add(WardProfiles.VanillaWard.GetStableHashCode());
            if (WardProfiles.PlayerWardEnabled.Value)
                hashes.Add(WardProfiles.PlayerWard.GetStableHashCode());

            int count = 0;
            foreach (List<ZDO> sector in ZDOMan.instance.m_objectsBySector)
            {
                if (sector == null) continue;
                for (int i = 0; i < sector.Count; i++)
                {
                    ZDO zdo = sector[i];
                    if (zdo == null || !hashes.Contains(zdo.GetPrefab())) continue;
                    if (zdo.GetLong(ZDOVars.s_creator, 0L) == playerId) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Pede a contagem ao servidor pelo RPC que o Deadheim ja tinha
        /// (DeadheimPortalAndTotemCountServer), que responde em Plugin.PlayerWardCount.
        /// </summary>
        public static void RequestWardCount()
        {
            Player lp = Player.m_localPlayer;
            if (lp == null || ZRoutedRpc.instance == null) return;

            ZPackage pkg = new ZPackage();
            pkg.Write(lp.GetPlayerID());
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "DeadheimPortalAndTotemCountServer", pkg);
        }

        // ------------------------------------------------------------------ update

        /// <summary>
        /// Chamado do Update do Plugin. Queima combustivel em quem for dono da ZDO
        /// (servidor incluido) e mantem a contagem de wards fresca com o martelo na mao.
        /// </summary>
        public static void Update()
        {
            if (ZNet.instance != null && Time.time >= _nextFuelTick)
            {
                _nextFuelTick = Time.time + 5f;
                foreach (PrivateArea area in PrivateArea.m_allAreas)
                {
                    if (area == null) continue;
                    TickFuel(area);
                }
            }

            if (Time.time < _nextCountRefresh) return;

            Player lp = Player.m_localPlayer;
            if (lp == null) return;

            ItemDrop.ItemData tool = lp.GetRightItem();
            if (tool == null || tool.m_shared == null || tool.m_shared.m_buildPieces == null) return;

            _nextCountRefresh = Time.time + 10f;
            RequestWardCount();
        }
    }
}
