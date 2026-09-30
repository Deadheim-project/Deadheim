using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Deadheim.Pvp
{
    [Serializable]
    internal sealed class PvpPlayerRecord
    {
        public long id;
        public string name;
        public int kills;
        public int deaths;
        /// <summary>Segundos UTC ate quando e PK. Relogio so do servidor.</summary>
        public double pkUntil;
        /// <summary>PK permanente: so sai quando e morto por jogador.</summary>
        public bool pkPermanent;
        /// <summary>Abates que deram PK desde que a marca atual comecou: escolhe o nivel (PkTiers).</summary>
        public int pkStreak;
        /// <summary>Perda do nivel atual de PK (PvpConfig.PkPenalty).</summary>
        public int pkPenalty;
        /// <summary>Segundos UTC a partir de quando pode receber outra bounty.</summary>
        public double bountyReadyAt;
        /// <summary>Contador de PK: abates que deram PK (matar sem ser em defesa, arena ou castelo).</summary>
        public int pkKills;
        /// <summary>Deslogou em combate com CombatLogout=Death: morre ao voltar.</summary>
        public bool combatLogPending;
        /// <summary>Moedas a entregar quando ele voltar (bounty paga ou devolvida com ele offline).</summary>
        public int pendingCoins;

        public float Ratio => deaths <= 0 ? kills : (float)kills / deaths;

        public bool IsPk(double now) => pkPermanent || pkUntil > now;

        public void ClearPk()
        {
            pkUntil = 0d;
            pkPermanent = false;
            pkStreak = 0;
            pkPenalty = 0;
        }
    }

    [Serializable]
    internal sealed class PvpStoreData
    {
        public int version = 2;
        public List<PvpPlayerRecord> players = new List<PvpPlayerRecord>();
        public List<PvpBountyRecord> bounties = new List<PvpBountyRecord>();
    }

    /// <summary>
    /// Estado persistente do PvP no servidor: K/D, PK e cooldown de desafio. Um JSON
    /// por mundo em BepInEx/config/Deadheim, salvo junto com o mundo e a cada minuto se mudou.
    /// </summary>
    internal static class PvpStore
    {
        private static PvpStoreData _data;
        private static string _path;
        private static bool _dirty;
        private static float _nextSave;

        public static PvpStoreData Data
        {
            get
            {
                EnsureLoaded();
                return _data;
            }
        }

        public static void MarkDirty() => _dirty = true;

        private static string PathFor()
        {
            string world = ZNet.instance != null ? ZNet.instance.GetWorldName() : "world";
            foreach (char c in Path.GetInvalidFileNameChars()) world = world.Replace(c, '_');
            return Path.Combine(Path.Combine(Paths.ConfigPath, "Deadheim"), "pvp-" + world + ".json");
        }

        private static void EnsureLoaded()
        {
            string path = PathFor();
            if (_data != null && path == _path) return;

            if (_data != null && _dirty) Save();
            _path = path;
            _data = null;
            try
            {
                if (File.Exists(path)) _data = JsonUtility.FromJson<PvpStoreData>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                // Arquivo quebrado nao pode derrubar o servidor nem ser sobrescrito as cegas.
                string backup = path + ".corrompido-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                try { File.Copy(path, backup, true); } catch { }
                Debug.LogError($"[Deadheim PvP] {path} ilegivel ({ex.Message}); copia em {backup}, comecando vazio.");
            }

            if (_data == null) _data = new PvpStoreData();
            if (_data.players == null) _data.players = new List<PvpPlayerRecord>();
            if (_data.bounties == null) _data.bounties = new List<PvpBountyRecord>();
            foreach (PvpBountyRecord bounty in _data.bounties)
                if (bounty.contributions == null) bounty.contributions = new List<PvpBountyContribution>();
            _dirty = false;
            Debug.Log($"[Deadheim PvP] Estado carregado de {path}: {_data.players.Count} jogador(es).");
        }

        public static void Save()
        {
            if (_data == null || string.IsNullOrEmpty(_path)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                string temp = _path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(_data, true));
                // Copy + Delete e nao File.Replace: o servidor da DatHost roda Mono em Linux.
                File.Copy(temp, _path, true);
                File.Delete(temp);
                _dirty = false;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim PvP] Nao foi possivel salvar " + _path + ": " + ex.Message);
            }
        }

        public static void Tick()
        {
            if (!_dirty || Time.realtimeSinceStartup < _nextSave) return;
            _nextSave = Time.realtimeSinceStartup + 60f;
            Save();
        }

        public static void SaveIfDirty()
        {
            if (_dirty) Save();
        }

        /// <summary>Descarta o que esta em memoria (logout do host, troca de mundo).</summary>
        public static void Unload()
        {
            SaveIfDirty();
            _data = null;
            _path = null;
        }

        public static PvpPlayerRecord Player(long playerId, string name)
        {
            List<PvpPlayerRecord> players = Data.players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].id != playerId) continue;
                if (!string.IsNullOrEmpty(name) && players[i].name != name)
                {
                    players[i].name = name;
                    _dirty = true;
                }
                return players[i];
            }

            PvpPlayerRecord record = new PvpPlayerRecord { id = playerId, name = name ?? playerId.ToString() };
            players.Add(record);
            _dirty = true;
            return record;
        }
    }
}
