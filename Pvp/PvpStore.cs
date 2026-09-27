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
        /// <summary>Segundos UTC a partir de quando pode aceitar outro desafio.</summary>
        public double challengeReadyAt;

        public float Ratio => deaths <= 0 ? kills : (float)kills / deaths;
    }

    [Serializable]
    internal sealed class PvpClanRecord
    {
        public string name;
        public long leader;
        public List<long> members = new List<long>();
        public List<string> names = new List<string>();

        public string NameOf(long playerId)
        {
            int index = members.IndexOf(playerId);
            return index >= 0 && index < names.Count ? names[index] : playerId.ToString();
        }

        public void SetName(long playerId, string playerName)
        {
            int index = members.IndexOf(playerId);
            if (index < 0) return;
            while (names.Count < members.Count) names.Add(string.Empty);
            names[index] = playerName;
        }

        public void Remove(long playerId)
        {
            int index = members.IndexOf(playerId);
            if (index < 0) return;
            members.RemoveAt(index);
            if (index < names.Count) names.RemoveAt(index);
        }
    }

    [Serializable]
    internal sealed class PvpStoreData
    {
        public int version = 1;
        public List<PvpPlayerRecord> players = new List<PvpPlayerRecord>();
        public List<PvpClanRecord> clans = new List<PvpClanRecord>();
    }

    /// <summary>
    /// Estado persistente do PvP no servidor: K/D, PK, cooldown de desafio e clas. Um JSON
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
            if (_data.clans == null) _data.clans = new List<PvpClanRecord>();
            foreach (PvpClanRecord clan in _data.clans)
            {
                if (clan.members == null) clan.members = new List<long>();
                if (clan.names == null) clan.names = new List<string>();
            }
            _dirty = false;
            Debug.Log($"[Deadheim PvP] Estado carregado de {path}: {_data.players.Count} jogador(es), {_data.clans.Count} cla(s).");
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
