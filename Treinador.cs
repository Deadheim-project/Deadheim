using HarmonyLib;
using System;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// A porta dos buffs do NPC Treinador (NpcValheim) para o Deadheim. O NpcValheim liga por
    /// reflexao, sem referencia de compilacao, igual faz com a PvpApi: por isso so tipos
    /// primitivos e Version. Mudou o formato de algum metodo, sobe Version, e o NpcValheim que
    /// nao conhece a versao nova trata o Deadheim como ausente em vez de chamar as cegas.
    ///
    /// Quem cobra e decide e o servidor do NpcValheim (preco, Deadcoins, limite semanal). Daqui
    /// so sai o efeito no jogador local, com os numeros que o servidor mandou junto da compra:
    /// a skill e a morte rodam no cliente de quem joga, entao o buff tambem.
    /// </summary>
    public static class TreinadorApi
    {
        public const int Version = 1;

        /// <summary>Ha um personagem no mundo para receber o buff.</summary>
        public static bool Ready() => Player.m_localPlayer != null;

        /// <summary>
        /// Comeca (ou estende) o estudo: +bonus de XP (0.5 = +50%) nas skills abaixo de maxSkill,
        /// por minutes minutos de jogo. Devolve o motivo de nao ter aplicado, ou null.
        /// </summary>
        public static string GrantStudy(float bonus, float minutes, float maxSkill) =>
            Treinador.GrantStudy(Player.m_localPlayer, bonus, minutes, maxSkill);

        /// <summary>Segundos de estudo que faltam (0 = sem estudo).</summary>
        public static double LocalStudySeconds() => Treinador.StudyRemaining;

        public static float LocalStudyBonus() => Treinador.StudyActive ? Treinador.StudyBonus : 0f;

        public static float LocalStudyMaxSkill() => Treinador.StudyActive ? Treinador.StudyMaxSkill : 0f;

        /// <summary>Tempo maximo de estudo acumulado, em segundos.</summary>
        public static double MaxStudySeconds() => TreinadorRegras.MaxStudySeconds;

        /// <summary>
        /// Uma protecao: corta factor (0.5 = metade) da perda de skill da proxima morte em que
        /// valer. Com pveOnly so morte PvE; a pena de PK nunca e reduzida. Devolve o motivo de
        /// nao ter aplicado, ou null.
        /// </summary>
        public static string GrantProtection(float factor, bool pveOnly) =>
            Treinador.GrantProtection(Player.m_localPlayer, factor, pveOnly);

        /// <summary>Quantas mortes a protecao ainda cobre (0 = sem protecao).</summary>
        public static int LocalProtectionCharges() => Treinador.ProtectionCharges;

        public static float LocalProtectionFactor() => Treinador.ProtectionCharges > 0 ? Treinador.ProtectionCut : 0f;

        public static bool LocalProtectionPveOnly() => Treinador.ProtectionPveOnly;

        public static int MaxProtectionCharges() => TreinadorRegras.MaxProtectionCharges;

        /// <summary>
        /// O ganho de skill do jogador local antes do estudo, em fracao do vanilla:
        /// SkillMultiplier do servidor x o do lado (PvE ou PvP). E o numero que o estudo multiplica.
        /// </summary>
        public static float LocalSkillGain()
        {
            float gain = Plugin.SkillMultiplier != null ? Plugin.SkillMultiplier.Value : 1f;
            if (Pvp.PvpConfig.Active)
                gain *= Mathf.Max(0f, Pvp.PvpPve.IsLocal ? Pvp.PvpConfig.PveSkillMultiplier.Value : Pvp.PvpConfig.PvpSkillMultiplier.Value);
            return Mathf.Max(0f, gain);
        }
    }

    /// <summary>
    /// O estado dos buffs do Treinador no jogador local. Vive no personagem (m_customData), entao
    /// relogar ou morrer nao apaga: o estudo e gravado como segundos que faltam e so corre com o
    /// jogo aberto, como a imunidade do PvP; a protecao e um contador de mortes cobertas.
    ///
    /// Quem usa: o RaiseSkill do Patches.cs multiplica o ganho pelo estudo, e o DeathPatch e o
    /// SkillLossPatch do PvP decidem e aplicam a protecao. Cada buff aparece na barra de efeitos.
    /// </summary>
    internal static class Treinador
    {
        private const string KeyStudy = "dh_treinoEstudo";
        private const string KeyProtection = "dh_treinoProtecao";

        private static double _studyUntil;
        private static float _studyBonus;
        private static float _studyMaxSkill;
        private static int _charges;
        private static float _cut;
        private static bool _pveOnly = true;
        private static float _nextUpdate;

        /// <summary>Relogio monotonico (o mesmo do PvP): mudar a hora do Windows nao estica o estudo.</summary>
        private static double Mono => Time.realtimeSinceStartupAsDouble;

        public static double StudyRemaining => Math.Max(0d, _studyUntil - Mono);
        public static bool StudyActive => StudyRemaining > 0d;
        public static float StudyBonus => _studyBonus;
        public static float StudyMaxSkill => _studyMaxSkill;
        public static int ProtectionCharges => _charges;
        public static float ProtectionCut => _cut;
        public static bool ProtectionPveOnly => _pveOnly;

        public static void Init()
        {
            Vanilla.Prefabs.PiecesReady += RegisterStatusEffects;
        }

        // ----------------------------------------------------------------- compra

        public static string GrantStudy(Player player, float bonus, float minutes, float maxSkill)
        {
            if (player == null || player != Player.m_localPlayer) return "Sem personagem no mundo.";
            if (float.IsNaN(minutes) || minutes <= 0f) return "Duracao invalida.";
            bonus = TreinadorRegras.ClampBonus(bonus);
            maxSkill = TreinadorRegras.ClampLevel(maxSkill);
            if (bonus <= 0f || maxSkill <= 0f) return "Estudo sem efeito.";

            double left = TreinadorRegras.ClampSeconds(StudyRemaining + minutes * 60d);
            _studyUntil = Mono + left;
            _studyBonus = bonus;
            _studyMaxSkill = maxSkill;
            Save(player);
            _nextUpdate = 0f;
            Debug.Log($"[Deadheim Treinador] Estudo: +{bonus * 100f:0}% ate skill {maxSkill:0}, {left / 60d:0.#} min");
            return null;
        }

        public static string GrantProtection(Player player, float factor, bool pveOnly)
        {
            if (player == null || player != Player.m_localPlayer) return "Sem personagem no mundo.";
            factor = TreinadorRegras.ClampFactor(factor);
            if (factor <= 0f) return "Protecao sem efeito.";
            if (_charges >= TreinadorRegras.MaxProtectionCharges)
                return $"Voce ja guarda {TreinadorRegras.MaxProtectionCharges} protecoes.";

            _charges++;
            _cut = factor;
            _pveOnly = pveOnly;
            Save(player);
            _nextUpdate = 0f;
            Debug.Log($"[Deadheim Treinador] Protecao: corta {factor * 100f:0}% ({(pveOnly ? "so PvE" : "PvE e PvP")}), {_charges} carga(s)");
            return null;
        }

        // ------------------------------------------------------------------ efeito

        /// <summary>Quanto o estudo multiplica um ganho de skill. So conta as skills do jogador local.</summary>
        public static float StudyMultiplier(Skills skills, float level)
        {
            Player local = Player.m_localPlayer;
            if (local == null || skills == null || !ReferenceEquals(skills, local.GetSkills())) return 1f;
            return TreinadorRegras.StudyMultiplier(StudyActive, level, _studyMaxSkill, _studyBonus);
        }

        /// <summary>Se a protecao cobre a morte que esta acontecendo agora (que tira skill).</summary>
        public static bool Covers(bool byPlayer, bool pkPays) =>
            TreinadorRegras.Covers(_charges, byPlayer, pkPays, _pveOnly);

        /// <summary>Gasta uma protecao: a morte coberta ja esta tirando menos skill.</summary>
        public static void ConsumeProtection(Player player)
        {
            if (_charges <= 0) return;
            _charges--;
            Save(player);
            _nextUpdate = 0f;
        }

        // -------------------------------------------------------- gravado no personagem

        private static void Load(Player player)
        {
            ResetSession();
            if (player == null) return;

            if (player.m_customData.TryGetValue(KeyStudy, out string study)
                && TreinadorRegras.TryReadStudy(study, out double left, out float bonus, out float max) && left > 0d)
            {
                _studyUntil = Mono + left;
                _studyBonus = bonus;
                _studyMaxSkill = max;
            }
            if (player.m_customData.TryGetValue(KeyProtection, out string protection)
                && TreinadorRegras.TryReadProtection(protection, out int charges, out float cut, out bool pveOnly))
            {
                _charges = charges;
                _cut = cut;
                _pveOnly = pveOnly;
            }
            Save(player);
        }

        /// <summary>Grava no personagem o que falta (antes de o perfil ser salvo, e a cada mudanca).</summary>
        private static void Save(Player player)
        {
            if (player == null) return;
            double left = StudyRemaining;
            if (left > 0d) player.m_customData[KeyStudy] = TreinadorRegras.WriteStudy(left, _studyBonus, _studyMaxSkill);
            else player.m_customData.Remove(KeyStudy);

            if (_charges > 0) player.m_customData[KeyProtection] = TreinadorRegras.WriteProtection(_charges, _cut, _pveOnly);
            else player.m_customData.Remove(KeyProtection);
        }

        private static void ResetSession()
        {
            _studyUntil = 0d;
            _studyBonus = 0f;
            _studyMaxSkill = 0f;
            _charges = 0;
            _cut = 0f;
            _pveOnly = true;
            _nextUpdate = 0f;
        }

        // ------------------------------------------------------------- barra de efeitos

        private static readonly int StudyHash = "DH_TreinoEstudo".GetStableHashCode();
        private static readonly int ProtectionHash = "DH_TreinoProtecao".GetStableHashCode();

        private static void RegisterStatusEffects()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_StatusEffects == null) return;
            Register(db, "DH_TreinoEstudo", "Estudo",
                "Treinador: mais XP de skill nas skills abaixo do teto. O tempo so corre com o jogo aberto.",
                "se:Rested", "Feathers");
            Register(db, "DH_TreinoProtecao", "Protecao",
                "Treinador: a proxima morte que valer tira menos skill. A pena de PK nunca e reduzida.",
                "se:SoftDeath", "ShieldWood");
        }

        private static void Register(ObjectDB db, string id, string name, string tooltip, params string[] icons)
        {
            int hash = id.GetStableHashCode();
            if (db.m_StatusEffects.Exists(e => e != null && e.NameHash() == hash)) return;
            SE_Stats effect = ScriptableObject.CreateInstance<SE_Stats>();
            effect.name = id;
            effect.m_name = name;
            effect.m_tooltip = tooltip;
            effect.m_icon = IconFor(db, icons);
            db.m_StatusEffects.Add(effect);
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

        public static void Update(Player player)
        {
            if (player == null || player != Player.m_localPlayer || Time.time < _nextUpdate) return;
            _nextUpdate = Time.time + 0.25f;

            if (_studyUntil > 0d && !StudyActive)
            {
                _studyUntil = 0d;
                Save(player);
                player.Message(MessageHud.MessageType.TopLeft, "O estudo do Treinador acabou.");
            }

            SEMan seman = player.GetSEMan();
            if (seman == null) return;
            Show(seman, StudyHash, StudyActive, (float)StudyRemaining, $"Estudo +{_studyBonus * 100f:0}% (ate {_studyMaxSkill:0})");
            Show(seman, ProtectionHash, _charges > 0, 0f,
                $"Protecao -{_cut * 100f:0}%" + (_charges > 1 ? $" x{_charges}" : string.Empty));
        }

        /// <summary>
        /// Como o PvpHud.Show: sem tempo o m_ttl fica 0 e o jogo nao conta nem tira o buff. O
        /// tempo decorrido vem de GetDuration(), que e publico: o m_time e protegido no jogo.
        /// </summary>
        private static void Show(SEMan seman, int hash, bool active, float remaining, string name)
        {
            StatusEffect current = seman.GetStatusEffect(hash);
            if (!active)
            {
                if (current != null) seman.RemoveStatusEffect(hash, true);
                return;
            }
            if (current == null) current = seman.AddStatusEffect(hash, resetTime: true);
            if (current == null) return;
            current.m_name = name;
            current.m_ttl = remaining > 0f ? current.GetDuration() + remaining : 0f;
        }

        // ----------------------------------------------------------------- ciclo de vida

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class SpawnPatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance == Player.m_localPlayer) Load(__instance);
            }
        }

        /// <summary>O perfil vai ser salvo (autosave, logout, respawn): grava o que falta do estudo.</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.Save))]
        private static class SavePatch
        {
            private static void Prefix(Player __instance)
            {
                if (__instance != null && __instance == Player.m_localPlayer) Save(__instance);
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private static class LogoutPatch
        {
            private static void Prefix()
            {
                Player player = Player.m_localPlayer;
                if (player != null) Save(player);
                ResetSession();
            }
        }
    }
}
