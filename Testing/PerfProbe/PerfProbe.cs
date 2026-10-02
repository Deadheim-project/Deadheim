using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

namespace PerfProbe
{
    /// <summary>
    /// Mede quanto cada mod custa por frame, dentro do jogo de verdade.
    ///
    /// Depois que o personagem entra no mundo, cronometra:
    /// - todo Update/LateUpdate/FixedUpdate/OnGUI de MonoBehaviour dos plugins;
    /// - todo prefix/postfix/finalizer Harmony que os plugins puseram no jogo;
    /// - Resources.FindObjectsOfTypeAll e os wrappers gerenciados de FindObjectsOfType, por
    ///   tipo pedido. O FindObjectsByType&lt;T&gt; generico vai direto ao extern da Unity e nao
    ///   e capturado aqui: o custo dele aparece no Update de quem chama.
    ///
    /// A cada 20 s grava no LogOutput.log o frame medio, o p95, quantas coletas de lixo
    /// houve e os 25 maiores custos em ms por frame. Ferramenta de diagnostico: nao vai
    /// para o pacote dos jogadores.
    ///
    /// Nao instrumenta o AzuAntiCheat: o codigo dele e ofuscado e pode tratar patch nele
    /// como adulteracao. Transpiler tambem fica de fora (ele reescreve o metodo do jogo,
    /// nao ha metodo do mod para cronometrar).
    /// </summary>
    [BepInPlugin(Guid, "PerfProbe", "1.0.0")]
    public class PerfProbePlugin : BaseUnityPlugin
    {
        public const string Guid = "Detalhes.PerfProbe";
        private const float ReportEverySeconds = 20f;
        private const int TopN = 25;

        private sealed class Stat
        {
            public string Name;
            public long Ticks;
            public int Calls;
        }

        private static readonly Dictionary<MethodBase, Stat> _stats = new Dictionary<MethodBase, Stat>();
        private static readonly Dictionary<string, Stat> _scans = new Dictionary<string, Stat>();
        private static readonly HashSet<MethodBase> _instrumented = new HashSet<MethodBase>();
        private static int _mainThread;
        private static bool _measuring;
        private static bool _includeAzu;

        private readonly List<float> _frameMs = new List<float>(4096);
        private Harmony _harmony;
        private float _nextReport;
        private int _gcAtStart;
        private bool _started;

        private void Awake()
        {
            _mainThread = Thread.CurrentThread.ManagedThreadId;
            _harmony = new Harmony(Guid);
            _includeAzu = Config.Bind("Probe", "IncludeAzuAntiCheat", false,
                "Instrumenta tambem o AzuAntiCheat. Desligado por padrao: o anti-adulteracao dele pode fechar o jogo ao ver patch.").Value;
            Logger.LogInfo("PerfProbe carregado; instrumenta quando o personagem entrar no mundo." +
                           (_includeAzu ? " AzuAntiCheat incluido." : ""));
        }

        private void Update()
        {
            if (!_started)
            {
                if (Player.m_localPlayer == null) return;
                _started = true;
                StartCoroutine(StartMeasuring());
                return;
            }
            if (!_measuring) return;

            _frameMs.Add(Time.unscaledDeltaTime * 1000f);
            if (Time.realtimeSinceStartup >= _nextReport) Report();
        }

        private IEnumerator StartMeasuring()
        {
            // Os mods terminam de se ligar nos primeiros segundos no mundo (Jotunn, menus).
            yield return new WaitForSecondsRealtime(5f);
            Instrument();
            ResetWindow();
            _measuring = true;

            // Patch aplicado depois (abrir menu, trocar config) entra na proxima volta.
            while (true)
            {
                yield return new WaitForSecondsRealtime(60f);
                int before = _instrumented.Count;
                Instrument();
                if (_instrumented.Count != before)
                    Logger.LogInfo($"PerfProbe: +{_instrumented.Count - before} metodo(s) novos instrumentados.");
            }
        }

        // ------------------------------------------------------------ instrumentacao

        private static bool Skip(Assembly asm)
        {
            if (asm == null || asm == typeof(PerfProbePlugin).Assembly) return true;
            string name = asm.GetName().Name;
            return (!_includeAzu && name.IndexOf("AzuAnticheat", StringComparison.OrdinalIgnoreCase) >= 0)
                   || name == "0Harmony" || name.StartsWith("BepInEx", StringComparison.Ordinal)
                   || name.StartsWith("MonoMod", StringComparison.Ordinal)
                   || name.StartsWith("assembly_", StringComparison.Ordinal)
                   || name.StartsWith("Unity", StringComparison.Ordinal)
                   || name.StartsWith("System", StringComparison.Ordinal)
                   || name == "mscorlib" || name == "netstandard";
        }

