using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Estado persistente do PvP no servidor: K/D, PK, bounties e PvE permanente. Um arquivo
    /// de texto por mundo em BepInEx/config/Deadheim (formato em PvpStoreFormat), salvo junto
    /// com o mundo e a cada minuto se mudou.
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

        private static string WorldFileName(string extension)
        {
            string world = ZNet.instance != null ? ZNet.instance.GetWorldName() : "world";
            foreach (char c in Path.GetInvalidFileNameChars()) world = world.Replace(c, '_');
            return Path.Combine(Path.Combine(Paths.ConfigPath, "Deadheim"), "pvp-" + world + extension);
        }

        private static void EnsureLoaded()
        {
            string path = WorldFileName(".txt");
            if (_data != null && path == _path) return;

            if (_data != null && _dirty) Save();
            _path = path;
            _data = null;
            int skipped = 0;
            try
            {
                if (File.Exists(path)) _data = PvpStoreFormat.Read(File.ReadAllText(path), out skipped);
                else if (File.Exists(WorldFileName(".json")))
                    // O formato antigo (JsonUtility) so gravava o "version": nao ha nada para trazer.
                    Debug.LogWarning($"[Deadheim PvP] {WorldFileName(".json")} e do formato antigo, que nao guardava jogadores nem bounties; ignorado.");
            }
            catch (Exception ex)
            {
                // Arquivo quebrado nao pode derrubar o servidor nem ser sobrescrito as cegas.
                string backup = path + ".corrompido-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                try { File.Copy(path, backup, true); } catch { }
                Debug.LogError($"[Deadheim PvP] {path} ilegivel ({ex.Message}); copia em {backup}, comecando vazio.");
                _data = null;
            }

            if (_data == null) _data = new PvpStoreData();
            _dirty = false;
            Debug.Log($"[Deadheim PvP] Estado carregado de {path}: {_data.players.Count} jogador(es), " +
                      $"{_data.bounties.Count} bounty(ies)" + (skipped > 0 ? $", {skipped} linha(s) ignorada(s)" : string.Empty) + ".");
        }

        public static void Save()
        {
            if (_data == null || string.IsNullOrEmpty(_path)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                string temp = _path + ".tmp";
                File.WriteAllText(temp, PvpStoreFormat.Write(_data));
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
