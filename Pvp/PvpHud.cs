using Deadheim.Vanilla;
using Deadheim.Shared;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim.Pvp
{
    /// <summary>
    /// O estado de PvP do jogador na tela: cada estado (em combate, imune, PK, agressor, cacado,
    /// bounty, zona segura, PvE) e um buff na barra de efeitos do jogo, com contagem; e uma linha
    /// no topo da tela resume tudo (PvP ativo ou nao, e por que). Os dois ficam no HUD do jogo,
    /// entao somem junto quando o jogador esconde o HUD.
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
                ? Avisos.Pvp(L.T("pvphud.pvp_ativo_t"), string.IsNullOrEmpty(why) ? L.T("pvphud.cuidado") : why)
                : Avisos.Ok(L.T("pvphud.pvp_off_t"), string.IsNullOrEmpty(why) ? L.T("pvphud.ninguem_ataca") : why));
        }

        public static void ResetSession()
        {
            _announced = false;
            _buffsShown = false;
            if (_text != null) Object.Destroy(_text.gameObject);
            _text = null;
        }

        public static void Update(Player player)
        {
            if (Time.time < _nextRefresh) return;
            _nextRefresh = Time.time + 0.25f;

            UpdateBuffs(player);
            if (!EnsureText()) return;
            _text.text = Compose(player);
        }

        // ------------------------------------------------------------------ buffs

        /// <summary>Um estado do PvP como buff do jogo. O icone vem de um efeito ou item que o jogo ja tem.</summary>
        private sealed class Buff
        {
            public readonly string Id;
            public readonly int Hash;
            private readonly string _nameKey;
            public string Name => L.T(_nameKey);
            private readonly string _tipKey;
            public string Tooltip => L.T(_tipKey);
            public readonly string[] Icons;

            public Buff(string id, string name, string tooltip, params string[] icons)
            {
                Id = id;
                Hash = id.GetStableHashCode();
                _nameKey = name;
                _tipKey = tooltip;
                Icons = icons;
            }
        }

        // Icones: "se:Nome" e um efeito do jogo, o resto e prefab de item. O primeiro que existir vale.
        private static readonly Buff Combat = new Buff("DH_Combat", "pvphud.b_combat",
            "pvphud.t_combat", "SwordBronze");
        private static readonly Buff Immune = new Buff("DH_PvpImmune", "pvphud.b_immune",
            "pvphud.t_immune", "ShieldWood", "SwordBronze");
        private static readonly Buff Pk = new Buff("DH_Pk", "pvphud.b_pk",
            "pvphud.t_pk",
            "TrophySkeleton", "SwordBronze");
        private static readonly Buff Aggressor = new Buff("DH_Aggressor", "pvphud.b_aggressor",
            "pvphud.t_aggressor", "AxeStone", "SwordBronze");
        private static readonly Buff Hunted = new Buff("DH_Hunted", "pvphud.b_hunted",
            "pvphud.t_hunted", "Bow", "SwordBronze");
        private static readonly Buff Bounty = new Buff("DH_Bounty", "pvphud.b_bounty",
            "pvphud.t_bounty", "Coins", "SwordBronze");
        private static readonly Buff Safe = new Buff("DH_SafeZone", "pvphud.b_safe",
            "pvphud.t_safe", "se:Shelter", "ShieldWood", "SwordBronze");
        private static readonly Buff Pve = new Buff("DH_Pve", "pvphud.b_pve",
            "pvphud.t_pve", "Hammer", "SwordBronze");

        // Recargas de teleporte: um buff por cooldown (PvpModule.Cooldown), com a contagem do que falta.
        private static readonly Buff CdHearthstone = new Buff("DH_CdHearthstone", "pvphud.b_cdhs",
            "pvphud.t_cdhs", "Hearthstone", "SurtlingCore", "SwordBronze");
        private static readonly Buff CdRetreat1 = new Buff("DH_CdRetreat1", "pvphud.b_cdr1",
            "pvphud.t_cdr1", "Ruby", "Hearthstone", "SwordBronze");
        private static readonly Buff CdRetreat2 = new Buff("DH_CdRetreat2", "pvphud.b_cdr2",
            "pvphud.t_cdr2", "Amber", "Hearthstone", "SwordBronze");

        private static readonly Buff[] All = { Combat, Immune, Pk, Aggressor, Hunted, Bounty, Safe, Pve, CdHearthstone, CdRetreat1, CdRetreat2 };

        /// <summary>Hash do buff de cada recarga, para o teste conferir na barra de efeitos.</summary>
        public static int CooldownHash(PvpModule.Cooldown kind) =>
            kind == PvpModule.Cooldown.Hearthstone ? CdHearthstone.Hash : kind == PvpModule.Cooldown.Retreat2 ? CdRetreat2.Hash : CdRetreat1.Hash;

        /// <summary>Hash dos buffs, para o teste conferir na barra de efeitos.</summary>
        public static int CombatHash => Combat.Hash;
        public static int ImmuneHash => Immune.Hash;
        public static int PkHash => Pk.Hash;
        public static int AggressorHash => Aggressor.Hash;
        public static int HuntedHash => Hunted.Hash;
        public static int BountyHash => Bounty.Hash;
        public static int SafeHash => Safe.Hash;
        public static int PveHash => Pve.Hash;

        public static void RegisterStatusEffects()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_StatusEffects == null) return;
            foreach (Buff buff in All)
            {
                if (db.m_StatusEffects.Exists(e => e != null && e.NameHash() == buff.Hash)) continue;
                SE_Stats effect = ScriptableObject.CreateInstance<SE_Stats>();
                effect.name = buff.Id;
                effect.m_name = buff.Name;
                effect.m_tooltip = buff.Tooltip;
                effect.m_icon = IconFor(db, buff.Icons);
                db.m_StatusEffects.Add(effect);
            }
        }

        private static Sprite IconFor(ObjectDB db, string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                Sprite icon = null;
                if (candidate.StartsWith("se:"))
                    icon = db.GetStatusEffect(candidate.Substring(3).GetStableHashCode())?.m_icon;
                else
                {
                    GameObject item = db.GetItemPrefab(candidate);
                    icon = item != null ? item.GetComponent<ItemDrop>()?.m_itemData.GetIcon() : null;
                }
                if (icon != null) return icon;
            }
            return null;
        }

        private static bool _buffsShown;

        /// <summary>Tira todos os buffs do PvP (modulo desligado ao vivo).</summary>
        public static void ClearBuffs(Player player)
        {
            if (!_buffsShown) return;
            _buffsShown = false;
            SEMan seman = player != null ? player.GetSEMan() : null;
            if (seman == null) return;
            foreach (Buff buff in All)
                if (seman.GetStatusEffect(buff.Hash) != null) seman.RemoveStatusEffect(buff.Hash, true);
        }

        /// <summary>Liga, desliga e acerta a contagem de cada buff pelo estado que o modulo ja decidiu.</summary>
        private static void UpdateBuffs(Player player)
        {
            SEMan seman = player != null ? player.GetSEMan() : null;
            if (seman == null) return;
            _buffsShown = true;
            PvpFlags flags = PvpState.Current;
            bool states = PvpConfig.StateBuffs == null || PvpConfig.StateBuffs.Value;

            float combat = PvpConfig.CombatStatusIcon.Value ? PvpState.ShownCombatRemaining : 0f;
            Show(seman, Combat, combat > 0f, combat, PvpState.InCombat ? L.T("pvphud.em_combate") : L.T("pvphud.luta_monstro"));

            Show(seman, Immune, states && (flags & PvpFlags.Immune) != 0, (float)PvpState.ImmuneRemaining);
            bool permanent = (flags & PvpFlags.PkPermanent) != 0;
            int pkCount = PvpState.PkCount;
            Show(seman, Pk, states && (flags & PvpFlags.Pk) != 0, permanent ? 0f : (float)PvpState.PkRemaining,
                (permanent ? L.T("pvphud.pk_perm") : "PK") + (pkCount > 1 ? $" x{pkCount}" : string.Empty));
            Show(seman, Aggressor, states && (flags & PvpFlags.Aggressor) != 0, PvpState.AggressorRemaining);
            Show(seman, Hunted, states && (flags & PvpFlags.Hunted) != 0, PvpState.IsHuntedForever ? 0f : (float)PvpState.HuntedRemaining,
                (PvpState.IsHuntedForever ? L.T("pvphud.cacado_morrer") : L.T("pvphud.cacado")) + (PvpClient.HuntPaused ? L.T("pvphud.pausado") : string.Empty));
            Show(seman, Bounty, states && (flags & PvpFlags.HuntPending) != 0, (float)PvpState.HuntPendingRemaining,
                L.T("pvphud.bounty_n", PvpClient.BountyPot) + (PvpClient.HuntPaused ? L.T("pvphud.pausada") : string.Empty));
            Show(seman, Safe, states && (flags & PvpFlags.Protected) != 0, 0f, PvpState.ZoneLabel ?? L.T("pvphud.zona_segura"));
            Show(seman, Pve, states && (flags & PvpFlags.Pve) != 0, 0f, PvpPve.Title);

            // Recargas de teleporte: reaparecem sozinhas no login enquanto faltar tempo (carregadas do perfil).
            float hs = (float)PvpModule.Remaining(PvpModule.Cooldown.Hearthstone);
            Show(seman, CdHearthstone, hs > 0f, hs);
            float r1 = (float)PvpModule.Remaining(PvpModule.Cooldown.Retreat1);
            Show(seman, CdRetreat1, r1 > 0f, r1);
            float r2 = (float)PvpModule.Remaining(PvpModule.Cooldown.Retreat2);
            Show(seman, CdRetreat2, r2 > 0f, r2);
        }

        /// <summary>
        /// Sem tempo (permanente, zona) o m_ttl fica 0: o jogo nao mostra contagem nem tira o buff.
        /// Com tempo, a contagem da barra e m_ttl - m_time e acompanha o relogio do modulo.
        /// </summary>
        private static void Show(SEMan seman, Buff buff, bool active, float remaining, string name = null)
        {
            StatusEffect current = seman.GetStatusEffect(buff.Hash);
            if (!active)
            {
                if (current != null) seman.RemoveStatusEffect(buff.Hash, true);
                return;
            }
            if (current == null) current = seman.AddStatusEffect(buff.Hash, resetTime: true);
            if (current == null) return;
            current.m_name = name ?? buff.Name;
            current.m_ttl = remaining > 0f ? current.m_time + remaining : 0f;
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

            if ((flags & PvpFlags.Pve) != 0)
                parts.Add(L.T("pvphud.h_pve", PvpPve.Title));
            else if ((flags & (PvpFlags.Arena | PvpFlags.Castle)) != 0)
                parts.Add("<color=#ff8c00>" + PvpState.ZoneLabel + "</color>");
            else if ((flags & PvpFlags.Hunted) != 0)
                parts.Add(L.T("pvphud.h_cacado", PvpState.IsHuntedForever ? L.T("pvphud.ate_morrer") : PvpClient.FormatDuration(PvpState.HuntedRemaining), PvpClient.BountyPot, PvpClient.HuntPaused ? L.T("pvphud.pausado") : string.Empty));
            else if ((flags & PvpFlags.Immune) != 0)
                parts.Add(L.T("pvphud.h_imune", PvpClient.FormatDuration(PvpState.ImmuneRemaining)));
            else if ((flags & PvpFlags.Protected) != 0)
                parts.Add(L.T("pvphud.h_zona", PvpState.ZoneLabel));
            else if (player != null && player.IsPVPEnabled())
                parts.Add(L.T("pvphud.h_ativo"));
            else
                parts.Add(L.T("pvphud.h_desl"));

            if ((flags & PvpFlags.PkPermanent) != 0)
                parts.Add(L.T("pvphud.h_pkperm", PvpState.PkCount > 1 ? $" (x{PvpState.PkCount})" : string.Empty));
            else if ((flags & PvpFlags.Pk) != 0)
                parts.Add(L.T("pvphud.h_pk", PvpClient.FormatDuration(PvpState.PkRemaining), PvpState.PkCount > 1 ? $" (x{PvpState.PkCount})" : string.Empty));
            if ((flags & PvpFlags.Aggressor) != 0)
                parts.Add(L.T("pvphud.h_agressor"));
            if ((flags & PvpFlags.HuntPending) != 0)
                parts.Add(L.T("pvphud.h_pend", PvpClient.BountyPot, PvpClient.FormatDuration(PvpState.HuntPendingRemaining), PvpClient.HuntPaused ? L.T("pvphud.pausado") : string.Empty));
            if ((flags & PvpFlags.Combat) != 0)
                parts.Add(L.T("pvphud.h_comb", Mathf.CeilToInt(PvpState.CombatRemaining)));
            else if (PvpState.PveCombatBlocks)
                parts.Add(L.T("pvphud.h_monstro", Mathf.CeilToInt(PvpState.PveCombatRemaining)));

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
                Chat.instance.AddString("<color=#ffd700>" + L.T("pvphud.rank_titulo") + "</color>");
                foreach (string line in lines) Chat.instance.AddString(line);
                Chat.instance.AddString(L.T("pvphud.voce", mine));
            }

            if (_panel != null) Object.Destroy(_panel);

            _panel = Ui.CreateWoodpanel(Ui.Front.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, 560f, 120f + 26f * (lines.Count + 2), false);
            _panel.name = "DeadheimPvpRank";

            Ui.CreateText(L.T("pvphud.rank_painel"), _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), Ui.AveriaSerifBold, 26, Ui.ValheimOrange, true, Color.black, 520f, 36f, false)
                .GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            StringBuilder body = new StringBuilder();
            if (lines.Count == 0) body.AppendLine(L.T("pvphud.ninguem_pont"));
            foreach (string line in lines) body.AppendLine(line);
            body.AppendLine();
            body.Append(L.T("pvphud.voce", mine));

            GameObject text = Ui.CreateText(body.ToString(), _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f - 13f * (lines.Count + 2)), Ui.AveriaSerifBold, 18, Color.white, true, Color.black,
                500f, 26f * (lines.Count + 2), false);
            Text label = text.GetComponent<Text>();
            label.alignment = TextAnchor.UpperLeft;
            label.supportRichText = true;

            GameObject close = Ui.CreateButton(L.T("pvphud.fechar"), _panel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
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