        private static IEnumerable<Assembly> PluginAssemblies()
        {
            var set = new HashSet<Assembly>();
            foreach (PluginInfo info in Chainloader.PluginInfos.Values)
                if (info?.Instance != null) set.Add(info.Instance.GetType().Assembly);

            string pluginDir = Path.GetFullPath(Paths.PluginPath);
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string location;
                try { location = asm.Location; } catch { continue; }
                if (!string.IsNullOrEmpty(location)
                    && Path.GetFullPath(location).StartsWith(pluginDir, StringComparison.OrdinalIgnoreCase))
                    set.Add(asm);
            }
            return set.Where(a => !Skip(a));
        }

        private void Instrument()
        {
            var sw = Stopwatch.StartNew();
            int ok = 0, failed = 0;

            HarmonyMethod before = new HarmonyMethod(typeof(PerfProbePlugin), nameof(Before)) { priority = Priority.First };
            HarmonyMethod after = new HarmonyMethod(typeof(PerfProbePlugin), nameof(After)) { priority = Priority.Last };

            // 1) Update/LateUpdate/FixedUpdate/OnGUI dos MonoBehaviours dos plugins.
            foreach (Assembly asm in PluginAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                catch { continue; }

                foreach (Type type in types)
                {
                    if (type == null || type.IsGenericTypeDefinition) continue;

                    // Corrotinas: o corpo vive no MoveNext da classe que o compilador gera.
                    if (typeof(IEnumerator).IsAssignableFrom(type) && type.Name.Contains(">d__"))
                    {
                        MethodInfo move = type.GetMethod("MoveNext",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                            null, Type.EmptyTypes, null);
                        if (move != null)
                        {
                            string owner = type.DeclaringType != null ? type.DeclaringType.FullName : "?";
                            if (Patch(move, $"{asm.GetName().Name}: corrotina {owner}.{type.Name}", before, after)) ok++; else failed++;
                        }
                        continue;
                    }

                    if (!typeof(MonoBehaviour).IsAssignableFrom(type)) continue;
                    foreach (string name in new[] { "Update", "LateUpdate", "FixedUpdate", "OnGUI" })
                    {
                        MethodInfo m = type.GetMethod(name,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                            null, Type.EmptyTypes, null);
                        if (m == null || m.IsAbstract) continue;
                        if (Patch(m, $"{asm.GetName().Name}: {type.FullName}.{name}", before, after)) ok++; else failed++;
                    }
                }
            }

            // 2) Os prefix/postfix/finalizer que os plugins puseram em metodos do jogo.
            foreach (MethodBase original in Harmony.GetAllPatchedMethods().ToList())
            {
                if (_instrumented.Contains(original)) continue;
                Patches info = Harmony.GetPatchInfo(original);
                if (info == null) continue;

                foreach (var (kind, list) in new[]
                         { ("prefix", info.Prefixes), ("postfix", info.Postfixes), ("finalizer", info.Finalizers) })
                {
                    foreach (Patch p in list)
                    {
                        if (p.owner == Guid) continue;
                        MethodInfo m = p.PatchMethod;
                        if (m == null || Skip(m.DeclaringType?.Assembly)) continue;
                        if (m.IsGenericMethod || m.ContainsGenericParameters || m.GetMethodBody() == null) continue;
                        string label = $"{m.DeclaringType.Assembly.GetName().Name}: {kind} {original.DeclaringType?.Name}.{original.Name}" +
                                       $"  ({m.DeclaringType.Name}.{m.Name})";
                        if (Patch(m, label, before, after)) ok++; else failed++;
                    }
                }
            }

            // 3) Varreduras da Unity, por tipo pedido (quem chamou aparece em 1 ou 2).
            HarmonyMethod scanBefore = new HarmonyMethod(typeof(PerfProbePlugin), nameof(Before));
            HarmonyMethod scanAfter = new HarmonyMethod(typeof(PerfProbePlugin), nameof(AfterScan));
            foreach (MethodBase scan in ScanMethods())
            {
                if (_instrumented.Contains(scan)) continue;
                try
                {
                    _harmony.Patch(scan, prefix: scanBefore, postfix: scanAfter);
                    _instrumented.Add(scan);
                    ok++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Logger.LogWarning($"PerfProbe: nao instrumentei {scan.DeclaringType?.Name}.{scan.Name}: {ex.Message}");
                }
            }

            Logger.LogInfo($"PerfProbe: {ok} metodo(s) instrumentados, {failed} falharam, em {sw.ElapsedMilliseconds} ms.");
        }

