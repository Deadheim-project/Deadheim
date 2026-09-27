// Roteiro "solo": um cliente real conectado ao servidor dedicado e um segundo jogador
// ("Dummy") criado e controlado pelo proprio driver.
//
// Existe porque dois clientes do Valheim + servidor nao cabem na memoria de uma maquina
// comum. O Dummy e um Player de verdade (mesmo prefab, ZDO propria, nome e playerID),
// so que o dono da ZDO dele e este cliente: ele ataca o jogador local pelo mesmo
// RPC_Damage de um jogador remoto, e a regra roda no cliente da vitima, como no jogo.
//
// O que o solo nao cobre e o roteiro A/B cobre: o servidor entregando a marca de PK e a
// recompensa ao matador online, o convite de cla entre dois jogadores e o mapa do colega.
using Deadheim.Pvp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PvpTestDriver
{
    /// <summary>
    /// O Player.FixedUpdate do jogo destroi qualquer Player que este cliente possua e que
    /// nao seja o jogador local ("Destroying old local player"). O Dummy e exatamente isso,
    /// entao os ciclos de Player dele nao rodam: fica parado, com ZDO e colisor intactos.
    /// </summary>
    [HarmonyLib.HarmonyPatch]
    internal static class DummyGuard
    {
        [HarmonyLib.HarmonyPatch(typeof(Player), "FixedUpdate")]
        [HarmonyLib.HarmonyPrefix]
        private static bool FixedUpdate(Player __instance) => __instance == null || __instance != Driver.DummyInstance;

        [HarmonyLib.HarmonyPatch(typeof(Player), "Update")]
        [HarmonyLib.HarmonyPrefix]
        private static bool Update(Player __instance) => __instance == null || __instance != Driver.DummyInstance;

        [HarmonyLib.HarmonyPatch(typeof(Player), "LateUpdate")]
        [HarmonyLib.HarmonyPrefix]
        private static bool LateUpdate(Player __instance) => __instance == null || __instance != Driver.DummyInstance;
    }

    /// <summary>Diagnostico: o que acontece com um golpe no Dummy dentro do RPC_Damage.</summary>
    [HarmonyLib.HarmonyPatch]
    internal static class DummyDamageTrace
    {
        [HarmonyLib.HarmonyPatch(typeof(Character), "RPC_Damage")]
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.First)]
        private static void Before(Character __instance, HitData hit)
        {
            if (__instance == null || __instance != Driver.DummyInstance) return;
            Driver.Trace($"RPC_Damage(dummy) atacante={hit.m_attacker} achado={(hit.GetAttacker() != null)} dano={hit.GetTotalDamage()} vida={__instance.GetHealth()}");
        }

        [HarmonyLib.HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        [HarmonyLib.HarmonyPostfix]
        private static void Applied(Character __instance, HitData hit)
        {
            if (__instance == null || __instance != Driver.DummyInstance) return;
            Driver.Trace($"ApplyDamage(dummy) dano={hit.GetTotalDamage()} vida={__instance.GetHealth()}");
        }
    }

    public partial class Driver
    {
        private const long DummyId = 424242L;
        private const string DummyName = "Dummy";

        private Player _dummy;

        /// <summary>O Dummy atual, para o patch que impede o jogo de destrui-lo.</summary>
        internal static Player DummyInstance;

        private IEnumerator RunSolo()
        {
            Log("spawn ok (solo)");
            yield return new WaitForSeconds(4f);
            File.WriteAllText(Path.Combine(_sync, "spawned-S"), DateTime.Now.ToString("HH:mm:ss"));

            List<KeyValuePair<string, Func<IEnumerator>>> script = new List<KeyValuePair<string, Func<IEnumerator>>>
            {
                Step("setup", SoloSetup),
                Step("eu-bato", SoloIHit),
                Step("dummy-bate", SoloDummyHits),
                Step("zona-segura", SoloSafeZone),
                Step("atacante-protegido", SoloAttackerProtected),
                Step("combate", SoloCombatTag),
                Step("cla", SoloClan),
                Step("territorio", SoloTerritory),
                Step("defesa-morte", SoloDefenseDeath),
                Step("imunidade", SoloImmunity),
                Step("pk", SoloPk),
                Step("arena", SoloArena),
                Step("desafio", SoloChallenge),
                Step("desafio-sobrevive", SoloChallengeSurvive),
                Step("morte-pve-x-pvp", SoloDeathCause),
                Step("defesa-do-dummy", SoloDummyDefends),
                Step("tumba", SoloTombstone),
                Step("transporte", SoloTransport),
                Step("ward-natureza", SoloWardNature),
                Step("retreat", Retreat),
                Step("rank", SoloRank),
                Step("coins", Coins),
            };

            // -dhtest-steps a,b,c roda so esses passos (o setup sempre roda).
            string only = Arg(Environment.GetCommandLineArgs(), "-dhtest-steps");
            HashSet<string> wanted = string.IsNullOrEmpty(only) ? null : new HashSet<string>(only.Split(','));

            foreach (KeyValuePair<string, Func<IEnumerator>> step in script)
            {
                if (wanted != null && step.Key != "setup" && !wanted.Contains(step.Key)) continue;
                Log("=== " + step.Key);
                yield return RunSafely(step.Value(), step.Key);
            }

            Log($"DONE pass={_pass} fail={_fail}");
            File.WriteAllText(Path.Combine(_sync, "result-S.txt"), $"pass={_pass} fail={_fail}");
        }

        // ------------------------------------------------------------------- dummy

        internal static void Trace(string text) => Log("trace " + text);

        private static Vector3 Ground(Vector3 p)
        {
            p.y = ZoneSystem.instance.GetSolidHeight(p) + 0.3f;
            return p;
        }

        private Player SpawnDummy(Vector3 at)
        {
            GameObject go = Instantiate(ZNetScene.instance.GetPrefab("Player"), Ground(at), Quaternion.identity);
            Player dummy = go.GetComponent<Player>();
            DummyInstance = dummy;
            _dummy = dummy;
            dummy.SetPlayerID(DummyId, DummyName);
            // O jogo so tira o personagem da animacao de acordar no FixedUpdate, que o
            // DummyGuard pausa. Preso nela o boneco fica em InCutscene e o vanilla ignora dano.
            dummy.m_nview.GetZDO().Set(ZDOVars.s_wakeup, false);
            if (dummy.m_animator != null) dummy.m_animator.SetBool("wakeup", false);
            dummy.SetMaxHealth(1000f, false);
            dummy.SetHealth(1000f);
            SetDummy(true, PvpFlags.None);
            Log($"dummy criado em {go.transform.position} id={dummy.GetPlayerID()} nome={dummy.GetPlayerName()}");
            return dummy;
        }

        private void SetDummy(bool pvp, PvpFlags flags)
        {
            Player dummy = _dummy;
            if (dummy == null) return;
            dummy.m_pvp = pvp;
            ZDO zdo = dummy.m_nview.GetZDO();
            zdo.Set(ZDOVars.s_pvp, pvp);
            zdo.Set(PvpState.ZdoFlags, (int)flags);
        }

        private void MoveDummy(Vector3 at)
        {
            if (_dummy == null) return;
            Vector3 p = Ground(at);
            _dummy.transform.position = p;
            if (_dummy.m_body != null)
            {
                _dummy.m_body.position = p;
                _dummy.m_body.linearVelocity = Vector3.zero;
            }
        }

        private HitData HitFrom(Player attacker, Character target, float damage)
        {
            HitData hit = new HitData();
            hit.m_damage.m_slash = damage;
            hit.m_point = target.GetCenterPoint();
            hit.m_dir = Vector3.forward;
            hit.m_hitType = HitData.HitType.PlayerHit;
            hit.m_skill = Skills.SkillType.Swords;
            hit.m_blockable = false;
            hit.m_dodgeable = false;
            hit.m_staggerMultiplier = 0f;
            hit.m_pushForce = 0f;
            hit.SetAttacker(attacker);
            return hit;
        }

        /// <summary>O Dummy golpeia o jogador local: RPC_Damage no dono da vitima (este cliente).</summary>
        private void DummyStrikesMe(float damage)
            => Me.m_nview.InvokeRPC("RPC_Damage", HitFrom(_dummy, Me, damage));

        /// <summary>O jogador local golpeia o Dummy pelo RPC (a checagem do atacante nao roda).</summary>
        private void IStrikeDummy(float damage)
            => _dummy.m_nview.InvokeRPC("RPC_Damage", HitFrom(Me, _dummy, damage));

        private void Heal()
        {
            Me.SetHealth(Me.GetMaxHealth());
            DamageTaken = 0f;
        }

        private IEnumerator ExpectDummyDamage(string name, float before, float expected)
        {
            yield return Wait(0.5f);
            float taken = before - _dummy.GetHealth();
            Check(name, Mathf.Abs(taken - expected) < 0.05f, $"esperado={expected:0.##} recebido={taken:0.##}");
        }

        // O servidor roda na mesma maquina: o log dele mostra o que o cliente nao ve
        // (quem virou PK, quem ganhou recompensa de defesa).
        private string ServerLogPath => Path.Combine(Path.GetDirectoryName(_sync.TrimEnd('\\', '/')), "server-unity.log");
        private long _serverLogMark;

        private void MarkServerLog()
        {
            try { _serverLogMark = new FileInfo(ServerLogPath).Length; }
            catch (Exception) { _serverLogMark = 0L; }
        }

        private string ServerLogSinceStart()
        {
            long mark = _serverLogMark;
            _serverLogMark = 0L;
            string all = ServerLogSinceMark();
            _serverLogMark = mark;
            return all;
        }

        private string ServerLogSinceMark()
        {
            try
            {
                using FileStream stream = new FileStream(ServerLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                stream.Seek(Math.Min(_serverLogMark, stream.Length), SeekOrigin.Begin);
                using StreamReader reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                return "(log do servidor ilegivel: " + ex.Message + ")";
            }
        }

        private static Dictionary<long, string> ClanDirectory
            => (Dictionary<long, string>)typeof(Clans).GetField("_directory", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        private IEnumerator DummyKillsMe(string what)
        {
            Player dead = Me;
            DummyStrikesMe(1000f);
            yield return WaitRespawn(dead);
            Log("morte: " + what);
        }

        // --------------------------------------------------------------------- fases

        private IEnumerator SoloSetup()
        {
            Check("setup/modulo-ativo", PvpConfig.Active);
            Check("setup/templo", PvpZones.TryGetStartCenter(out _temple), $"templo={_temple}");
            _temple.y = 0f;
            _safe = FindLand(_temple, 20f, 27f);
            _openA = FindLand(_temple, 50f, 80f, _temple);
            _openB = _openA + Vector3.right * 2f;
            _wardA = FindLand(_temple, 50f, 80f, _openA, _temple);
            _wardB = FindLand(_temple, 50f, 80f, _openA, _wardA, _temple);
            _ship = _openA + Vector3.forward * 8f;
            _deathFactor = Me.GetSkills().m_DeathLowerFactor * Game.m_skillReductionRate;
            Log($"posicoes: templo={_temple} seguro={_safe} aberto={_openA} wardA={_wardA} wardB={_wardB} fatorMorte={_deathFactor}");
            Log("ilha: " + PvpZones.DescribeIsland());

            Check("setup/arredor-do-templo-seguro", PvpZones.IsSafeArea(_safe), PvpZones.SafeAreaName(_safe));
            Check("setup/aberto-nao-seguro", !PvpZones.IsSafeArea(_openA) && !PvpZones.IsArena(_openA));
            Check("setup/templo-e-arena", PvpZones.IsArena(_temple));

            SetSwords(Me, 50f);
            // Sem armadura: o dano medido tem que ser so o da regra.
            Me.UnequipAllItems();
            Me.GetInventory().AddItem(ObjectDB.instance.GetItemPrefab("Wood"), 10);

            yield return MoveTo(_openA);
            _dummy = SpawnDummy(_openB);
            // Personagem novo nasce "acordando" (InCutscene) e o vanilla ignora dano nesse estado.
            float ready = Time.time + 30f;
            while (Time.time < ready && (Me.InCutscene() || _dummy.InCutscene())) yield return Wait(0.5f);
            Check("setup/fora-de-cutscene", !Me.InCutscene() && !_dummy.InCutscene(), $"eu={Me.InCutscene()} dummy={_dummy.InCutscene()}");
            yield return Wait(1.5f);
            Check("setup/pvp-ativo-no-aberto", Me.IsPVPEnabled() && (PvpState.Current & PvpFlags.Protected) == 0, $"flags={PvpState.Current}");
            Check("setup/hud", PvpHud.Compose(Me).Contains("PvP ATIVO"), PvpHud.Compose(Me));
            string log = File.Exists(ServerLogPath) ? ServerLogSinceStart() : "";
            Check("setup/servidor-recebeu-hello", log.Contains($"{_myName} ({Me.GetPlayerID()}) sincronizado"), Tail(log));
        }

        private IEnumerator SoloIHit()
        {
            Log($"dummy antes: vida={_dummy.GetHealth()} max={_dummy.GetMaxHealth()} pvp={_dummy.IsPVPEnabled()} dono={_dummy.m_nview.IsOwner()} " +
                $"cutscene={_dummy.InCutscene()} morto={_dummy.IsDead()} teleporte={_dummy.IsTeleporting()} voo={_dummy.IsDebugFlying()} " +
                $"deus={_dummy.InGodMode()} regra={PvpRules.Check(Me, _dummy)} euPvp={Me.IsPVPEnabled()}");
            float before = _dummy.GetHealth();
            IStrikeDummy(Hit);
            yield return ExpectDummyDamage("eu-bato/dano-x0.5", before, Hit * PvpConfig.DamageMultiplier.Value);
        }

        private IEnumerator SoloDummyHits()
        {
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("dummy-bate/dano-x0.5", Hit * PvpConfig.DamageMultiplier.Value, 0.5f);
            yield return Wait(0.5f);
            Check("dummy-bate/em-combate", PvpState.InCombat && (PvpState.Current & PvpFlags.Combat) != 0, $"flags={PvpState.Current}");
        }

        private IEnumerator SoloSafeZone()
        {
            yield return Wait(CombatWait);
            yield return MoveTo(_safe);
            yield return Wait(0.5f);
            Check("zona-segura/protegido", (PvpState.Current & PvpFlags.Protected) != 0 && !Me.IsPVPEnabled(), $"flags={PvpState.Current}");
            Check("zona-segura/hud", PvpHud.Compose(Me).Contains("ZONA SEGURA"), PvpHud.Compose(Me));
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("zona-segura/bloqueia-dano", 0f, 0.5f);

            // Protegido tambem nao ataca: o golpe no Dummy e barrado no cliente da vitima.
            float before = _dummy.GetHealth();
            IStrikeDummy(Hit);
            yield return ExpectDummyDamage("zona-segura/protegido-nao-ataca", before, 0f);
            Check("zona-segura/atacante-recusa", PvpRules.Check(Me, _dummy) == PvpRules.Verdict.AttackerProtected,
                PvpRules.Check(Me, _dummy).ToString());
        }

        private IEnumerator SoloAttackerProtected()
        {
            yield return MoveTo(_openA);
            yield return Wait(0.5f);
            SetDummy(false, PvpFlags.Protected);
            yield return Wait(0.3f);
            Check("atacante-protegido/tag", _dummy.GetHoverName().Contains("SEGURO"), _dummy.GetHoverName());
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("atacante-protegido/bloqueado", 0f, 0.5f);
            SetDummy(true, PvpFlags.None);
        }

        private IEnumerator SoloCombatTag()
        {
            yield return Wait(CombatWait);
            Heal();
            DummyStrikesMe(Hit);
            yield return Wait(0.3f);
            yield return MoveTo(_safe);
            Check("combate/zona-segura-nao-protege", PvpState.InCombat && Me.IsPVPEnabled() && (PvpState.Current & PvpFlags.Protected) == 0,
                $"flags={PvpState.Current}");
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("combate/leva-dano-na-zona-segura", Hit * PvpConfig.DamageMultiplier.Value, 0.5f);
            yield return Wait(CombatWait);
            Check("combate/protegido-depois", (PvpState.Current & PvpFlags.Protected) != 0 && !Me.IsPVPEnabled(), $"flags={PvpState.Current}");
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("combate/bloqueado-depois", 0f, 0.5f);
        }

        private IEnumerator SoloClan()
        {
            yield return MoveTo(_openA);
            yield return Wait(CombatWait);
            Command("cla criar Lobos");
            yield return Wait(2f);
            Check("cla/criado", Clans.OwnClan == "Lobos", "cla=" + Clans.OwnClan);

            ClanDirectory[DummyId] = "Lobos";
            Check("cla/mesmo-cla", Clans.SameClan(Me.GetPlayerID(), DummyId));
            Check("cla/tag-no-nome", _dummy.GetHoverName().Contains("[Lobos]"), _dummy.GetHoverName());
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("cla/sem-fogo-amigo", 0f, 0.5f);
            float before = _dummy.GetHealth();
            IStrikeDummy(Hit);
            yield return ExpectDummyDamage("cla/sem-fogo-amigo-2", before, 0f);

            ClanDirectory.Remove(DummyId);
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("cla/fora-do-cla-leva-dano", Hit * PvpConfig.DamageMultiplier.Value, 0.5f);

            Command("cla sair");
            yield return Wait(2f);
            Check("cla/saiu", string.IsNullOrEmpty(Clans.OwnClan), "cla=" + Clans.OwnClan);
        }

        private IEnumerator SoloTerritory()
        {
            yield return Wait(CombatWait);
            yield return MoveTo(_wardA + Vector3.right * 2f);
            MoveDummy(_wardA - Vector3.right * 2f);
            _myWard = SpawnWard(_wardA, Me, _dummy);
            yield return Wait(1.5f);
            Check("territorio/meu", PvpRules.InOwnTerritory(Me.GetPlayerID(), Me.transform.position));
            Check("territorio/compartilhado", PvpRules.SharesTerritory(Me.GetPlayerID(), DummyId, Me.transform.position));
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("territorio/donos-sem-fogo-amigo", 0f, 0.5f);

            _myWard.RemovePermitted(DummyId);
            yield return Wait(0.5f);
            Check("territorio/dummy-virou-invasor", !PvpRules.SharesTerritory(Me.GetPlayerID(), DummyId, Me.transform.position));
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("territorio/defensor-leva-menos",
                Hit * PvpConfig.DamageMultiplier.Value * PvpConfig.WardDefenseMultiplier.Value, 0.5f);
        }

        private IEnumerator SoloDefenseDeath()
        {
            // Morro dentro do meu territorio para o invasor: defesa de castelo, sem perda de skill.
            SetSwords(Me, 50f);
            ArmHardDeath();
            yield return DummyKillsMe("defesa");
            Check("defesa-morte/sem-perda-de-skill", Mathf.Abs(Swords(Me) - 50f) < 0.05f, "skill=" + Swords(Me));
            // Renasce no templo, que no teste e arena (la a imunidade nao vale): confere o estado.
            Check("defesa-morte/imune", PvpState.IsImmune, $"imune={PvpState.IsImmune} flags={PvpState.Current}");
            if (_myWard != null) ZNetScene.instance.Destroy(_myWard.gameObject);
            _myWard = null;
        }

        private IEnumerator SoloImmunity()
        {
            yield return MoveTo(_openA);
            MoveDummy(_openB);
            yield return Wait(1f);
            Check("imunidade/hud", PvpHud.Compose(Me).Contains("IMUNE"), PvpHud.Compose(Me));
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("imunidade/nao-recebe", 0f, 0.5f);
            float before = _dummy.GetHealth();
            IStrikeDummy(Hit);
            yield return ExpectDummyDamage("imunidade/nao-causa", before, 0f);
        }

        private IEnumerator SoloPk()
        {
            ClearAllProtection();
            PvpState.ApplyServerTimers(300d, 0d, 0d);
            yield return Wait(1f);
            Check("pk/marcado", (PvpState.Current & PvpFlags.Pk) != 0, $"flags={PvpState.Current}");
            Check("pk/hud", PvpHud.Compose(Me).Contains("PK"), PvpHud.Compose(Me));
            SetSwords(Me, 50f);
            float before = Swords(Me);
            ArmHardDeath();
            MarkServerLog();
            yield return DummyKillsMe("pk");
            yield return Wait(1f);
            string log = ServerLogSinceMark();
            Check("pk/servidor-marca-o-matador", log.Contains($"{DummyName} ({DummyId}) agora e PK"), Tail(log));
            float expected = before * (1f - Mathf.Clamp01(_deathFactor * PvpConfig.PkSkillLossMultiplier.Value));
            Check("pk/perda-em-dobro", Mathf.Abs(Swords(Me) - expected) < 0.05f, $"antes={before} depois={Swords(Me)} esperado={expected}");
            Check("pk/marca-some-ao-morrer", !PvpState.IsPk, $"flags={PvpState.Current}");
        }

        private IEnumerator SoloArena()
        {
            ClearAllProtection();
            yield return MoveTo(_temple + Vector3.right * 2f);
            MoveDummy(_temple - Vector3.right * 2f);
            PvpState.GrantImmunity(Me, 120d);
            yield return Wait(1f);
            Check("arena/dentro-e-imune-ainda-luta", (PvpState.Current & PvpFlags.Arena) != 0 && Me.IsPVPEnabled(), $"flags={PvpState.Current}");
            Check("arena/hud", PvpHud.Compose(Me).Contains("Arena"), PvpHud.Compose(Me));
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("arena/imunidade-ignorada", Hit * PvpConfig.DamageMultiplier.Value, 0.5f);

            ClanDirectory[DummyId] = "Lobos";
            ClanDirectory[Me.GetPlayerID()] = "Lobos";
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("arena/fogo-amigo-liberado", Hit * PvpConfig.DamageMultiplier.Value, 0.5f);
            ClanDirectory.Remove(DummyId);
            ClanDirectory.Remove(Me.GetPlayerID());

            PvpState.ClearImmunity(Me);
            SetSwords(Me, 50f);
            ArmHardDeath();
            yield return DummyKillsMe("arena");
            Check("arena/sem-perda-de-skill", Mathf.Abs(Swords(Me) - 50f) < 0.05f, "skill=" + Swords(Me));
            Check("arena/sem-imunidade", !PvpState.IsImmune, $"flags={PvpState.Current}");
        }

        private static bool ListedPublic(string name, out Vector3 position)
        {
            ZNet.PlayerInfo entry = ZNet.instance.m_players.FirstOrDefault(p => p.m_name == name);
            position = entry.m_position;
            return entry.m_name == name && entry.m_publicPosition;
        }

        private IEnumerator SoloChallenge()
        {
            ClearAllProtection();
            yield return MoveTo(_openA);
            MoveDummy(_openB);
            yield return Wait(CombatWait);
            ZNet.instance.SetPublicReferencePosition(false);
            yield return Wait(3f);
            Check("desafio/fora-do-mapa-antes", !ListedPublic(_myName, out _));

            Command("desafio");
            yield return Wait(1.5f);
            Check("desafio/pendente", PvpState.IsHuntPending && (PvpState.Current & PvpFlags.HuntPending) != 0, $"flags={PvpState.Current}");
            yield return Wait(PvpConfig.ChallengeDelaySeconds.Value + 1.5f);
            yield return MoveTo(_safe);
            yield return Wait(0.5f);
            Check("desafio/cacado-sem-zona-segura", PvpState.IsHunted && Me.IsPVPEnabled() && (PvpState.Current & PvpFlags.Protected) == 0,
                $"flags={PvpState.Current}");
            Check("desafio/hud", PvpHud.Compose(Me).Contains("CACADO"), PvpHud.Compose(Me));
            yield return Wait(3f);
            bool listed = ListedPublic(_myName, out Vector3 at);
            Check("desafio/no-mapa-de-todos", listed && Utils.DistanceXZ(at, _safe) < 25f, $"listado={listed} pos={at}");

            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("desafio/leva-dano-na-zona-segura", Hit * PvpConfig.DamageMultiplier.Value, 0.5f);

            yield return DummyKillsMe("desafio");
            yield return Wait(2f);
            Check("desafio/terminou-ao-morrer", !PvpState.IsHunted && !PvpState.IsHuntPending, $"flags={PvpState.Current}");
            yield return Wait(3f);
            Check("desafio/saiu-do-mapa", !ListedPublic(_myName, out _));
        }

        private IEnumerator SoloChallengeSurvive()
        {
            ClearAllProtection();
            yield return Wait(Mathf.Max(1f, PvpConfig.ChallengeCooldownMinutes.Value * 60f) + 2f);
            PvpConfig.Reward reward = PvpConfig.ParseReward(PvpConfig.ChallengeSurviveReward.Value);
            int before = CountItem(Me, reward.Prefab);
            Command("desafio");
            float until = Time.time + PvpConfig.ChallengeDelaySeconds.Value + PvpConfig.ChallengeDurationMinutes.Value * 60f + 15f;
            while (Time.time < until && CountItem(Me, reward.Prefab) == before) yield return Wait(1f);
            int gained = CountItem(Me, reward.Prefab) - before;
            Check("desafio-sobrevive/recompensa", gained == reward.Amount, $"ganhou={gained} esperado={reward.Amount}");
            yield return Wait(2f);
            Check("desafio-sobrevive/terminou", !PvpState.IsHunted, $"flags={PvpState.Current}");
        }

        private static string Tail(string text)
        {
            text = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', '|');
            return text.Length > 400 ? text.Substring(text.Length - 400) : text;
        }

        /// <summary>
        /// Eu invado o territorio do Dummy e ele me mata la dentro: ele defendeu. O servidor
        /// tem que dar a recompensa do bioma a ele e nao marca-lo como PK; eu perco skill
        /// normalmente (invasor) e fico imune.
        /// </summary>
        private IEnumerator SoloDummyDefends()
        {
            ClearAllProtection();
            yield return MoveTo(_wardB + Vector3.right * 2f);
            MoveDummy(_wardB - Vector3.right * 2f);
            PrivateArea ward = SpawnWard(_wardB, _dummy, null);
            yield return Wait(1.5f);
            Check("defesa-do-dummy/ele-defende", PvpRules.IsDefending(DummyId, Me.GetPlayerID(), Me.transform.position));

            SetSwords(Me, 50f);
            ArmHardDeath();
            MarkServerLog();
            yield return DummyKillsMe("invadi o territorio do dummy");
            Check("defesa-do-dummy/invasor-perde-skill-normal", Mathf.Abs(Swords(Me) - 50f * (1f - _deathFactor)) < 0.05f, "skill=" + Swords(Me));
            Check("defesa-do-dummy/invasor-fica-imune", PvpState.IsImmune, $"flags={PvpState.Current}");
            yield return Wait(1f);

            string log = ServerLogSinceMark();
            Heightmap.Biome biome = WorldGenerator.instance.GetBiome(_wardB);
            int expected = PvpConfig.DefenseRewardFor(biome);
            Check("defesa-do-dummy/recompensa-por-bioma", log.Contains($"Recompensa de defesa: {DummyName} +{expected} "), $"bioma={biome} esperado={expected} log={Tail(log)}");
            Check("defesa-do-dummy/defensor-nao-vira-pk", !log.Contains($"{DummyName} ({DummyId}) agora e PK"), Tail(log));
            PvpState.ClearImmunity(Me);
            ZNetScene.instance.Destroy(ward.gameObject);
        }

        /// <summary>Morte por jogador x por PvE: e dela que dependem imunidade, PK, ranking e defesa.</summary>
        private IEnumerator SoloDeathCause()
        {
            ClearAllProtection();
            yield return MoveTo(_openA);
            MoveDummy(_openB);
            yield return Wait(CombatWait);

            // 1. Queda sem atacante nenhum: PvE puro.
            SetSwords(Me, 50f);
            ArmHardDeath();
            Player dead = Me;
            HitData fall = new HitData { m_hitType = HitData.HitType.Fall, m_point = Me.GetCenterPoint() };
            fall.m_damage.m_damage = 1000f;
            Me.m_nview.InvokeRPC("RPC_Damage", fall);
            yield return WaitRespawn(dead);
            Check("pve/queda-perde-skill-normal", Mathf.Abs(Swords(Me) - 50f * (1f - _deathFactor)) < 0.05f, "skill=" + Swords(Me));
            Check("pve/queda-sem-imunidade", !PvpState.IsImmune, $"flags={PvpState.Current}");

            // 2. Morto por monstro: PvE, mesmo com atacante.
            yield return MoveTo(_openA);
            GameObject boar = Instantiate(ZNetScene.instance.GetPrefab("Boar"), Ground(_openA + Vector3.forward * 3f), Quaternion.identity);
            yield return Wait(1f);
            ArmHardDeath();
            dead = Me;
            HitData bite = new HitData { m_hitType = HitData.HitType.EnemyHit, m_point = Me.GetCenterPoint() };
            bite.m_damage.m_pierce = 1000f;
            bite.SetAttacker(boar.GetComponent<Character>());
            Me.m_nview.InvokeRPC("RPC_Damage", bite);
            yield return WaitRespawn(dead);
            Check("pve/mob-sem-imunidade", !PvpState.IsImmune, $"flags={PvpState.Current}");
            ZNetScene.instance.Destroy(boar);

            // 3. Jogador bate e a queda termina o servico: credito para o jogador.
            yield return MoveTo(_openA);
            MoveDummy(_openB);
            yield return Wait(CombatWait);
            Heal();
            DummyStrikesMe(Hit);
            yield return Wait(0.5f);
            dead = Me;
            fall = new HitData { m_hitType = HitData.HitType.Fall, m_point = Me.GetCenterPoint() };
            fall.m_damage.m_damage = 1000f;
            Me.m_nview.InvokeRPC("RPC_Damage", fall);
            yield return WaitRespawn(dead);
            Check("pvp/queda-apos-golpe-conta-como-jogador", PvpState.IsImmune, $"imune={PvpState.IsImmune} flags={PvpState.Current}");
            PvpState.ClearImmunity(Me);
        }

        private IEnumerator SoloTombstone()
        {
            GameObject prefab = Me.m_tombstone;
            Vector3 at = Me.transform.position + Me.transform.forward * 2f + Vector3.up;
            TombStone tomb = Instantiate(prefab, at, Quaternion.identity).GetComponent<TombStone>();
            yield return Wait(0.5f);
            tomb.Setup(DummyName, DummyId);
            tomb.m_container.GetInventory().AddItem(ObjectDB.instance.GetItemPrefab("Wood"), 5);
            yield return Wait(0.5f);
            bool opened = tomb.Interact(Me, false, false);
            Check("tumba/alheia-trancada", !opened && !InventoryGui.IsVisible(), $"abriu={opened}");
            Check("tumba/hover", tomb.GetHoverText().Contains("Trancada"), tomb.GetHoverText());

            tomb.m_nview.GetZDO().Set(ZDOVars.s_owner, Me.GetPlayerID());
            yield return Wait(0.3f);
            opened = tomb.Interact(Me, false, false);
            Check("tumba/dono-abre", opened);
            yield return Wait(1f);
            if (InventoryGui.IsVisible()) InventoryGui.instance.Hide();
        }

        private IEnumerator SoloWardNature()
        {
            yield return MoveTo(_wardB + Vector3.right * 3f);
            PrivateArea ward = SpawnWard(_wardB, _dummy, null);
            yield return Wait(1f);

            GameObject treePrefab = ZNetScene.instance.GetPrefab("Beech1");
            if (treePrefab == null)
            {
                Check("ward-natureza/prefab", false, "Beech1 nao existe");
                yield break;
            }

            TreeBase inside = Instantiate(treePrefab, Ground(_wardB + Vector3.forward * 3f), Quaternion.identity).GetComponent<TreeBase>();
            TreeBase outside = Instantiate(treePrefab, Ground(_wardB + Vector3.forward * 40f), Quaternion.identity).GetComponent<TreeBase>();
            yield return Wait(1f);

            float insideBefore = inside.m_nview.GetZDO().GetFloat(ZDOVars.s_health, inside.m_health);
            float outsideBefore = outside.m_nview.GetZDO().GetFloat(ZDOVars.s_health, outside.m_health);
            inside.Damage(ChopHit(inside));
            outside.Damage(ChopHit(outside));
            yield return Wait(1f);
            float insideAfter = inside.m_nview.GetZDO().GetFloat(ZDOVars.s_health, inside.m_health);
            float outsideAfter = outside.m_nview.GetZDO().GetFloat(ZDOVars.s_health, outside.m_health);
            Check("ward-natureza/arvore-dentro-protegida", Mathf.Approximately(insideBefore, insideAfter), $"antes={insideBefore} depois={insideAfter}");
            Check("ward-natureza/arvore-fora-cai", outsideAfter < outsideBefore, $"antes={outsideBefore} depois={outsideAfter}");

            ZNetScene.instance.Destroy(inside.gameObject);
            ZNetScene.instance.Destroy(outside.gameObject);
            ZNetScene.instance.Destroy(ward.gameObject);
        }

        private IEnumerator SoloTransport()
        {
            ClearAllProtection();
            yield return MoveTo(_openA);
            yield return Wait(CombatWait);
            GameObject ship = Instantiate(ZNetScene.instance.GetPrefab("Raft"), Ground(_ship) + Vector3.up * 0.3f, Quaternion.identity);
            yield return Wait(2f);
            WearNTear wnt = ship.GetComponent<WearNTear>();
            float before = wnt.GetHealthPercentage();
            HitData hit = new HitData();
            hit.m_damage.m_blunt = 50f;
            hit.m_point = ship.transform.position;
            hit.SetAttacker(Me);
            wnt.Damage(hit);
            yield return Wait(1.5f);
            Check("transporte/barco-nao-toma-dano-de-jogador", Mathf.Approximately(before, wnt.GetHealthPercentage()),
                $"antes={before} depois={wnt.GetHealthPercentage()}");

            Vector3 deck = ship.transform.position + Vector3.up * 1.2f;
            Me.transform.position = deck;
            Me.m_body.position = deck;
            Me.m_body.linearVelocity = Vector3.zero;
            yield return Wait(1.5f);
            Check("transporte/no-barco-protegido", PvpZones.IsOnTransport(Me) && (PvpState.Current & PvpFlags.Protected) != 0,
                $"volumes={Me.InNumShipVolumes} flags={PvpState.Current}");
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("transporte/barco-bloqueia-dano", 0f, 0.5f);
            yield return MoveTo(_openA);
            ZNetScene.instance.Destroy(ship);
        }

        private HitData ChopHit(Component target)
        {
            HitData hit = new HitData();
            hit.m_damage.m_chop = 20f;
            hit.m_toolTier = 4;
            hit.m_point = target.transform.position + Vector3.up;
            hit.m_dir = Vector3.forward;
            hit.m_hitType = HitData.HitType.PlayerHit;
            hit.SetAttacker(Me);
            return hit;
        }

        private IEnumerator SoloRank()
        {
            int before = Chat.instance.m_chatBuffer.Count;
            Command("rank");
            yield return Wait(2f);
            List<string> lines = Chat.instance.m_chatBuffer.Skip(Math.Max(0, before - 1)).ToList();
            // O Dummy me matou fora da arena 5 vezes (defesa minha, PK, desafio, queda logo depois
            // do golpe e defesa dele). A arena e as mortes de PvE (queda sozinha, javali) nao contam.
            string mine = lines.LastOrDefault(l => l.StartsWith("Voce:")) ?? "";
            Check("rank/minha-linha", mine.Contains("K 0  D 5"), mine);
            Check("rank/dummy", lines.Any(l => l.Contains(DummyName) && l.Contains("K 5  D 0")), string.Join(" / ", lines));
        }
    }
}
