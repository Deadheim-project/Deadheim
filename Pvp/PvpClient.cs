using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>Lado cliente: o que chega do servidor e o que o jogador local conta a ele.</summary>
    internal static class PvpClient
    {
        public static double BountyCooldown { get; private set; }
        private static double _bountyCooldownAt;

        /// <summary>O servidor parou o relogio da bounty (cacado no proprio ward ou pouca gente online).</summary>
        public static bool HuntPaused { get; private set; }
        /// <summary>Moedas na cabeca do jogador local (0 = sem bounty).</summary>
        public static int BountyPot { get; private set; }
        /// <summary>A bounty do jogador local so acaba com a morte dele.</summary>
        public static bool BountyUntilDeath { get; private set; }
        private static bool _punishPending;

        public static double BountyCooldownRemaining
            => Math.Max(0d, BountyCooldown - (PvpState.Now - _bountyCooldownAt));

        public static void Handle(string op, ZPackage pkg)
        {
            switch (op)
            {
                case PvpNet.OpState:
                {
                    double pk = pkg.ReadDouble();
                    bool pkPermanent = pkg.ReadBool();
                    int pkPenalty = pkg.ReadInt();
                    int pkCount = pkg.ReadInt();
                    double pending = pkg.ReadDouble();
                    double hunted = pkg.ReadDouble();
                    BountyPot = pkg.ReadInt();
                    BountyUntilDeath = pkg.ReadBool();
                    HuntPaused = pkg.ReadBool();
                    BountyCooldown = pkg.ReadDouble();
                    _bountyCooldownAt = PvpState.Now;
                    PvpPve.ApplyLocal(pkg.ReadBool());
                    PvpState.ApplyServerTimers(pk, pending, hunted, BountyPot > 0 && BountyUntilDeath);
                    PvpState.ApplyPk(pkPermanent, (PvpConfig.PkPenalty)pkPenalty);
                    PvpState.ApplyPkCount(Player.m_localPlayer, pkCount);
                    break;
                }
                case PvpNet.OpPunish:
                    _punishPending = true;
                    break;
                case PvpNet.OpMessage:
                {
                    bool center = pkg.ReadBool();
                    ShowMessage(pkg.ReadString(), center);
                    break;
                }
                case PvpNet.OpReward:
                    GiveReward(pkg.ReadString(), pkg.ReadInt(), pkg.ReadString());
                    break;
                case PvpNet.OpRankResult:
                {
                    int count = pkg.ReadInt();
                    List<string> lines = new List<string>();
                    for (int i = 0; i < count; i++) lines.Add(pkg.ReadString());
                    string mine = pkg.ReadString();
                    PvpRankPanel.Show(lines, mine);
                    break;
                }
                default:
                    Debug.LogWarning("[Deadheim PvP] Operacao desconhecida do servidor: " + op);
                    break;
            }
        }

        public static void ShowMessage(string text, bool center)
        {
            if (string.IsNullOrEmpty(text)) return;
            Player local = Player.m_localPlayer;
            if (center && local != null) local.Message(MessageHud.MessageType.Center, text);
            else if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, text);
            if (Chat.instance != null)
                foreach (string line in text.Split('\n'))
                    Chat.instance.AddString("<color=#c0c0c0>[PvP]</color> " + line);
            Debug.Log("[Deadheim PvP] " + text);
        }

        /// <summary>Recompensa no inventario; o que nao couber cai no chao, aos pes.</summary>
        public static void GiveReward(string prefabName, int amount, string reason)
        {
            Player player = Player.m_localPlayer;
            if (player == null || amount <= 0) return;

            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                Debug.LogWarning("[Deadheim PvP] Item de recompensa desconhecido: " + prefabName);
                return;
            }

            int left = amount;
            int maxStack = Mathf.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
            Inventory inventory = player.GetInventory();
            while (left > 0)
            {
                int chunk = Mathf.Min(left, maxStack);
                if (!inventory.AddItem(prefab, chunk))
                {
                    ItemDrop dropped = UnityEngine.Object.Instantiate(prefab,
                        player.transform.position + player.transform.forward + Vector3.up, Quaternion.identity).GetComponent<ItemDrop>();
                    if (dropped != null) dropped.SetStack(chunk);
                }
                left -= chunk;
            }

            string itemName = Localization.instance != null
                ? Localization.instance.Localize(drop.m_itemData.m_shared.m_name)
                : prefabName;
            ShowMessage($"<color=#ffd700>+{amount} {itemName}</color> - {reason}", true);
        }

        /// <summary>
        /// Deslogou em combate (CombatLogout=Death): morre assim que o personagem estiver de pe
        /// no mundo, onde saiu. Morte sem atacante: PvE, com a perda de skill normal e a tumba ali.
        /// </summary>
        public static void Update()
        {
            if (!_punishPending) return;
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || player.InCutscene() || player.IsTeleporting()
                || !player.m_nview.IsValid() || !player.m_nview.IsOwner()) return;
            _punishPending = false;

            ShowMessage("<color=#ff5050>Voce deslogou em combate e morreu onde saiu.</color>", true);
            HitData hit = new HitData { m_hitType = HitData.HitType.Undefined, m_point = player.GetCenterPoint() };
            hit.m_damage.m_damage = 1e7f;
            player.m_nview.InvokeRPC("RPC_Damage", hit);
            Debug.Log("[Deadheim PvP] Morte por deslogar em combate aplicada.");
        }

        public static void ResetSession()
        {
            HuntPaused = false;
            BountyPot = 0;
            BountyUntilDeath = false;
            _punishPending = false;
        }

        // -------------------------------------------------------------------- envio

        public static void SendHello() => PvpNet.SendToServer(PvpNet.Package(PvpNet.OpHello));

        public static void SendDeath(ZDOID killer, bool arena, string castle, bool killerDefendingCastle, Vector3 position,
                                     bool victimWasAggressor, int coinsDropped, int cargoDropped)
        {
            ZPackage pkg = PvpNet.Package(PvpNet.OpDeath);
            pkg.Write(killer);
            pkg.Write(arena);
            pkg.Write(castle ?? string.Empty);
            pkg.Write(killerDefendingCastle);
            pkg.Write(position);
            pkg.Write(victimWasAggressor);
            pkg.Write(coinsDropped);
            pkg.Write(cargoDropped);
            PvpNet.SendToServer(pkg);
        }

        /// <summary>Comando de admin que o servidor decide (pk). O servidor confere se e admin.</summary>
        public static void SendAdmin(string action, string target, double value)
        {
            ZPackage pkg = PvpNet.Package(PvpNet.OpAdmin);
            pkg.Write(action ?? string.Empty);
            pkg.Write(target ?? string.Empty);
            pkg.Write(value);
            PvpNet.SendToServer(pkg);
        }

        /// <summary>
        /// Pedido de bounty. Em place/pay as moedas saem do inventario AQUI, antes de pedir
        /// (o inventario e do cliente); se o servidor recusar, ele devolve.
        /// </summary>
        /// <summary>join (virar PvE permanente) | admin-off &lt;jogador&gt;. Quem decide e o servidor.</summary>
        public static void SendPve(string action, string target)
        {
            ZPackage pkg = PvpNet.Package(PvpNet.OpPve);
            pkg.Write(action ?? string.Empty);
            pkg.Write(target ?? string.Empty);
            PvpNet.SendToServer(pkg);
        }

        public static bool SendBounty(string action, string target, int amount, out string refusal, int houseAmount = 0)
        {
            refusal = null;
            if (amount > 0 && !TakeCoins(amount, out refusal)) return false;
            ZPackage pkg = PvpNet.Package(PvpNet.OpBounty);
            pkg.Write(action ?? string.Empty);
            pkg.Write(target ?? string.Empty);
            // Bounty de admin (paga pela casa): nenhuma moeda sai do inventario.
            pkg.Write(amount > 0 ? amount : houseAmount);
            PvpNet.SendToServer(pkg);
            return true;
        }

        private static bool TakeCoins(int amount, out string refusal)
        {
            refusal = null;
            Player player = Player.m_localPlayer;
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Coins") : null;
            ItemDrop coins = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            Inventory inventory = player != null ? player.GetInventory() : null;
            if (coins == null || inventory == null)
            {
                refusal = "Entre no mundo primeiro.";
                return false;
            }
            string name = coins.m_itemData.m_shared.m_name;
            int have = inventory.CountItems(name);
            if (have < amount)
            {
                refusal = $"Voce precisa de {amount} moedas no inventario (tem {have}).";
                return false;
            }
            inventory.RemoveItem(name, amount);
            return true;
        }

        public static void SendRankRequest() => PvpNet.SendToServer(PvpNet.Package(PvpNet.OpRank));

        // ------------------------------------------------------------------ formato

        public static string FormatDuration(double seconds)
        {
            if (seconds < 0d) seconds = 0d;
            TimeSpan span = TimeSpan.FromSeconds(Math.Ceiling(seconds));
            if (span.TotalHours >= 1d) return $"{(int)span.TotalHours}h{span.Minutes:00}m";
            if (span.TotalMinutes >= 1d) return $"{span.Minutes}m{span.Seconds:00}s";
            return $"{span.Seconds}s";
        }
    }
}
