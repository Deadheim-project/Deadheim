// Driver de teste de ponta a ponta do modulo de PvP do Deadheim.
//
// NUNCA vai para jogador. Dois clientes reais (papeis A e B) conectam num servidor
// dedicado local e seguem o mesmo roteiro, sincronizados por arquivos numa pasta
// comum: cada fase roda a parte de A e a de B e espera o outro terminar. Cada checagem
// escreve "[PVPTEST] PASS|FAIL <nome> <detalhe>" no LogOutput.log do cliente; no fim,
// "[PVPTEST] DONE pass=N fail=M".
//
// Argumentos (depois do +connect/-password que o DirectJoinFlow do Deadheim ja usa):
//   -dhtest-role A|B     papel deste cliente
//   -dhtest-sync <dir>   pasta de sincronizacao, a mesma para os dois
//   -dhtest-save <dir>   pasta dos personagens de teste (nao toca nos do jogador)
using BepInEx;
using BepInEx.Logging;
using Deadheim.Pvp;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PvpTestDriver
{
    [BepInPlugin("Detalhes.PvpTestDriver", "PvpTestDriver", "1.0.0")]
    [BepInDependency("Detalhes.Deadheim")]
    public partial class Driver : BaseUnityPlugin
    {
        private const float Hit = 10f;

        internal static Driver Instance;
        internal static float DamageTaken;

        private static ManualLogSource _log;

        private string _role;
        private string _sync;
        private string _myName;
        private string _otherName;
        private bool _creatingCharacter;
        private float _characterPanelSince = -1f;
        private bool _started;
        private int _phase;
        private int _pass;
        private int _fail;

        private Vector3 _temple;
        private Vector3 _safe;
        private Vector3 _openA;
        private Vector3 _openB;
        private Vector3 _wardA;
        private Vector3 _wardB;
        private Vector3 _ship;
        private float _deathFactor;

        private bool IsA => _role == "A";
        private Player Me => Player.m_localPlayer;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            _role = Arg(args, "-dhtest-role");
            if (string.IsNullOrEmpty(_role))
            {
                enabled = false;
                return;
            }

            _sync = Arg(args, "-dhtest-sync");
            string save = Arg(args, "-dhtest-save");
            if (!string.IsNullOrEmpty(save))
            {
                Directory.CreateDirectory(save);
                Utils.SetSaveDataPath(save);
            }

            // Dois clientes + servidor numa maquina so: texturas em 1/4 e 30 fps para caber
            // na memoria. Nada disso e salvo nas preferencias do jogador.
            QualitySettings.globalTextureMipmapLimit = 2;
            Application.targetFrameRate = 30;

            _myName = _role == "S" ? "Solo" : IsA ? "Alfa" : "Bravo";
            _otherName = IsA ? "Bravo" : "Alfa";
            _log = Logger;
            Instance = this;
            new Harmony("Detalhes.PvpTestDriver").PatchAll();
            Log($"driver ativo: papel={_role} sync={_sync} save={save}");
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static void Log(string text) => _log?.LogInfo("[PVPTEST] " + text);

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) _pass++;
            else _fail++;
            Log($"{(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        // ---------------------------------------------------------------- menu e spawn

        private void Update()
        {
            FejdStartup startup = FejdStartup.instance;
            if (startup != null && startup.m_newCharacterPanel != null && startup.m_newCharacterPanel.activeInHierarchy)
            {
                if (_characterPanelSince < 0f) _characterPanelSince = Time.time;
                if (!_creatingCharacter && Time.time - _characterPanelSince > 1.5f)
                {
                    _creatingCharacter = true;
                    startup.m_csNewCharacterName.text = _myName;
                    Log("criando personagem " + _myName);
                    startup.OnNewCharacterDone(true);
                }
            }

            if (Game.instance != null && Game.instance.InIntro(true) && ZNet.GetConnectionStatus() == ZNet.ConnectionStatus.Connected)
                Game.instance.SkipIntro();

            if (!_started && Me != null && !Me.InCutscene() && !Me.IsTeleporting())
            {
                _started = true;
                StartCoroutine(_role == "S" ? RunSolo() : Run());
            }
        }

        // --------------------------------------------------------------------- roteiro

        private IEnumerator Run()
        {
            Log("spawn ok; esperando o outro cliente");
            yield return new WaitForSeconds(5f);
            // O segundo cliente so e aberto depois que este entra no mundo: o marcador abaixo
            // e o sinal para o orquestrador.
            File.WriteAllText(Path.Combine(_sync, "spawned-" + _role), DateTime.Now.ToString("HH:mm:ss"));
            yield return Barrier("spawn", 900f);

            List<KeyValuePair<string, Func<IEnumerator>>> script = new List<KeyValuePair<string, Func<IEnumerator>>>
            {
                Step("setup", Setup),
                Step("hit-open", HitOpen),
                Step("safe-zone", SafeZone),
                Step("combat-tag", CombatTag),
                Step("attacker-in-safe", AttackerInSafe),
                Step("territory", Territory),
                Step("immunity", Immunity),
                Step("pk", Pk),
                Step("arena", Arena),
                Step("bounty", Bounty),
                Step("tombstone", Tombstone),
                Step("transport", Transport),
                Step("retreat", Retreat),
                Step("rank", Rank),
                Step("coins", Coins),
            };

            foreach (KeyValuePair<string, Func<IEnumerator>> step in script)
            {
                Log("=== " + step.Key);
                yield return RunSafely(step.Value(), step.Key);
                yield return Barrier(step.Key + "-fim", 180f);
            }

            Log($"DONE pass={_pass} fail={_fail}");
            File.WriteAllText(Path.Combine(_sync, "result-" + _role + ".txt"), $"pass={_pass} fail={_fail}");
        }

        private static KeyValuePair<string, Func<IEnumerator>> Step(string name, Func<IEnumerator> body)
            => new KeyValuePair<string, Func<IEnumerator>>(name, body);

        /// <summary>Coroutine que estoura nao pode travar o roteiro nem o outro cliente.</summary>
        private IEnumerator RunSafely(IEnumerator body, string name)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!body.MoveNext()) yield break;
                    current = body.Current;
                }
                catch (Exception ex)
                {
                    Check(name + "/excecao", false, ex.ToString().Replace('\n', ' '));
                    yield break;
                }
                yield return current;
            }
        }

        /// <summary>Espera o outro cliente chegar no mesmo ponto do roteiro.</summary>
        private IEnumerator Barrier(string name, float timeout = 90f)
        {
            _phase++;
            string mine = Path.Combine(_sync, $"{_phase:000}-{name}.{_role}");
            string other = Path.Combine(_sync, $"{_phase:000}-{name}.{(IsA ? "B" : "A")}");
            File.WriteAllText(mine, DateTime.Now.ToString("HH:mm:ss.fff"));
            float until = Time.time + timeout;
            while (!File.Exists(other))
            {
                if (Time.time > until)
                {
                    Check("barreira/" + name, false, "o outro cliente nao chegou");
                    yield break;
                }
                yield return new WaitForSeconds(0.2f);
            }
        }

        private IEnumerator Both(string name) => Barrier(name);

        // ---------------------------------------------------------------- auxiliares

        private Player Other => Player.GetAllPlayers().FirstOrDefault(p => p != null && p != Me && p.GetPlayerName() == _otherName);

        private static Vector3 Offset(Vector3 origin, float x, float z) => new Vector3(origin.x + x, origin.y, origin.z + z);

        private static bool IsLand(Vector3 p)
            => WorldGenerator.instance.GetHeight(p.x, p.z) > ZoneSystem.instance.m_waterLevel + 1.5f;

        /// <summary>Primeiro ponto de terra no anel [minR, maxR], longe dos ja usados. Deterministico.</summary>
        private static Vector3 FindLand(Vector3 center, float minR, float maxR, params Vector3[] avoid)
        {
            for (int a = 0; a < 32; a++)
            {
                float angle = a * Mathf.PI * 2f / 32f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                for (float r = minR; r <= maxR; r += 4f)
                {
                    Vector3 p = center + dir * r;
                    if (!IsLand(p) || !IsLand(p + Vector3.right * 4f) || !IsLand(p + Vector3.forward * 4f) || !IsLand(p - Vector3.right * 4f)) continue;
                    if (avoid.Any(v => Utils.DistanceXZ(v, p) < 28f)) continue;
                    return p;
                }
            }
            return center + new Vector3(minR, 0f, 0f);
        }

        private IEnumerator MoveTo(Vector3 target)
        {
            Player me = Me;
            if (me == null) yield break;
            Vector3 p = target;
            p.y = ZoneSystem.instance.GetSolidHeight(p) + 0.3f;
            me.transform.position = p;
            if (me.m_body != null)
            {
                me.m_body.position = p;
                me.m_body.linearVelocity = Vector3.zero;
            }
            me.m_maxAirAltitude = p.y;
            yield return new WaitForSeconds(1.2f);
        }

        private IEnumerator WaitOther(float timeout = 20f)
        {
            float until = Time.time + timeout;
            while (Other == null && Time.time < until) yield return new WaitForSeconds(0.25f);
        }

        /// <summary>
        /// Um golpe de <paramref name="damage"/> de corte do jogador local no outro. Por padrao
        /// vai direto no RPC_Damage (sem passar pela checagem do atacante): e o cliente da
        /// vitima que tem que barrar, porque um cliente modificado pularia a do atacante.
        /// </summary>
        private bool Strike(float damage, bool raw = true)
        {
            Player other = Other;
            Player me = Me;
            if (other == null || me == null)
            {
                Check("golpe/alvo", false, "outro jogador nao carregado");
                return false;
            }

            HitData hit = new HitData();
            hit.m_damage.m_slash = damage;
            hit.m_point = other.GetCenterPoint();
            hit.m_dir = (other.transform.position - me.transform.position).normalized;
            hit.m_hitType = HitData.HitType.PlayerHit;
            hit.m_skill = Skills.SkillType.Swords;
            hit.m_blockable = false;
            hit.m_dodgeable = false;
            hit.m_staggerMultiplier = 0f;
            hit.m_pushForce = 0f;
            hit.SetAttacker(me);
            if (raw) other.m_nview.InvokeRPC("RPC_Damage", hit);
            else other.Damage(hit);
            return true;
        }

        /// <summary>Proxima morte conta como "dura" (perde skill), sem depender de quando foi a anterior.</summary>
        private void ArmHardDeath()
        {
            if (Me != null) Me.m_timeSinceDeath = Me.m_hardDeathCooldown + 999f;
        }

        private IEnumerator ExpectDamage(string name, float expected, float settle = 2f)
        {
            yield return new WaitForSeconds(settle);
            Check(name, Mathf.Abs(DamageTaken - expected) < 0.05f, $"esperado={expected:0.##} recebido={DamageTaken:0.##}");
        }

        private IEnumerator WaitRespawn(Player dead, float timeout = 40f)
        {
            float until = Time.time + timeout;
            while (Time.time < until)
            {
                Player me = Me;
                // InCutscene cobre a animacao de acordar: o vanilla ignora dano ate ela acabar.
                if (me != null && me != dead && !me.IsDead() && !me.IsTeleporting() && !me.InCutscene()) break;
                yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(2f);
            Check("renasceu", Me != null && Me != dead && !Me.IsDead());
        }

        // Nivel cru: GetSkillLevel arredonda para baixo (47.5 vira 47).
        private static float Swords(Player p) => p.GetSkills().GetSkill(Skills.SkillType.Swords).m_level;

        private static void SetSwords(Player p, float level)
        {
            Skills.Skill skill = p.GetSkills().GetSkill(Skills.SkillType.Swords);
            skill.m_level = level;
            skill.m_accumulator = 0f;
        }

        private static int CountItem(Player p, string prefab)
        {
            GameObject go = ObjectDB.instance.GetItemPrefab(prefab);
            string name = go.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
            return p.GetInventory().CountItems(name);
        }

        private static void Command(string line)
        {
            Log("comando: /" + line);
            Chat.instance.TryRunCommand(line);
        }

        private PvpFlags OtherFlags => PvpState.FlagsOf(Other);

        private bool OtherPvp => Other != null && Other.IsPVPEnabled();

        private void ClearAllProtection()
        {
            PvpState.ClearImmunity(Me);
            PvpState.ClearPk();
            PvpState.ClearAggressor();
            SetDummy(true, PvpFlags.None);
        }

        private static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        private float CombatWait => PvpConfig.CombatTagSeconds.Value + 1.5f;

        // --------------------------------------------------------------------- fases

        private IEnumerator Setup()
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
            Log($"posicoes: templo={_temple} seguro={_safe} abertoA={_openA} wardA={_wardA} wardB={_wardB} fatorMorte={_deathFactor}");
            Log("ilha: " + PvpZones.DescribeIsland());

            Check("setup/templo-seguro", PvpZones.IsSafeArea(_temple + Vector3.right * 22f) || PvpZones.IsArena(_temple),
                "templo e arredores protegidos");
            Check("setup/aberto-nao-seguro", !PvpZones.IsSafeArea(_openA) && !PvpZones.IsArena(_openA));
            Check("setup/coins-sem-peso", ObjectDB.instance.GetItemPrefab("Coins").GetComponent<ItemDrop>().m_itemData.m_shared.m_weight == 0f);

            SetSwords(Me, 50f);
            if (!IsA) Me.GetInventory().AddItem(ObjectDB.instance.GetItemPrefab("Wood"), 10);

            yield return MoveTo(IsA ? _openA : _openB);
            yield return WaitOther();
            yield return Wait(1.5f);
            Check("setup/vejo-o-outro", Other != null);
            Check("setup/pvp-ativo-no-aberto", Me.IsPVPEnabled() && (PvpState.Current & PvpFlags.Protected) == 0,
                $"flags={PvpState.Current}");
        }

        private IEnumerator HitOpen()
        {
            if (!IsA) DamageTaken = 0f;
            yield return Both("hit-open-pronto");
            if (IsA)
            {
                Strike(Hit);
                yield return Wait(1f);
                Check("hit-open/atacante-em-combate", PvpState.InCombat);
            }
            yield return Both("hit-open-golpe");
            if (!IsA)
            {
                yield return ExpectDamage("hit-open/dano-x0.5", Hit * PvpConfig.DamageMultiplier.Value, 0.5f);
                Check("hit-open/vitima-em-combate", PvpState.InCombat);
            }
            else
            {
                yield return Wait(1f);
                Check("hit-open/vejo-combate-no-outro", (OtherFlags & PvpFlags.Combat) != 0, $"flags={OtherFlags}");
            }
        }

        private IEnumerator SafeZone()
        {
            yield return Wait(CombatWait);
            if (!IsA)
            {
                yield return MoveTo(_safe);
                yield return Wait(1f);
                Check("safe/protegido", (PvpState.Current & PvpFlags.Protected) != 0 && !Me.IsPVPEnabled(), $"flags={PvpState.Current}");
                DamageTaken = 0f;
            }
            yield return Both("safe-pronto");
            if (IsA)
            {
                yield return Wait(1f);
                Check("safe/vejo-protegido", !OtherPvp && (OtherFlags & PvpFlags.Protected) != 0, $"flags={OtherFlags}");
                string hover = Other != null ? Other.GetHoverName() : "";
                Check("safe/nome-com-tag", hover.Contains("SEGURO"), hover);
                Check("safe/atacante-recusa", PvpRules.Check(Me, Other) == PvpRules.Verdict.VictimProtected, PvpRules.Check(Me, Other).ToString());
                Strike(Hit);
            }
            yield return Both("safe-golpe");
            if (!IsA) yield return ExpectDamage("safe/bloqueado", 0f);
        }

        private IEnumerator CombatTag()
        {
            if (!IsA)
            {
                yield return MoveTo(_openB);
                yield return Wait(1f);
                DamageTaken = 0f;
            }
            yield return Both("combat-pronto");
            if (IsA) Strike(Hit);
            yield return Both("combat-golpe1");
            if (!IsA)
            {
                yield return Wait(0.5f);
                yield return MoveTo(_safe);
                Check("combat/em-combate-na-zona-segura", PvpState.InCombat && Me.IsPVPEnabled() && (PvpState.Current & PvpFlags.Protected) == 0,
                    $"flags={PvpState.Current}");
                DamageTaken = 0f;
            }
            yield return Both("combat-fugiu");
            if (IsA)
            {
                yield return Wait(0.5f);
                Strike(Hit);
            }
            yield return Both("combat-golpe2");
            if (!IsA) yield return ExpectDamage("combat/zona-segura-nao-protege-em-combate", Hit * PvpConfig.DamageMultiplier.Value);
            yield return Wait(CombatWait);
            if (!IsA)
            {
                Check("combat/protegido-apos-combate", (PvpState.Current & PvpFlags.Protected) != 0, $"flags={PvpState.Current}");
                DamageTaken = 0f;
            }
            yield return Both("combat-expirou");
            if (IsA)
            {
                yield return Wait(0.5f);
                Strike(Hit);
            }
            yield return Both("combat-golpe3");
            if (!IsA) yield return ExpectDamage("combat/bloqueado-apos-expirar", 0f);
        }

        private IEnumerator AttackerInSafe()
        {
            if (IsA) yield return MoveTo(_safe);
            else yield return MoveTo(_openB);
            yield return Wait(CombatWait);
            if (!IsA) DamageTaken = 0f;
            yield return Both("ataque-seguro-pronto");
            if (IsA)
            {
                Check("atacante-seguro/pvp-off", !Me.IsPVPEnabled(), $"flags={PvpState.Current}");
                Strike(Hit);
            }
            yield return Both("ataque-seguro-golpe");
            if (!IsA) yield return ExpectDamage("atacante-seguro/bloqueado", 0f);
        }

        private PrivateArea SpawnWard(Vector3 at, Player owner, Player permitted, string prefabName = "guard_stone")
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            Vector3 p = at;
            p.y = ZoneSystem.instance.GetSolidHeight(p);
            GameObject go = Instantiate(prefab, p, Quaternion.identity);
            Piece piece = go.GetComponent<Piece>();
            piece.m_creator = owner.GetPlayerID();
            piece.m_nview.GetZDO().Set(ZDOVars.s_creator, owner.GetPlayerID());
            PrivateArea area = go.GetComponent<PrivateArea>();
            area.SetEnabled(true);
            if (permitted != null) area.AddPermitted(permitted.GetPlayerID(), permitted.GetPlayerName());
            Log($"ward em {p} dono={owner.GetPlayerName()} permitido={(permitted != null ? permitted.GetPlayerName() : "-")} raio={area.m_radius}");
            return area;
        }

        private PrivateArea _myWard;

        private IEnumerator Territory()
        {
            yield return Wait(CombatWait);
            yield return MoveTo(IsA ? _wardA + Vector3.right * 2f : _wardA - Vector3.right * 2f);
            yield return WaitOther();
            if (IsA) _myWard = SpawnWard(_wardA, Me, Other);
            yield return Both("territorio-ward");
            if (!IsA)
            {
                yield return Wait(2f);
                Check("territorio/b-no-proprio-territorio", PvpRules.InOwnTerritory(Me.GetPlayerID(), Me.transform.position));
                DamageTaken = 0f;
            }
            yield return Both("territorio-pronto");
            if (IsA)
            {
                Check("territorio/compartilhado", Other != null && PvpRules.SharesTerritory(Me.GetPlayerID(), Other.GetPlayerID(), Other.transform.position));
                Strike(Hit);
            }
            yield return Both("territorio-golpe");
            if (!IsA) yield return ExpectDamage("territorio/donos-sem-fogo-amigo", 0f);

            if (IsA && Other != null)
            {
                _myWard.RemovePermitted(Other.GetPlayerID());
                Log("B removido do ward");
            }
            yield return Both("territorio-removido");
            if (!IsA)
            {
                yield return Wait(2f);
                Check("territorio/b-virou-invasor", !PvpRules.InOwnTerritory(Me.GetPlayerID(), Me.transform.position));
                DamageTaken = 0f;
            }
            yield return Both("territorio-invasor");
            if (IsA) Strike(Hit);
            yield return Both("territorio-golpe2");
            if (!IsA) yield return ExpectDamage("territorio/invasor-dano-normal", Hit * PvpConfig.DamageMultiplier.Value);

            // Defensor no proprio ward (so B tem permissao): dano x0.5 x0.5.
            yield return MoveTo(IsA ? _wardB + Vector3.right * 2f : _wardB - Vector3.right * 2f);
            if (!IsA) _myWard = SpawnWard(_wardB, Me, null);
            yield return Both("defesa-ward");
            if (!IsA)
            {
                yield return Wait(2f);
                Check("defesa/no-proprio-territorio", PvpRules.InOwnTerritory(Me.GetPlayerID(), Me.transform.position));
                DamageTaken = 0f;
            }
            yield return Both("defesa-pronto");
            if (IsA) Strike(Hit);
            yield return Both("defesa-golpe");
            if (!IsA)
                yield return ExpectDamage("defesa/reducao-no-proprio-ward",
                    Hit * PvpConfig.DamageMultiplier.Value * PvpConfig.WardDefenseMultiplier.Value);
            yield return Both("territorio-fim");
            if (_myWard != null) ZNetScene.instance.Destroy(_myWard.gameObject);
            _myWard = null;
        }

        private int _coinsBefore;
        private float _swordsBefore;

        private IEnumerator Immunity()
        {
            yield return MoveTo(IsA ? _openA : _openB);
            yield return WaitOther();
            DamageTaken = 0f;
            yield return Both("imune-pronto");
            if (IsA)
            {
                yield return Wait(1f);
                string hover = Other != null ? Other.GetHoverName() : "";
                Check("imune/vejo-tag", hover.Contains("IMUNE"), hover);
                Strike(Hit);
            }
            yield return Both("imune-golpe-a");
            if (!IsA)
            {
                yield return ExpectDamage("imune/nao-recebe-dano-pvp", 0f);
                Strike(Hit);
            }
            yield return Both("imune-golpe-b");
            if (IsA) yield return ExpectDamage("imune/nao-causa-dano-pvp", 0f);
        }

        private IEnumerator Pk()
        {
            if (!IsA)
            {
                ClearAllProtection();
                SetSwords(Me, 50f);
            }
            else SetSwords(Me, 50f);
            yield return Wait(CombatWait);
            yield return Both("pk-pronto");
            Player dead = Me;
            if (IsA)
            {
                Strike(1000f);
                yield return Wait(4f);
                Check("pk/matador-vira-pk", PvpState.IsPk && (PvpState.Current & PvpFlags.Pk) != 0, $"flags={PvpState.Current}");
            }
            else
            {
                yield return WaitRespawn(dead);
                yield return Wait(1f);
                Check("pk/imune-de-novo", PvpState.IsImmune);
            }
            yield return Both("pk-marcado");

            // B mata o PK: A perde skill em dobro e deixa de ser PK; B nao vira PK.
            yield return MoveTo(IsA ? _openA : _openB);
            yield return WaitOther();
            if (!IsA)
            {
                ClearAllProtection();
                yield return Wait(1f);
                string hover = Other != null ? Other.GetHoverName() : "";
                Check("pk/vejo-tag-pk", hover.Contains("[PK]"), hover);
            }
            else
            {
                _swordsBefore = Swords(Me);
                ArmHardDeath();
            }
            yield return Wait(CombatWait);
            yield return Both("pk-vinganca-pronto");
            dead = Me;
            if (!IsA)
            {
                Strike(1000f);
                yield return Wait(4f);
                Check("pk/matar-pk-nao-gera-pk", !PvpState.IsPk, $"flags={PvpState.Current}");
            }
            else
            {
                yield return WaitRespawn(dead);
                float expected = _swordsBefore * (1f - Mathf.Clamp01(_deathFactor * PvpConfig.PkSkillLossMultiplier.Value));
                Check("pk/perda-de-skill-em-dobro", Mathf.Abs(Swords(Me) - expected) < 0.05f, $"antes={_swordsBefore} depois={Swords(Me)} esperado={expected}");
                yield return Wait(2f);
                Check("pk/marca-some-ao-morrer", !PvpState.IsPk, $"flags={PvpState.Current}");
            }
        }

        private IEnumerator Arena()
        {
            ClearAllProtection();
            yield return MoveTo(IsA ? _temple + Vector3.right * 2f : _temple - Vector3.right * 2f);
            yield return WaitOther();
            yield return Wait(1f);
            Check("arena/dentro", (PvpState.Current & PvpFlags.Arena) != 0 && Me.IsPVPEnabled(), $"flags={PvpState.Current}");

            // Imunidade nao vale na arena.
            if (IsA)
            {
                PvpState.GrantImmunity(Me, 120d);
                yield return Wait(1f);
                Check("arena/imune-ainda-luta", Me.IsPVPEnabled(), $"flags={PvpState.Current}");
                DamageTaken = 0f;
            }
            yield return Both("arena-pronto");
            if (!IsA) Strike(Hit);
            yield return Both("arena-golpe");
            if (IsA)
            {
                yield return ExpectDamage("arena/imunidade-ignorada", Hit * PvpConfig.DamageMultiplier.Value);
                PvpState.ClearImmunity(Me);
                SetSwords(Me, 50f);
                _swordsBefore = 50f;
                ArmHardDeath();
            }
            yield return Both("arena-kill-pronto");
            Player dead = Me;
            if (!IsA)
            {
                Strike(1000f);
                yield return Wait(4f);
                Check("arena/matar-na-arena-nao-gera-pk", !PvpState.IsPk);
            }
            else
            {
                yield return WaitRespawn(dead);
                Check("arena/sem-perda-de-skill", Mathf.Abs(Swords(Me) - _swordsBefore) < 0.05f, $"antes={_swordsBefore} depois={Swords(Me)}");
                Check("arena/morte-na-arena-sem-imunidade", !PvpState.IsImmune);
            }
        }

        /// <summary>A coloca bounty em B (moedas saem do inventario), B vira CACADO, A mata e leva a parte dele.</summary>
        private IEnumerator Bounty()
        {
            const int pot = 1000;
            ClearAllProtection();
            yield return MoveTo(IsA ? _openA : _safe);
            yield return Wait(CombatWait);
            if (IsA)
            {
                Me.GetInventory().AddItem(ObjectDB.instance.GetItemPrefab("Coins"), pot);
                int before = CountItem(Me, "Coins");
                Command($"bounty {_otherName} {pot}");
                yield return Wait(2f);
                Check("desafio/bounty-cobrou", CountItem(Me, "Coins") == before - pot, $"antes={before} depois={CountItem(Me, "Coins")}");
            }
            yield return Both("desafio-colocada");
            if (!IsA)
            {
                Check("desafio/pendente", PvpState.IsHuntPending && PvpClient.BountyPot == pot, $"flags={PvpState.Current} pote={PvpClient.BountyPot}");
                yield return Wait(PvpConfig.BountyDelaySeconds.Value + 2f);
                Check("desafio/cacado", PvpState.IsHunted && Me.IsPVPEnabled() && (PvpState.Current & PvpFlags.Protected) == 0,
                    $"flags={PvpState.Current} zona={PvpZones.SafeAreaName(Me.transform.position)}");
                DamageTaken = 0f;
            }
            yield return Both("desafio-cacado");
            if (IsA)
            {
                _coinsBefore = CountItem(Me, "Coins");
                yield return Wait(3f);
                List<ZNet.PlayerInfo> onMap = new List<ZNet.PlayerInfo>();
                ZNet.instance.GetOtherPublicPlayers(onMap);
                ZNet.PlayerInfo hunted = onMap.FirstOrDefault(p => p.m_name == _otherName);
                Check("desafio/cacado-no-mapa", hunted.m_name == _otherName && Utils.DistanceXZ(hunted.m_position, _safe) < 20f,
                    $"entradas={onMap.Count} pos={hunted.m_position}");
                Strike(Hit);
            }
            yield return Both("desafio-golpe");
            if (!IsA) yield return ExpectDamage("desafio/sem-zona-segura", Hit * PvpConfig.DamageMultiplier.Value);
            yield return Both("desafio-kill-pronto");
            Player dead = Me;
            if (IsA)
            {
                Strike(1000f);
                yield return Wait(5f);
                int expected = Mathf.FloorToInt(pot * PvpConfig.BountyKillerSharePercent.Value / 100f);
                int gained = CountItem(Me, "Coins") - _coinsBefore;
                Check("desafio/recompensa-do-cacador", gained == expected, $"ganhou={gained} esperado={expected}");
            }
            else
            {
                yield return WaitRespawn(dead);
                yield return Wait(2f);
                Check("desafio/terminou", !PvpState.IsHunted && !PvpState.IsHuntPending, $"flags={PvpState.Current}");
            }
        }

        private IEnumerator Tombstone()
        {
            yield return Wait(1f);
            long bravo = IsA ? (Other != null ? Other.GetPlayerID() : 0L) : Me.GetPlayerID();
            TombStone tomb = FindObjectsByType<TombStone>(FindObjectsSortMode.None)
                .Where(t => t.GetOwner() == bravo && t.m_container.GetInventory().NrOfItems() > 0)
                .OrderBy(t => Utils.DistanceXZ(t.transform.position, Me.transform.position))
                .FirstOrDefault();
            if (IsA)
            {
                Check("tumba/existe", tomb != null, "tumbas do Bravo com itens");
                if (tomb != null)
                {
                    bool opened = tomb.Interact(Me, false, false);
                    Check("tumba/alheia-trancada", !opened && !InventoryGui.IsVisible(), $"abriu={opened}");
                    Check("tumba/hover-trancada", tomb.GetHoverText().Contains("Trancada"));
                }
            }
            yield return Both("tumba-a");
            if (!IsA && tomb != null)
            {
                bool opened = tomb.Interact(Me, false, false);
                Check("tumba/dono-abre", opened);
                yield return Wait(1f);
                if (InventoryGui.IsVisible()) InventoryGui.instance.Hide();
            }
        }

        private IEnumerator Transport()
        {
            yield return MoveTo(IsA ? _openA : _openB);
            ClearAllProtection();
            yield return Wait(CombatWait);
            if (IsA)
            {
                Vector3 p = _ship;
                p.y = ZoneSystem.instance.GetSolidHeight(p) + 0.5f;
                GameObject ship = Instantiate(ZNetScene.instance.GetPrefab("Raft"), p, Quaternion.identity);
                yield return Wait(2f);
                WearNTear wnt = ship.GetComponent<WearNTear>();
                float before = wnt.GetHealthPercentage();
                HitData hit = new HitData();
                hit.m_damage.m_blunt = 50f;
                hit.m_point = ship.transform.position;
                hit.SetAttacker(Me);
                wnt.Damage(hit);
                yield return Wait(1.5f);
                Check("transporte/barco-invulneravel", Mathf.Approximately(before, wnt.GetHealthPercentage()), $"antes={before} depois={wnt.GetHealthPercentage()}");
            }
            yield return Both("transporte-barco");
            if (!IsA)
            {
                Ship raft = FindObjectsByType<Ship>(FindObjectsSortMode.None).OrderBy(s => Utils.DistanceXZ(s.transform.position, _ship)).FirstOrDefault();
                if (raft != null)
                {
                    Vector3 deck = raft.transform.position + Vector3.up * 1.2f;
                    Me.transform.position = deck;
                    Me.m_body.position = deck;
                    Me.m_body.linearVelocity = Vector3.zero;
                    yield return Wait(1.5f);
                    Check("transporte/no-barco-protegido", PvpZones.IsOnTransport(Me) && (PvpState.Current & PvpFlags.Protected) != 0,
                        $"volumes={Me.InNumShipVolumes} flags={PvpState.Current}");
                }
                else Check("transporte/barco-carregado", false);
                yield return MoveTo(_openB);
            }
        }

        private IEnumerator Retreat()
        {
            if (_role != "B")
            {
                PvpState.MarkCombat();
                string refusal = PvpModule.RetreatRefusal(Me);
                Check("retreat/bloqueado-em-combate", refusal != null && refusal.Contains("combate"), refusal);
                yield return Wait(CombatWait);
                Check("retreat/livre-fora-de-combate", PvpModule.RetreatRefusal(Me) == null, PvpModule.RetreatRefusal(Me));
                // Luta com monstro tambem segura o retreat (RetreatBlockedByPveCombat), mesmo sem CombatFromPve.
                PvpState.MarkPveCombat();
                refusal = PvpModule.RetreatRefusal(Me);
                Check("retreat/bloqueado-em-luta-com-monstro", refusal != null && refusal.Contains("monstro"), refusal);
                yield return Wait(0.4f);
                StatusEffect fight = Me.GetSEMan().GetStatusEffect(PvpHud.CombatHash);
                Check("retreat/buff-luta-com-monstro", fight != null && fight.m_name == "Luta com monstro",
                    fight == null ? "sem buff" : fight.m_name);
                yield return Wait(CombatWait);
                Check("retreat/livre-depois-da-luta-com-monstro", PvpModule.RetreatRefusal(Me) == null, PvpModule.RetreatRefusal(Me));
                PvpModule.MarkRetreatUsed(Me);
                refusal = PvpModule.RetreatRefusal(Me);
                Check("retreat/cooldown", refusal != null && refusal.Contains("recarga"), refusal);
                Check("retreat/recarga-gravada-como-tempo-restante", Me.m_customData.ContainsKey("dh_retreatLeft")
                                                                     && !Me.m_customData.ContainsKey("dh_retreatAt"));
                yield return HearthstoneStone();
            }
            yield break;
        }

        /// <summary>
        /// A pedra do mod Hearthstone de verdade (T2): consumir o item com o mod carregado. Em luta
        /// (com monstro ou com jogador) a pedra nao pode teleportar nem sumir: no HarmonyX o prefixo
        /// do Hearthstone roda mesmo com o Deadheim recusando, e so nao gasta porque le
        /// __runOriginal (A1). Fora de luta ela teleporta para o ponto e gasta uma.
        /// </summary>
        private IEnumerator HearthstoneStone()
        {
            GameObject prefab = ObjectDB.instance.GetItemPrefab("Hearthstone");
            Check("pedra/mod-hearthstone-carregado", prefab != null);
            if (prefab == null) yield break;
            if (!Me.IsTeleportable(false))
            {
                Log("SKIP pedra: o inventario tem item que nao teleporta (o Hearthstone recusaria por isso, nao pela luta)");
                yield break;
            }

            yield return Wait(CombatWait);
            Vector3 home = Me.transform.position;
            Deadheim.Retreat.SetHearthStonePosition();
            yield return MoveTo(home + Vector3.forward * 30f);
            string name = prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
            Me.GetInventory().AddItem(prefab, 2);
            int stones = CountItem(Me, "Hearthstone");

            foreach (bool pvp in new[] { false, true })
            {
                string what = pvp ? "combate-pvp" : "luta-com-monstro";
                if (pvp) PvpState.MarkCombat();
                else PvpState.MarkPveCombat();
                Vector3 before = Me.transform.position;
                bool used = Me.ConsumeItem(Me.GetInventory(), Me.GetInventory().GetItem(name));
                yield return Wait(1.5f);
                Check($"pedra/bloqueada-em-{what}", !used && !Me.IsTeleporting() && CountItem(Me, "Hearthstone") == stones
                                                   && Utils.DistanceXZ(Me.transform.position, before) < 3f,
                    $"usou={used} pedras={CountItem(Me, "Hearthstone")}/{stones} andou={Utils.DistanceXZ(Me.transform.position, before):0.0}");
                yield return Wait(CombatWait);
            }

            bool went = Me.ConsumeItem(Me.GetInventory(), Me.GetInventory().GetItem(name));
            float until = Time.time + 20f;
            while (Time.time < until && (Me.IsTeleporting() || Utils.DistanceXZ(Me.transform.position, home) > 3f)) yield return Wait(0.5f);
            Check("pedra/fora-de-luta-teleporta-e-gasta-uma", went && CountItem(Me, "Hearthstone") == stones - 1
                                                             && Utils.DistanceXZ(Me.transform.position, home) < 3f,
                $"usou={went} pedras={CountItem(Me, "Hearthstone")}/{stones} distancia={Utils.DistanceXZ(Me.transform.position, home):0.0}");
            Me.GetInventory().RemoveItem(name, CountItem(Me, "Hearthstone"));
        }

        private IEnumerator Rank()
        {
            int before = Chat.instance.m_chatBuffer.Count;
            Command("rank");
            yield return Wait(2f);
            List<string> lines = Chat.instance.m_chatBuffer.Skip(Math.Max(0, before - 1)).ToList();
            string dump = string.Join(" / ", lines);
            // A: matou B no PK e no desafio (2), morreu para B uma vez (1). B: 1 abate, 2 mortes.
            // Arena nao conta.
            string mine = lines.LastOrDefault(l => l.StartsWith("Voce:")) ?? "";
            if (IsA) Check("rank/alfa", mine.Contains("K 2  D 1"), mine);
            else Check("rank/bravo", mine.Contains("K 1  D 2"), mine);
            Check("rank/lista", lines.Any(l => l.Contains("Alfa")) && lines.Any(l => l.Contains("Bravo")), dump);
        }

        private IEnumerator Coins()
        {
            ItemDrop.ItemData.SharedData coins = ObjectDB.instance.GetItemPrefab("Coins").GetComponent<ItemDrop>().m_itemData.m_shared;
            Check("coins/peso-zero", coins.m_weight == 0f, "peso=" + coins.m_weight);
            Check("coins/pilha-5000", coins.m_maxStackSize == PvpConfig.CoinsMaxStack.Value, "pilha=" + coins.m_maxStackSize);
            yield break;
        }
    }

    /// <summary>
    /// O mapa-mundi aloca texturas grandes que o teste nao usa (o mapa e conferido pela lista
    /// de jogadores publicos, nao pela imagem). Com dois clientes na mesma maquina ele
    /// estourava memoria (OutOfMemoryException em GenerateWorldMap).
    /// </summary>
    [HarmonyPatch(typeof(Minimap), "GenerateWorldMap")]
    internal static class SkipWorldMap
    {
        private static bool Prefix() => false;
    }

    /// <summary>
    /// Sem spawn natural no mundo de teste: um javali que nasce no templo mata o personagem
    /// de 25 de vida no meio do roteiro e a checagem falha por um motivo que nao e o testado.
    /// </summary>
    [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawning")]
    internal static class NoNaturalSpawns
    {
        private static bool Prefix() => false;
    }

    /// <summary>Soma o dano que o jogador local realmente tomou (depois de todos os modificadores).</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    internal static class DamageProbe
    {
        private static void Postfix(Character __instance, HitData hit)
        {
            if (__instance != Player.m_localPlayer || hit == null) return;
            Driver.DamageTaken += hit.GetTotalDamage();
        }
    }
}
