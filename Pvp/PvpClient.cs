using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>Lado cliente: o que chega do servidor e o que o jogador local conta a ele.</summary>
    internal static class PvpClient
    {
        public static double ChallengeCooldown { get; private set; }
        private static double _challengeCooldownAt;

        public static double ChallengeCooldownRemaining
            => Math.Max(0d, ChallengeCooldown - (PvpState.Now - _challengeCooldownAt));

        public static void Handle(string op, ZPackage pkg)
        {
            switch (op)
            {
                case PvpNet.OpState:
                {
                    double pk = pkg.ReadDouble();
                    double pending = pkg.ReadDouble();
                    double hunted = pkg.ReadDouble();
                    ChallengeCooldown = pkg.ReadDouble();
                    _challengeCooldownAt = PvpState.Now;
                    PvpState.ApplyServerTimers(pk, pending, hunted);
                    break;
                }
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

        // -------------------------------------------------------------------- envio

        public static void SendHello() => PvpNet.SendToServer(PvpNet.Package(PvpNet.OpHello));

        public static void SendDeath(ZDOID killer, bool arena, string castle, bool killerDefendingCastle, Vector3 position)
        {
            ZPackage pkg = PvpNet.Package(PvpNet.OpDeath);
            pkg.Write(killer);
            pkg.Write(arena);
            pkg.Write(castle ?? string.Empty);
            pkg.Write(killerDefendingCastle);
            pkg.Write(position);
            PvpNet.SendToServer(pkg);
        }

        public static void SendChallenge(string action)
        {
            ZPackage pkg = PvpNet.Package(PvpNet.OpChallenge);
            pkg.Write(action ?? string.Empty);
            PvpNet.SendToServer(pkg);
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
