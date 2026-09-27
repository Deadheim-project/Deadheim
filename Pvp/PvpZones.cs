using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Onde o PvP vale e onde nao vale: ilha inicial, zonas seguras extras, arenas e
    /// transportes. Tudo aqui e deterministico a partir da config e do gerador do mundo,
    /// entao cliente e servidor chegam na mesma resposta sem trocar mensagem.
    /// </summary>
    internal static class PvpZones
    {
        public const string StartLocation = "StartTemple";

        // Resolucao da mascara da ilha. 32 m fecha rio estreito depois da dilatacao
        // e mantem a varredura em ~9 mil amostras para um raio de 1500 m.
        private const float Cell = 32f;

        private static List<PvpConfig.Zone> _safe;
        private static List<PvpConfig.Zone> _arenas;

        private static bool _islandReady;
        private static bool _islandUnavailable;
        private static float _nextIslandTry;
        private static Vector2 _islandCenter;
        private static int _islandHalf;
        private static bool[] _islandMask;
        private static int _islandCells;
        private static string _islandBiomes = "";
        private static long _worldUid;

        public static void Invalidate()
        {
            _safe = null;
            _arenas = null;
            _islandReady = false;
            _islandUnavailable = false;
            _islandMask = null;
            _nextIslandTry = 0f;
        }

        private static List<PvpConfig.Zone> Safe
            => _safe ?? (_safe = PvpConfig.ParseZones(PvpConfig.SafeZones.Value, "SafeZones"));

        private static List<PvpConfig.Zone> Arenas
            => _arenas ?? (_arenas = PvpConfig.ParseZones(PvpConfig.ArenaZones.Value, "ArenaZones"));

        // ------------------------------------------------------------------ consultas

        public static bool IsArena(Vector3 point) => FindZone(Arenas, point) != null;

        public static string ArenaName(Vector3 point) => FindZone(Arenas, point)?.Name;

        /// <summary>Zona segura geografica: ilha inicial ou zona extra. Nao inclui transporte.</summary>
        public static bool IsSafeArea(Vector3 point) => SafeAreaName(point) != null;

        public static string SafeAreaName(Vector3 point)
        {
            if (IsArena(point)) return null;
            if (IsOnStartIsland(point)) return "Ilha Inicial";
            return FindZone(Safe, point)?.Name;
        }

        private static PvpConfig.Zone FindZone(List<PvpConfig.Zone> zones, Vector3 point)
        {
            for (int i = 0; i < zones.Count; i++)
                if (zones[i].Contains(point)) return zones[i];
            return null;
        }

        /// <summary>
        /// Barco andando (dentro do volume do navio, de pe no convés ou no leme), montaria ou
        /// puxando carroca. So faz sentido para o jogador local: e ele quem decide a
        /// propria bandeira de PvP.
        /// </summary>
        public static bool IsOnTransport(Player player)
        {
            if (player == null) return false;
            try
            {
                if (player.InNumShipVolumes > 0 || player.IsAttachedToShip() || player.GetStandingOnShip() != null)
                    return ShipIsMoving(player);
                if (player.IsRiding()) return MountIsMoving(player);

                foreach (Vagon vagon in Vagon.m_instances)
                {
                    if (vagon == null || vagon.m_attachJoin == null) continue;
                    Rigidbody body = vagon.m_attachJoin.connectedBody;
                    if (body != null && body.gameObject == player.gameObject) return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Deadheim PvP] Checagem de transporte falhou: " + ex.Message);
            }
            return false;
        }

        /// <summary>
        /// Barco parado ou encalhado nao e abrigo (ShipSafeMinSpeed): senao qualquer barco na
        /// praia, ate de inimigo, viraria bunker ao lado de uma base.
        /// </summary>
        private static bool ShipIsMoving(Player player)
        {
            float minimum = PvpConfig.ShipSafeMinSpeed.Value;
            if (minimum <= 0f) return true;

            Ship ship = player.GetStandingOnShip() ?? player.GetControlledShip();
            if (ship == null)
                foreach (Ship candidate in Ship.s_currentShips)
                    if (candidate != null && candidate.m_players.Contains(player))
                    {
                        ship = candidate;
                        break;
                    }
            if (ship == null || ship.m_body == null) return false;
            return ship.m_body.linearVelocity.magnitude >= minimum;
        }

        /// <summary>Montaria parada nao e abrigo (MountSafeMinSpeed), igual barco parado.</summary>
        private static bool MountIsMoving(Player player)
        {
            float minimum = PvpConfig.MountSafeMinSpeed.Value;
            if (minimum <= 0f) return true;
            Character mount = (player.m_doodadController as Sadle)?.GetCharacter();
            return mount != null && mount.GetVelocity().magnitude >= minimum;
        }

        // ---------------------------------------------------------------- ilha inicial

        public static bool IsOnStartIsland(Vector3 point)
        {
            string mode = PvpConfig.StartIslandMode.Value;
            if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase)) return false;
            if (!EnsureIsland()) return false;

            float radius = PvpConfig.StartIslandRadius.Value;
            Vector2 p = new Vector2(point.x, point.z);
            if ((p - _islandCenter).sqrMagnitude > radius * radius) return false;
            if (string.Equals(mode, "Radius", StringComparison.OrdinalIgnoreCase)) return true;

            int size = _islandHalf * 2 + 1;
            int ix = Mathf.RoundToInt((p.x - _islandCenter.x) / Cell) + _islandHalf;
            int iz = Mathf.RoundToInt((p.y - _islandCenter.y) / Cell) + _islandHalf;
            if (ix < 0 || iz < 0 || ix >= size || iz >= size) return false;
            return _islandMask[iz * size + ix];
        }

        public static bool TryGetStartCenter(out Vector3 center)
        {
            center = Vector3.zero;
            if (ZoneSystem.instance == null || ZNet.instance == null) return false;
            return ZoneSystem.instance.GetLocationIcon(StartLocation, out center);
        }

        public static string DescribeIsland()
        {
            if (!EnsureIsland()) return "ilha inicial ainda nao calculada (templo inicial desconhecido)";
            return $"centro=({_islandCenter.x:F0},{_islandCenter.y:F0}) raio={PvpConfig.StartIslandRadius.Value} " +
                   $"modo={PvpConfig.StartIslandMode.Value} celulas={_islandCells} (~{_islandCells * Cell * Cell / 1e6f:F2} km2) " +
                   $"biomas: {_islandBiomes}";
        }

        private static bool EnsureIsland()
        {
            if (WorldGenerator.instance == null || ZNet.instance == null) return false;

            // Outro mundo (logout e login em outro servidor): recalcula.
            long uid = ZNet.m_world != null ? ZNet.m_world.m_uid : 0L;
            if (uid != _worldUid)
            {
                _worldUid = uid;
                _islandReady = false;
                _islandUnavailable = false;
                _islandMask = null;
                _nextIslandTry = 0f;
            }

            if (_islandReady) return true;
            if (_islandUnavailable || Time.realtimeSinceStartup < _nextIslandTry) return false;
            _nextIslandTry = Time.realtimeSinceStartup + 2f;

            if (!TryGetStartCenter(out Vector3 temple)) return false;

            try
            {
                BuildIsland(new Vector2(temple.x, temple.z));
                _islandReady = true;
                Debug.Log("[Deadheim PvP] Zona segura inicial: " + DescribeIsland());
            }
            catch (Exception ex)
            {
                _islandUnavailable = true;
                Debug.LogError("[Deadheim PvP] Nao foi possivel calcular a ilha inicial: " + ex);
            }
            return _islandReady;
        }

        /// <summary>
        /// Terra ligada ao templo, dentro do raio. Terreno acima do nivel do mar vira
        /// terra; uma dilatacao de uma celula fecha rios estreitos antes do flood fill e
        /// outra, depois, cobre a faixa de praia onde os barcos encostam.
        /// </summary>
        private static void BuildIsland(Vector2 center)
        {
            _islandCenter = center;
            float radius = Mathf.Max(Cell, PvpConfig.StartIslandRadius.Value);
            _islandHalf = Mathf.CeilToInt(radius / Cell);
            int size = _islandHalf * 2 + 1;
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;

            HashSet<Heightmap.Biome> allowed = AllowedBiomes();
            bool[] land = new bool[size * size];
            for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
            {
                float wx = center.x + (x - _islandHalf) * Cell;
                float wz = center.y + (z - _islandHalf) * Cell;
                land[z * size + x] = WorldGenerator.instance.GetHeight(wx, wz) > water - 1f
                                     && (allowed == null || allowed.Contains(WorldGenerator.instance.GetBiome(wx, wz)));
            }

            bool[] bridged = Dilate(land, size);

            bool[] reached = new bool[size * size];
            Queue<int> open = new Queue<int>();
            int start = _islandHalf * size + _islandHalf;
            reached[start] = true;
            open.Enqueue(start);
            float maxCells = radius / Cell;

            while (open.Count > 0)
            {
                int cell = open.Dequeue();
                int cx = cell % size, cz = cell / size;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int nz = cz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                    int next = nz * size + nx;
                    if (reached[next] || !bridged[next]) continue;
                    float dx = nx - _islandHalf, dz = nz - _islandHalf;
                    if (dx * dx + dz * dz > maxCells * maxCells) continue;
                    reached[next] = true;
                    open.Enqueue(next);
                }
            }

            _islandMask = Dilate(reached, size);
            _islandCells = 0;
            Dictionary<Heightmap.Biome, int> biomes = new Dictionary<Heightmap.Biome, int>();
            for (int i = 0; i < _islandMask.Length; i++)
            {
                if (!_islandMask[i]) continue;
                _islandCells++;
                // O admin precisa saber o que a zona segura cobre: se passa da Campina, da
                // para evoluir inteiro sem sair dela.
                Heightmap.Biome biome = WorldGenerator.instance.GetBiome(
                    center.x + (i % size - _islandHalf) * Cell, center.y + (i / size - _islandHalf) * Cell);
                biomes[biome] = biomes.TryGetValue(biome, out int n) ? n + 1 : 1;
            }
            List<KeyValuePair<Heightmap.Biome, int>> sorted = new List<KeyValuePair<Heightmap.Biome, int>>(biomes);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            List<string> parts = new List<string>();
            foreach (KeyValuePair<Heightmap.Biome, int> entry in sorted)
                parts.Add($"{entry.Key} {entry.Value * 100f / Mathf.Max(1, _islandCells):0}%");
            _islandBiomes = string.Join(", ", parts);
        }

        /// <summary>StartIslandBiomes: null = qualquer bioma.</summary>
        private static HashSet<Heightmap.Biome> AllowedBiomes()
        {
            string raw = PvpConfig.StartIslandBiomes.Value;
            if (string.IsNullOrWhiteSpace(raw)) return null;
            HashSet<Heightmap.Biome> result = new HashSet<Heightmap.Biome>();
            foreach (string name in raw.Split(','))
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                try { result.Add((Heightmap.Biome)Enum.Parse(typeof(Heightmap.Biome), name.Trim(), true)); }
                catch (Exception) { Debug.LogWarning($"[Deadheim PvP] StartIslandBiomes: bioma desconhecido '{name.Trim()}'."); }
            }
            return result.Count > 0 ? result : null;
        }

        private static bool[] Dilate(bool[] source, int size)
        {
            bool[] result = new bool[source.Length];
            for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
            {
                if (!source[z * size + x]) continue;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                    result[nz * size + nx] = true;
                }
            }
            return result;
        }
    }
}