        private static IEnumerable<MethodBase> ScanMethods()
        {
            Type obj = typeof(UnityEngine.Object);
            foreach (MethodInfo m in obj.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.IsGenericMethodDefinition) continue;
                if (m.Name != "FindObjectsByType" && m.Name != "FindObjectsOfType" && m.Name != "FindObjectOfType"
                    && m.Name != "FindFirstObjectByType" && m.Name != "FindAnyObjectByType") continue;
                ParameterInfo[] ps = m.GetParameters();
                // Os externs (InternalCall) nao tem corpo para o Harmony; so os wrappers gerenciados.
                if (ps.Length > 0 && ps[0].ParameterType == typeof(Type) && m.GetMethodBody() != null) yield return m;
            }
            MethodInfo all = typeof(Resources).GetMethod("FindObjectsOfTypeAll", new[] { typeof(Type) });
            if (all != null) yield return all;

        }

        private bool Patch(MethodBase m, string label, HarmonyMethod before, HarmonyMethod after)
        {
            if (_instrumented.Contains(m)) return true;
            try
            {
                _harmony.Patch(m, prefix: before, postfix: after);
                _instrumented.Add(m);
                _stats[m] = new Stat { Name = label };
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"PerfProbe: nao instrumentei {label}: {ex.Message}");
                return false;
            }
        }

        // ------------------------------------------------------------ cronometro

        private static void Before(out long __state) => __state = Stopwatch.GetTimestamp();

        private static void After(long __state, MethodBase __originalMethod)
        {
            if (!_measuring || Thread.CurrentThread.ManagedThreadId != _mainThread) return;
            if (!_stats.TryGetValue(__originalMethod, out Stat stat)) return;
            stat.Ticks += Stopwatch.GetTimestamp() - __state;
            stat.Calls++;
        }

        private static void AfterScan(long __state, object[] __args, MethodBase __originalMethod)
        {
            if (!_measuring || Thread.CurrentThread.ManagedThreadId != _mainThread) return;
            string arg = __args != null && __args.Length > 0
                ? (__args[0] is Type t ? t.Name : __args[0] as string ?? "?")
                : "?";
            string key = $"[Unity] {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}({arg})";
            if (!_scans.TryGetValue(key, out Stat stat)) _scans[key] = stat = new Stat { Name = key };
            stat.Ticks += Stopwatch.GetTimestamp() - __state;
            stat.Calls++;
        }

        // --------------------------------------------------------------- relatorio

        private void ResetWindow()
        {
            foreach (Stat s in _stats.Values) { s.Ticks = 0; s.Calls = 0; }
            _scans.Clear();
            _frameMs.Clear();
            _gcAtStart = GC.CollectionCount(0);
            _nextReport = Time.realtimeSinceStartup + ReportEverySeconds;
        }

        private void Report()
        {
            int frames = Mathf.Max(1, _frameMs.Count);
            float total = _frameMs.Sum();
            float avg = total / frames;
            List<float> sorted = _frameMs.OrderBy(x => x).ToList();
            float p95 = sorted.Count > 0 ? sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.95f))] : 0f;
            float max = sorted.Count > 0 ? sorted[sorted.Count - 1] : 0f;
            double msPerTick = 1000.0 / Stopwatch.Frequency;

            IEnumerable<Stat> all = _stats.Values.Concat(_scans.Values).Where(s => s.Calls > 0);
            double modMs = _stats.Values.Sum(s => s.Ticks) * msPerTick / frames;

            Player p = Player.m_localPlayer;
            Vector3 pos = p != null ? p.transform.position : Vector3.zero;
            int netObjects = ZNetScene.instance != null ? ZNetScene.instance.m_instances.Count : -1;

            var sb = new StringBuilder();
            sb.AppendLine($"PerfProbe {ReportEverySeconds:0}s: {frames} frames, media {avg:0.0} ms ({1000f / Mathf.Max(0.001f, avg):0} fps), " +
                          $"p95 {p95:0.0} ms, max {max:0.0} ms, GC {GC.CollectionCount(0) - _gcAtStart}x, " +
                          $"mods ~{modMs:0.00} ms/frame (soma, pode contar aninhado), objetos de rede {netObjects}, pos ({pos.x:0},{pos.z:0})");
            sb.AppendLine("   ms/frame  chamadas/frame  us/chamada  metodo");
            foreach (Stat s in all.OrderByDescending(s => s.Ticks).Take(TopN))
            {
                double ms = s.Ticks * msPerTick;
                sb.AppendLine($"   {ms / frames,8:0.000}  {s.Calls / (double)frames,14:0.00}  {ms * 1000.0 / s.Calls,10:0.0}  {s.Name}");
            }
            Logger.LogInfo(sb.ToString());
            ResetWindow();
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
