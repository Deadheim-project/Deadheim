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
        private const string CastleName = "CasteloTeste";
        private const string CastleOwner = "Lobos";
        private const float CastleOffset = 53f;

        private Vector3 _castle;
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
                Step("armadura", SoloArmor),
                Step("zona-segura", SoloSafeZone),
                Step("atacante-protegido", SoloAttackerProtected),
                Step("combate", SoloCombatTag),
                Step("fuga", SoloEscape),
                Step("guilda", SoloGuild),
                Step("grupo", SoloGroup),
                Step("territorio", SoloTerritory),
                Step("castelo-invasor", SoloCastleInvader),
                Step("imunidade", SoloImmunity),
                Step("pk", SoloPk),
                Step("agressor", SoloAggressor),
                Step("arena", SoloArena),
                Step("desafio", SoloChallenge),
                Step("desafio-sobrevive", SoloChallengeSurvive),
                Step("morte-pve-x-pvp", SoloDeathCause),
                Step("saque", SoloLoot),
                Step("castelo-defensor", SoloCastleDefender),
                Step("tumba", SoloTombstone),
                Step("transporte", SoloTransport),
                Step("montaria", SoloMount),
                Step("ward-natureza", SoloWardNature),
                Step("retreat", Retreat),
                Step("rank", SoloRank),
                Step("coins", Coins),
                Step("forja", SoloForja),
                // Por ultimo: abre o menu do ESC, e com -Admin o cliente e admin.
                Step("ajustes", SoloAjustes),
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

        // O boneco nao entra numa guilda de verdade: o teste diz a guilda de cada um.
        private readonly Dictionary<long, string> _guilds = new Dictionary<long, string>();

        private void SetGuild(long playerId, string guild)
        {
            if (guild == null) _guilds.Remove(playerId);
            else _guilds[playerId] = guild;
            PvpGuilds.TestOverride = p => p != null && _guilds.TryGetValue(p.GetPlayerID(), out string g) ? g : null;
        }

        private void ClearGuilds()
        {
            _guilds.Clear();
            PvpGuilds.TestOverride = null;
        }

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
            // Mesmo deslocamento que o run-pvp-test.ps1 usa para escrever a zona do RaidSystem.
            _castle = FindLand(new Vector3(_temple.x - CastleOffset, 0f, _temple.z - CastleOffset), 0f, 20f);
            _deathFactor = Me.GetSkills().m_DeathLowerFactor * Game.m_skillReductionRate;
            Log($"posicoes: templo={_temple} seguro={_safe} aberto={_openA} wardA={_wardA} wardB={_wardB} castelo={_castle} fatorMorte={_deathFactor} " +
                $"janelaSemPerda={Me.m_hardDeathCooldown}s");
            Check("setup/raidsystem-ligado", PvpBridge.CastleAt != null && PvpBridge.CastleOwner != null);
            Log("ilha: " + PvpZones.DescribeIsland());
            Log("plugins: " + string.Join(", ", BepInEx.Bootstrap.Chainloader.PluginInfos.Values
                .Select(i => i.Metadata.Name + " " + i.Metadata.Version)));

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
            StatusEffect icon = Me.GetSEMan().GetStatusEffect(PvpHud.CombatStatusHash);
            Check("dummy-bate/icone-em-combate", icon != null && icon.GetRemaningTime() > 0f,
                icon != null ? $"restante={icon.GetRemaningTime():0.0}s" : "sem icone");
        }

        /// <summary>
        /// Com armadura o corte de PvP vale sobre o dano que passou da armadura. Aplicado no
        /// golpe cru, a armadura quadratica do Valheim transformava x0.5 em ~x0.25.
        /// </summary>
        private IEnumerator SoloArmor()
        {
            foreach (string prefab in new[] { "HelmetBronze", "ArmorBronzeChest", "ArmorBronzeLegs" })
            {
                Me.GetInventory().AddItem(ObjectDB.instance.GetItemPrefab(prefab), 1);
                ItemDrop.ItemData item = Me.GetInventory().GetAllItems()
                    .Find(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab);
                if (item != null) Me.EquipItem(item, false);
            }
            yield return Wait(0.5f);
            const float raw = 40f;
            float armor = Me.GetBodyArmor();
            float vanilla = HitData.DamageTypes.ApplyArmor(raw, armor);
            float expected = vanilla * PvpConfig.DamageMultiplier.Value;
            float cutBeforeArmor = HitData.DamageTypes.ApplyArmor(raw * PvpConfig.DamageMultiplier.Value, armor);
            Log($"armadura={armor} golpe={raw} vanilla={vanilla:0.##} esperado={expected:0.##} corteAntesDaArmadura={cutBeforeArmor:0.##}");
            Check("armadura/equipada", armor > raw / 4f, "armadura=" + armor);
            Heal();
            DummyStrikesMe(raw);
            yield return ExpectDamage("armadura/corte-depois-da-armadura", expected, 0.5f);
            Me.UnequipAllItems();
        }

        /// <summary>Teleporte longo (portal, NPC, pedra, retreat) nao funciona em combate.</summary>
        private IEnumerator SoloEscape()
        {
            ClearAllProtection();
            yield return MoveTo(_openA);
            MoveDummy(_openB);
            yield return Wait(CombatWait);
            Heal();
            DummyStrikesMe(Hit);
            yield return Wait(0.5f);
            Vector3 before = Me.transform.position;
            Vector3 far = Ground(_openA + Vector3.forward * 25f);
            bool went = Me.TeleportTo(far, Me.transform.rotation, true);
            yield return Wait(1f);
            Check("fuga/teleporte-bloqueado-em-combate", !went && Utils.DistanceXZ(Me.transform.position, before) < 3f,
                $"teleportou={went} andou={Utils.DistanceXZ(Me.transform.position, before):0.0}");

            yield return Wait(CombatWait);
            went = Me.TeleportTo(far, Me.transform.rotation, true);
            float until = Time.time + 20f;
            while (Time.time < until && (Me.IsTeleporting() || Utils.DistanceXZ(Me.transform.position, far) > 3f)) yield return Wait(0.5f);
            Check("fuga/teleporte-livre-fora-de-combate", went && Utils.DistanceXZ(Me.transform.position, far) < 3f,
                $"teleportou={went} distancia={Utils.DistanceXZ(Me.transform.position, far):0.0}");
            Check("fuga/icone-some-fora-de-combate", Me.GetSEMan().GetStatusEffect(PvpHud.CombatStatusHash) == null);

            // CombatFromPve: apanhar de monstro tambem prende o teleporte (o mod Combat fazia isso).
            yield return MoveTo(_openA);
            SetServerConfig("CombatFromPve", "true");
            yield return WaitFor(() => PvpConfig.CombatFromPve.Value, 20f);
            Check("fuga/combate-pve-ligado-pelo-cfg", PvpConfig.CombatFromPve.Value);
            GameObject boar = Instantiate(ZNetScene.instance.GetPrefab("Boar"), Ground(_openA + Vector3.forward * 4f), Quaternion.identity);
            yield return Wait(1f);
            HitData bite = new HitData { m_hitType = HitData.HitType.EnemyHit, m_point = Me.GetCenterPoint() };
            bite.m_damage.m_pierce = 1f;
            bite.SetAttacker(boar.GetComponent<Character>());
            Me.m_nview.InvokeRPC("RPC_Damage", bite);
            yield return Wait(0.6f);
            before = Me.transform.position;
            went = Me.TeleportTo(far, Me.transform.rotation, true);
            yield return Wait(1f);
            Check("fuga/combate-pve-bloqueia-teleporte", !went && PvpHud.Compose(Me).Contains("(PvE)"), PvpHud.Compose(Me));
            Check("fuga/combate-pve-nao-tira-pvp-da-zona", (PvpState.Current & PvpFlags.Combat) == 0, $"flags={PvpState.Current}");
            ZNetScene.instance.Destroy(boar);
            SetServerConfig("CombatFromPve", "false");
            yield return WaitFor(() => !PvpConfig.CombatFromPve.Value, 20f);
            Check("fuga/combate-pve-desligado-de-novo", !PvpConfig.CombatFromPve.Value);
            yield return Wait(CombatWait);
            yield return MoveTo(_openA);
        }

        /// <summary>Party do mod Groups: mesmo grupo e aliado, sem fogo amigo.</summary>
        private IEnumerator SoloGroup()
        {
            ClearAllProtection();
            yield return MoveTo(_openA);
            MoveDummy(_openB);
            yield return Wait(CombatWait);
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Groups"))
                Check("grupo/mod-groups-carregado", PvpGroups.IsAvailable);
            else
                Log("SKIP grupo/mod-groups-carregado: o Groups nao esta neste teste");

            PvpGroups.TestOverride = id => id == DummyId;
            Check("grupo/mesmo-grupo", PvpGroups.SameGroup(Me, _dummy));
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("grupo/sem-fogo-amigo", 0f, 0.5f);

            PvpGroups.TestOverride = null;
            yield return Wait(CombatWait);
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("grupo/fora-do-grupo-leva-dano", Hit * PvpConfig.DamageMultiplier.Value, 0.5f);
        }

        /// <summary>
        /// Legitima defesa: eu bato primeiro (viro AGRESSOR) e o Dummy me mata. Ele nao vira PK,
        /// mas o abate conta no ranking.
        /// </summary>
        private IEnumerator SoloAggressor()
        {
            ClearAllProtection();
            yield return MoveTo(_openA);
            MoveDummy(_openB);
            yield return Wait(CombatWait);

            // Pelo caminho do atacante (Character.Damage): e onde a marca nasce.
            _dummy.Damage(HitFrom(Me, _dummy, Hit));
            yield return Wait(0.6f);
            Check("agressor/marcado", PvpState.IsAggressor && (PvpState.Current & PvpFlags.Aggressor) != 0, $"flags={PvpState.Current}");
            Check("agressor/hud", PvpHud.Compose(Me).Contains("AGRESSOR"), PvpHud.Compose(Me));

            MarkServerLog();
            yield return DummyKillsMe("agressor");
            yield return Wait(1f);
            string log = ServerLogSinceMark();
            Check("agressor/legitima-defesa-sem-pk", log.Contains("legitima defesa") && !log.Contains($"{DummyName} ({DummyId}) agora e PK"), Tail(log));
            Check("agressor/marca-some-ao-morrer", !PvpState.IsAggressor, $"flags={PvpState.Current}");
            PvpState.ClearImmunity(Me);
        }

        /// <summary>Saque: quem morre para jogador deixa PvpCoinDropPercent das moedas no chao, fora da tumba.</summary>
        private IEnumerator SoloLoot()
        {
            ClearAllProtection();
            yield return MoveTo(_openA);
            MoveDummy(_openB);
            yield return Wait(CombatWait);

            GameObject coinsPrefab = ObjectDB.instance.GetItemPrefab("Coins");
            string coinName = coinsPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
            Inventory inventory = Me.GetInventory();
            inventory.RemoveItem(coinName, inventory.CountItems(coinName));
            inventory.AddItem(coinsPrefab, 1000);
            int expected = Mathf.FloorToInt(1000 * PvpConfig.PvpCoinDropPercent.Value / 100f);
            Vector3 deathAt = Me.transform.position;

            MarkServerLog();
            yield return DummyKillsMe("saque");
            yield return Wait(1.5f);
            List<ItemDrop> coins = ItemDrop.s_instances.Where(d => d != null && d.m_itemData.m_shared.m_name == coinName
                                                                   && Utils.DistanceXZ(d.transform.position, deathAt) < 8f).ToList();
            int onGround = coins.Sum(d => d.m_itemData.m_stack);
            Check("saque/moedas-no-chao", onGround == expected, $"no-chao={onGround} esperado={expected}");
            string log = ServerLogSinceMark();
            Check("saque/servidor-sabe", log.Contains($"moedasNoChao={expected}"), Tail(log));
            foreach (ItemDrop drop in coins) ZNetScene.instance.Destroy(drop.gameObject);
            PvpState.ClearImmunity(Me);
        }

        /// <summary>Muda uma linha do cfg do servidor, com ele ligado (recarga automatica).</summary>
        private void SetServerConfig(string key, string value)
        {
            string path = Path.Combine(Path.GetDirectoryName(_sync.TrimEnd('\\', '/')), "server", "BepInEx", "config", "Detalhes.Deadheim.cfg");
            string text = File.ReadAllText(path);
            string changed = System.Text.RegularExpressions.Regex.Replace(text,
                "(?m)^" + System.Text.RegularExpressions.Regex.Escape(key) + " = .*$", key + " = " + value);
            File.WriteAllText(path, changed);
            Log($"cfg do servidor: {key} = {value} (mudou={changed != text})");
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeout)
        {
            float until = Time.time + timeout;
            while (Time.time < until && !condition()) yield return new WaitForSeconds(0.5f);
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

        private IEnumerator SoloGuild()
        {
            yield return MoveTo(_openA);
            yield return Wait(CombatWait);
            ClearGuilds();
            Check("guilda/mod-guilds-carregado", PvpGuilds.IsAvailable);
            Check("guilda/sem-guilda-de-verdade", PvpGuilds.GuildOf(Me) == null, "guilda=" + PvpGuilds.GuildOf(Me));

            SetGuild(Me.GetPlayerID(), "Lobos");
            SetGuild(DummyId, "Lobos");
            Check("guilda/mesma-guilda", PvpGuilds.SameGuild(Me, _dummy));
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("guilda/sem-fogo-amigo", 0f, 0.5f);
            float before = _dummy.GetHealth();
            IStrikeDummy(Hit);
            yield return ExpectDummyDamage("guilda/sem-fogo-amigo-2", before, 0f);

            SetGuild(DummyId, "Corvos");
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("guilda/outra-guilda-leva-dano", Hit * PvpConfig.DamageMultiplier.Value, 0.5f);
            ClearGuilds();
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

        /// <summary>
        /// Castelo do RaidSystem, eu invasor (Corvos) e o Dummy da guilda dona (Lobos). La a
        /// imunidade nao vale; morrer la nao tira skill nem da imunidade; o Dummy defendeu, entao
        /// o servidor paga a recompensa do bioma e nao o marca como PK.
        /// </summary>
        private IEnumerator SoloCastleInvader()
        {
            ClearAllProtection();
            yield return MoveTo(_castle);
            MoveDummy(_castle + Vector3.right * 2f);
            yield return Wait(1.5f);
            Vector3 pos = Me.transform.position;
            Check("castelo/zona", PvpBridge.Castle(pos) == CastleName && PvpBridge.Owner(pos) == CastleOwner,
                $"castelo={PvpBridge.Castle(pos)} dono={PvpBridge.Owner(pos)}");

            SetGuild(Me.GetPlayerID(), "Corvos");
            SetGuild(DummyId, CastleOwner);
            PvpState.GrantImmunity(Me, 120d);
            yield return Wait(1f);
            Check("castelo/imunidade-nao-vale", Me.IsPVPEnabled() && (PvpState.Current & PvpFlags.Castle) != 0
                                                 && (PvpState.Current & PvpFlags.Immune) == 0, $"flags={PvpState.Current}");
            Check("castelo/hud", PvpHud.Compose(Me).Contains("Castelo"), PvpHud.Compose(Me));
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("castelo/imune-leva-dano", Hit * PvpConfig.DamageMultiplier.Value, 0.5f);

            PvpState.ClearImmunity(Me);
            SetSwords(Me, 50f);
            ArmHardDeath();
            MarkServerLog();
            yield return DummyKillsMe("castelo: invasor");
            Check("castelo-invasor/sem-perda-de-skill", Mathf.Abs(Swords(Me) - 50f) < 0.05f, "skill=" + Swords(Me));
            Check("castelo-invasor/sem-imunidade", !PvpState.IsImmune, $"flags={PvpState.Current}");
            Check("castelo-invasor/nao-abre-janela-sem-perda", Me.m_timeSinceDeath > Me.m_hardDeathCooldown,
                $"desdeAMorte={Me.m_timeSinceDeath:0} janela={Me.m_hardDeathCooldown:0}");
            yield return Wait(1f);

            string log = ServerLogSinceMark();
            int expected = PvpConfig.CastleRewardFor(WorldGenerator.instance.GetBiome(_castle));
            Check("castelo-invasor/defensor-ganha-recompensa", log.Contains($"Recompensa de defesa: {DummyName} +{expected} "), Tail(log));
            Check("castelo-invasor/defensor-nao-vira-pk", !log.Contains($"{DummyName} ({DummyId}) agora e PK"), Tail(log));
            Check("castelo-invasor/raidsystem-recebeu-o-abate", log.Contains($"[RaidSystem] Abate de {DummyName} em {_myName}")
                                                                || log.Contains($"[RaidSystem] Abate registrado: {DummyName}"), Tail(log));
            ClearGuilds();
        }

        private IEnumerator SoloImmunity()
        {
            // Morte por jogador em campo aberto: e ela que da a imunidade testada abaixo.
            ClearAllProtection();
            yield return MoveTo(_openA);
            MoveDummy(_openB);
            yield return Wait(CombatWait);
            yield return DummyKillsMe("imunidade");
            Check("imunidade/concedida-pela-morte-pvp", PvpState.IsImmune, $"flags={PvpState.Current}");
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
            PvpState.ApplyPkCount(Me, 3);
            Check("pk/contador-no-hud", PvpHud.Compose(Me).Contains("x3"), PvpHud.Compose(Me));
            Check("pk/contador-na-zdo", Me.m_nview.GetZDO().GetInt(PvpState.ZdoPkCount, 0) == 3);
            SetSwords(Me, 50f);
            float before = Swords(Me);
            // Acabou de morrer: o vanilla estaria na janela sem perda de skill. PK perde assim mesmo.
            Me.m_timeSinceDeath = 0f;
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

            SetGuild(DummyId, "Lobos");
            SetGuild(Me.GetPlayerID(), "Lobos");
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("arena/fogo-amigo-liberado", Hit * PvpConfig.DamageMultiplier.Value, 0.5f);
            ClearGuilds();

            PvpState.ClearImmunity(Me);
            SetSwords(Me, 50f);
            ArmHardDeath();
            yield return DummyKillsMe("arena");
            Check("arena/sem-perda-de-skill", Mathf.Abs(Swords(Me) - 50f) < 0.05f, "skill=" + Swords(Me));
            Check("arena/sem-imunidade", !PvpState.IsImmune, $"flags={PvpState.Current}");
            Check("arena/nao-abre-janela-sem-perda", Me.m_timeSinceDeath > Me.m_hardDeathCooldown,
                $"desdeAMorte={Me.m_timeSinceDeath:0} janela={Me.m_hardDeathCooldown:0}");
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

            Vector3 here = Me.transform.position;
            bool went = Me.TeleportTo(Ground(_openA), Me.transform.rotation, true);
            yield return Wait(1f);
            Check("desafio/cacado-nao-teleporta", !went && Utils.DistanceXZ(Me.transform.position, here) < 3f,
                $"teleportou={went} andou={Utils.DistanceXZ(Me.transform.position, here):0.0}");

            MarkServerLog();
            yield return MoveTo(_wardA + Vector3.right * 2f);
            MoveDummy(_wardA - Vector3.right * 2f);
            yield return Wait(3f);
            Check("desafio/pausado-no-proprio-ward", PvpClient.HuntPaused && PvpHud.Compose(Me).Contains("pausado"), PvpHud.Compose(Me));
            string pause = ServerLogSinceMark();
            Check("desafio/servidor-pausou", pause.Contains("pausado (dentro do proprio ward)"), Tail(pause));
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("desafio/cacado-sem-defesa-do-ward", Hit * PvpConfig.DamageMultiplier.Value, 0.5f);
            yield return MoveTo(_openA);
            MoveDummy(_openB);
            yield return Wait(3f);
            Check("desafio/retomado-fora-do-ward", !PvpClient.HuntPaused && PvpState.IsHunted, PvpHud.Compose(Me));

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
        /// Castelo, agora eu da guilda dona e o Dummy invasor. Morro sem perder skill (vale para
        /// todos no castelo), ele nao vira PK e nao ha recompensa: quem matou nao defendia.
        /// </summary>
        private IEnumerator SoloCastleDefender()
        {
            ClearAllProtection();
            yield return MoveTo(_castle);
            MoveDummy(_castle + Vector3.right * 2f);
            SetGuild(Me.GetPlayerID(), CastleOwner);
            SetGuild(DummyId, "Corvos");
            yield return Wait(1.5f);

            SetSwords(Me, 50f);
            ArmHardDeath();
            MarkServerLog();
            yield return DummyKillsMe("castelo: defensor");
            Check("castelo-defensor/sem-perda-de-skill", Mathf.Abs(Swords(Me) - 50f) < 0.05f, "skill=" + Swords(Me));
            Check("castelo-defensor/sem-imunidade", !PvpState.IsImmune, $"flags={PvpState.Current}");
            yield return Wait(1f);

            string log = ServerLogSinceMark();
            Check("castelo-defensor/invasor-sem-recompensa", !log.Contains("Recompensa de defesa"), Tail(log));
            Check("castelo-defensor/invasor-nao-vira-pk", !log.Contains($"{DummyName} ({DummyId}) agora e PK"), Tail(log));
            ClearGuilds();
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

            // 4. Veneno de jogador que dura mais que KillCreditSeconds. O tique do veneno nao
            //    tem atacante (SE_Poison chama ApplyDamage direto), mas a morte e de quem envenenou.
            yield return MoveTo(_openA);
            MoveDummy(_openB);
            yield return Wait(CombatWait);
            Heal();
            HitData venomHit = HitFrom(_dummy, Me, 0f);
            venomHit.m_damage.m_poison = 800f;
            Me.m_nview.InvokeRPC("RPC_Damage", venomHit);
            yield return Wait(0.5f);
            StatusEffect venom = Me.GetSEMan().GetStatusEffects().FirstOrDefault(e => e is SE_Poison);
            float lasts = venom != null ? venom.GetRemaningTime() : 0f;
            Check("pvp/veneno-dura-mais-que-o-credito", lasts > PvpConfig.KillCreditSeconds.Value + 3f,
                $"veneno={lasts:0.#}s credito={PvpConfig.KillCreditSeconds.Value}s");
            float until = Time.time + PvpConfig.KillCreditSeconds.Value + 2f;
            while (Time.time < until)
            {
                Me.SetHealth(Me.GetMaxHealth());
                yield return Wait(0.2f);
            }
            dead = Me;
            HitData tick = new HitData { m_hitType = HitData.HitType.Poisoned, m_point = Me.GetCenterPoint() };
            tick.m_damage.m_poison = 1000f;
            Me.ApplyDamage(tick, true, false);
            yield return WaitRespawn(dead);
            Check("pvp/morte-por-veneno-de-jogador-conta-como-jogador", PvpState.IsImmune, $"imune={PvpState.IsImmune} flags={PvpState.Current}");
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

            // Veio de minerio no ward alheio: livre (ProtectNatureOres=false), senao ward tranca minerio.
            GameObject vein = Instantiate(ZNetScene.instance.GetPrefab("MineRock_Tin"), Ground(_wardB + Vector3.back * 3f), Quaternion.identity);
            yield return Wait(0.5f);
            HitData pick = ChopHit(vein.transform);
            pick.m_damage.m_chop = 0f;
            pick.m_damage.m_pickaxe = 20f;
            Check("ward-natureza/estanho-e-veio", Deadheim.Wards.WardPatches.IsOreVein(vein));
            Check("ward-natureza/arvore-nao-e-veio", !Deadheim.Wards.WardPatches.IsOreVein(inside.gameObject));
            Check("ward-natureza/veio-livre-no-ward", !Deadheim.Wards.WardPatches.NatureBlocked(vein.transform.position, pick, vein));
            Check("ward-natureza/arvore-trancada-no-ward", Deadheim.Wards.WardPatches.NatureBlocked(inside.transform.position, ChopHit(inside), inside.gameObject));
            ZNetScene.instance.Destroy(vein);

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
            Check("transporte/barco-parado-nao-protege", !PvpZones.IsOnTransport(Me) && (PvpState.Current & PvpFlags.Protected) == 0,
                $"volumes={Me.InNumShipVolumes} flags={PvpState.Current}");

            // Recarga do cfg com o servidor ligado: o arquivo muda, o servidor rele e o
            // ServerSync entrega o valor novo aqui, sem reiniciar nada.
            MarkServerLog();
            SetServerConfig("ShipSafeMinSpeed", "0");
            yield return WaitFor(() => Mathf.Approximately(PvpConfig.ShipSafeMinSpeed.Value, 0f), 20f);
            Check("config/recarga-chega-no-cliente", Mathf.Approximately(PvpConfig.ShipSafeMinSpeed.Value, 0f),
                "ShipSafeMinSpeed=" + PvpConfig.ShipSafeMinSpeed.Value);
            string reload = ServerLogSinceMark();
            Check("config/servidor-recarregou", reload.Contains("Config recarregada de Detalhes.Deadheim.cfg"), Tail(reload));
            yield return Wait(1f);
            Check("transporte/no-barco-protegido", PvpZones.IsOnTransport(Me) && (PvpState.Current & PvpFlags.Protected) != 0,
                $"volumes={Me.InNumShipVolumes} flags={PvpState.Current}");
            Heal();
            DummyStrikesMe(Hit);
            yield return ExpectDamage("transporte/barco-bloqueia-dano", 0f, 0.5f);
            SetServerConfig("ShipSafeMinSpeed", "1");
            yield return WaitFor(() => Mathf.Approximately(PvpConfig.ShipSafeMinSpeed.Value, 1f), 20f);
            Check("config/recarga-volta", Mathf.Approximately(PvpConfig.ShipSafeMinSpeed.Value, 1f),
                "ShipSafeMinSpeed=" + PvpConfig.ShipSafeMinSpeed.Value);
            yield return MoveTo(_openA);
            ZNetScene.instance.Destroy(ship);
        }

        /// <summary>
        /// Montaria com sela e transporte: nao toma dano de jogador. A estamina da sela vem do
        /// cfg [Montarias] (o antigo SaddleStaminaControl).
        /// </summary>
        private IEnumerator SoloMount()
        {
            ClearAllProtection();
            yield return MoveTo(_openA);
            yield return Wait(CombatWait);
            // Lox: domavel de novo (o Deadheim nao tira mais a doma dele nem a do lobo).
            GameObject go = Instantiate(ZNetScene.instance.GetPrefab("Lox"), Ground(_openA + Vector3.forward * 8f), Quaternion.identity);
            Character lox = go.GetComponent<Character>();
            Tameable tame = go.GetComponent<Tameable>();
            if (lox == null || tame == null)
            {
                Check("montaria/criatura-domavel", false, $"character={lox != null} tameable={tame != null}");
                if (go != null) ZNetScene.instance.Destroy(go);
                yield break;
            }
            lox.SetTamed(true);
            yield return Wait(0.5f);
            lox.m_nview.GetZDO().Set(ZDOVars.s_haveSaddleHash, true);
            tame.SetSaddle(true);
            yield return Wait(0.5f);

            Sadle saddle = go.GetComponentInChildren<Sadle>(true);
            Check("montaria/estamina-da-sela-do-cfg", saddle != null && Mathf.Approximately(saddle.m_maxStamina, Deadheim.Montarias.MaxStamina.Value)
                                                      && Mathf.Approximately(saddle.m_runStaminaDrain, Deadheim.Montarias.RunStaminaDrain.Value),
                saddle != null ? $"max={saddle.m_maxStamina} corrida={saddle.m_runStaminaDrain}" : "sem sela");

            float before = lox.GetHealth();
            lox.m_nview.InvokeRPC("RPC_Damage", HitFrom(Me, lox, 50f));
            yield return Wait(0.6f);
            Check("montaria/com-sela-nao-toma-dano-de-jogador", Mathf.Approximately(before, lox.GetHealth()), $"antes={before} depois={lox.GetHealth()}");

            lox.m_nview.GetZDO().Set(ZDOVars.s_haveSaddleHash, false);
            tame.SetSaddle(false);
            yield return Wait(0.5f);
            before = lox.GetHealth();
            lox.m_nview.InvokeRPC("RPC_Damage", HitFrom(Me, lox, 50f));
            yield return Wait(0.6f);
            Check("montaria/sem-sela-toma-dano", lox.GetHealth() < before, $"antes={before} depois={lox.GetHealth()}");
            ZNetScene.instance.Destroy(go);
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
            // O Dummy me matou fora da arena 9 vezes (castelo como invasor, imunidade, PK, agressor,
            // desafio, queda logo depois do golpe, veneno, saque, castelo como defensor). Arena e
            // PvE (queda sozinha, javali) nao contam.
            string mine = lines.LastOrDefault(l => l.StartsWith("Voce:")) ?? "";
            Check("rank/minha-linha", mine.Contains("K 0  D 9"), mine);
            Check("rank/dummy", lines.Any(l => l.Contains(DummyName) && l.Contains("K 9  D 0")), string.Join(" / ", lines));
            Check("rank/contador-de-pk", lines.Any(l => l.Contains(DummyName) && l.Contains("PK ")), string.Join(" / ", lines));
        }
    }
}
