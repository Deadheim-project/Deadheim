using Deadheim.Vanilla;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Uma linha no topo da tela dizendo o estado de PvP do jogador: ativo, protegido (e
    /// por que), imune, PK, cacado, em combate. Pendurada no HUD do jogo, entao some junto
    /// quando o jogador esconde o HUD.
    /// </summary>
    internal static class PvpHud
    {
        private static Text _text;
        private static float _nextRefresh;
        private static bool _announced;

        public static void OnPvpChanged(bool enabled)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            // O primeiro ajuste depois de nascer nao e troca de estado: nao anuncia.
            if (!_announced)
            {
                _announced = true;
                return;
            }

            string why = PvpState.ZoneLabel;
            player.Message(MessageHud.MessageType.Center, enabled
                ? "<color=#ff5050>PvP ATIVO</color>" + (string.IsNullOrEmpty(why) ? string.Empty : " - " + why)
                : "<color=#7CFC00>PvP desligado</color>" + (string.IsNullOrEmpty(why) ? string.Empty : " - " + why));
        }

        public static void ResetSession()
        {
            _announced = false;
            if (_text != null) Object.Destroy(_text.gameObject);
            _text = null;
        }

        public static void Update(Player player)
        {
            if (Time.time < _nextRefresh) return;
            _nextRefresh = Time.time + 0.25f;

            UpdateCombatIcon(player);
            if (!EnsureText()) return;
            _text.text = Compose(player);
        }

        // ------------------------------------------------------- icone "Em combate"

        /// <summary>Status "Em combate" na barra de efeitos, como o mod Combat fazia.</summary>
        public static readonly int CombatStatusHash = "DH_Combat".GetStableHashCode();

        public static void RegisterStatusEffect()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_StatusEffects == null) return;
            if (db.m_StatusEffects.Exists(e => e != null && e.NameHash() == CombatStatusHash)) return;

            SE_Stats effect = ScriptableObject.CreateInstance<SE_Stats>();
            effect.name = "DH_Combat";
            effect.m_name = "Em combate";
            effect.m_tooltip = "Sem teleporte, retreat nem pedra de retorno ate a luta esfriar.";
            GameObject sword = db.GetItemPrefab("SwordBronze");
            effect.m_icon = sword != null ? sword.GetComponent<ItemDrop>()?.m_itemData.GetIcon() : null;
            db.m_StatusEffects.Add(effect);
        }

        private static void UpdateCombatIcon(Player player)
        {
            SEMan seman = player != null ? player.GetSEMan() : null;
            if (seman == null) return;
            StatusEffect current = seman.GetStatusEffect(CombatStatusHash);
            float remaining = PvpState.EscapeCombatRemaining;
            if (remaining <= 0f || !PvpConfig.CombatStatusIcon.Value)
            {
                if (current != null) seman.RemoveStatusEffect(CombatStatusHash, true);
                return;
            }
            if (current == null) current = seman.AddStatusEffect(CombatStatusHash, resetTime: true);
            // A contagem da barra e m_ttl - m_time: acompanha o relogio de combate do modulo.
            if (current != null) current.m_ttl = current.m_time + remaining;
        }

        private static bool EnsureText()
        {
            if (_text != null) return true;
            if (Hud.instance == null || Hud.instance.m_rootObject == null) return false;

            GameObject go = Ui.CreateText(string.Empty, Hud.instance.m_rootObject.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f),
                Ui.AveriaSerifBold, 18, Color.white, true, Color.black, 900f, 30f, false);
            go.name = "DeadheimPvpStatus";
            _text = go.GetComponent<Text>();
            _text.alignment = TextAnchor.MiddleCenter;
            _text.supportRichText = true;
            _text.raycastTarget = false;
            return true;
        }

        public static string Compose(Player player)
        {
            PvpFlags flags = PvpState.Current;
            List<string> parts = new List<string>();

            if ((flags & (PvpFlags.Arena | PvpFlags.Castle)) != 0)
                parts.Add("<color=#ff8c00>" + PvpState.ZoneLabel + "</color>");
            else if ((flags & PvpFlags.Hunted) != 0)
                parts.Add("<color=#ff5050>CACADO " + PvpClient.FormatDuration(PvpState.HuntedRemaining)
                          + (PvpClient.HuntPaused ? " (pausado)" : string.Empty) + "</color>");
            else if ((flags & PvpFlags.Immune) != 0)
                parts.Add("<color=#7fd4ff>IMUNE A PvP " + PvpClient.FormatDuration(PvpState.ImmuneRemaining) + "</color>");
            else if ((flags & PvpFlags.Protected) != 0)
                parts.Add("<color=#7CFC00>ZONA SEGURA: " + PvpState.ZoneLabel + "</color>");
            else if (player != null && player.IsPVPEnabled())
                parts.Add("<color=#ff5050>PvP ATIVO</color>");
            else
                parts.Add("<color=#c0c0c0>PvP desligado</color>");

            if ((flags & PvpFlags.Pk) != 0)
                parts.Add("<color=#ff3030>PK " + PvpClient.FormatDuration(PvpState.PkRemaining)
                          + (PvpState.PkCount > 1 ? $" (x{PvpState.PkCount})" : string.Empty) + "</color>");
            if ((flags & PvpFlags.Aggressor) != 0)
                parts.Add("<color=#ff7a3d>AGRESSOR</color>");
            if ((flags & PvpFlags.HuntPending) != 0)
                parts.Add("<color=#ff8c00>Desafio em " + PvpClient.FormatDuration(PvpState.HuntPendingRemaining) + "</color>");
            if ((flags & PvpFlags.Combat) != 0)
                parts.Add("<color=#ffb347>Em combate " + Mathf.CeilToInt(PvpState.CombatRemaining) + "s</color>");
            else if (PvpState.InEscapeCombat)
                parts.Add("<color=#ffb347>Em combate (PvE) " + Mathf.CeilToInt(PvpState.EscapeCombatRemaining) + "s</color>");

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) text.Append("  |  ");
                text.Append(parts[i]);
            }
            return text.ToString();
        }
    }

    /// <summary>Janela do /rank: top K/D e a sua linha. Tambem sai no chat.</summary>
    internal static class PvpRankPanel
    {
        private static GameObject _panel;
        private static float _closeAt;

        public static void Show(List<string> lines, string mine)
        {
            if (Chat.instance != null)
            {
                Chat.instance.AddString("<color=#ffd700>--- Ranking PvP (K/D) ---</color>");
                foreach (string line in lines) Chat.instance.AddString(line);
                Chat.instance.AddString("Voce: " + mine);
            }

            if (_panel != null) Object.Destroy(_panel);

            _panel = Ui.CreateWoodpanel(Ui.Front.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, 560f, 120f + 26f * (lines.Count + 2), false);
            _panel.name = "DeadheimPvpRank";

            Ui.CreateText("Ranking PvP (K/D)", _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), Ui.AveriaSerifBold, 26, Ui.ValheimOrange, true, Color.black, 520f, 36f, false)
                .GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            StringBuilder body = new StringBuilder();
            if (lines.Count == 0) body.AppendLine("Ninguem pontuou ainda.");
            foreach (string line in lines) body.AppendLine(line);
            body.AppendLine();
            body.Append("Voce: ").Append(mine);

            GameObject text = Ui.CreateText(body.ToString(), _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f - 13f * (lines.Count + 2)), Ui.AveriaSerifBold, 18, Color.white, true, Color.black,
                500f, 26f * (lines.Count + 2), false);
            Text label = text.GetComponent<Text>();
            label.alignment = TextAnchor.UpperLeft;
            label.supportRichText = true;

            GameObject close = Ui.CreateButton("Fechar", _panel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), 140f, 36f);
            close.GetComponent<Button>().onClick.AddListener(Close);

            _closeAt = Time.time + 20f;
        }

        public static void Close()
        {
            if (_panel != null) Object.Destroy(_panel);
            _panel = null;
        }

        public static void Update()
        {
            if (_panel == null) return;
            if (Time.time > _closeAt || Input.GetKeyDown(KeyCode.Escape)) Close();
        }
    }
}
