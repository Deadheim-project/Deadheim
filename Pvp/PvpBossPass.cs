using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// O passe de chefe do NpcValheim (NpcValheim.BossPassApi) na faixa de PvP: quem tem o passe de
    /// um chefe, ganho ajudando a matar ou comprado no Mestre dos Passes, conta como quem o matou
    /// (BossPassCounts). Ligado por reflexao, como o Montarias faz: nenhum dos dois mods precisa do
    /// outro para compilar ou carregar, e sem o NpcValheim so a morte no perfil conta.
    ///
    /// Os passes do jogador local vem do servidor do NpcValheim. Pedimos uma vez por sessao (de novo
    /// a cada RequestEvery ate a resposta chegar); depois o proprio NpcValheim os atualiza na compra
    /// e na morte do chefe.
    /// </summary>
    internal static class PvpBossPass
    {
        private const string TypeName = "NpcValheim.BossPassApi";
        private const int KnownVersion = 1;
        private const float RequestEvery = 10f;

        // O BossOrder usa o nome interno do inimigo; o passe, o prefab da criatura.
        private static readonly Dictionary<string, string> VanillaPrefabs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "$enemy_eikthyr", "Eikthyr" },
            { "$enemy_gdking", "gd_king" },
            { "$enemy_bonemass", "Bonemass" },
            { "$enemy_dragon", "Dragon" },
            { "$enemy_goblinking", "GoblinKing" },
            { "$enemy_seekerqueen", "SeekerQueen" },
            { "$enemy_fader", "Fader" },
        };

        private static readonly Dictionary<string, string> FoundPrefabs = new Dictionary<string, string>(StringComparer.Ordinal);

        private static bool _resolved;
        private static MethodInfo _known;
        private static MethodInfo _has;
        private static MethodInfo _request;
        private static float _nextRequest;

        private static bool Available
        {
            get
            {
                Resolve();
                return _has != null;
            }
        }

        /// <summary>Sessao nova: pede os passes de novo (o NpcValheim tambem esquece os da anterior).</summary>
        public static void ResetSession() => _nextRequest = 0f;

        /// <summary>
        /// A posicao em <paramref name="order"/> do chefe mais avancado cujo passe o jogador local tem.
        /// 0 = nenhum, sem NpcValheim, com BossPassCounts desligado ou com os passes ainda a caminho.
        /// </summary>
        public static int LocalTier(List<string> order)
        {
            if (PvpConfig.BossPassCounts == null || !PvpConfig.BossPassCounts.Value || order == null || !Available) return 0;
            if (!(Call(_known) is bool known && known))
            {
                if (Time.time >= _nextRequest)
                {
                    _nextRequest = Time.time + RequestEvery;
                    Call(_request);
                }
                return 0;
            }

            for (int i = order.Count - 1; i >= 0; i--)
            {
                string prefab = PrefabOf(order[i]);
                if (prefab != null && Call(_has, prefab) is bool has && has) return i + 1;
            }
            return 0;
        }

        private static string PrefabOf(string token)
        {
            if (VanillaPrefabs.TryGetValue(token, out string prefab)) return prefab;
            if (FoundPrefabs.TryGetValue(token, out prefab)) return prefab;
            if (ZNetScene.instance == null) return null;

            // Chefe de outro mod no BossOrder: o prefab de chefe com esse nome interno.
            prefab = ZNetScene.instance.m_prefabs
                .Where(p => p != null)
                .Select(p => p.GetComponent<Character>())
                .FirstOrDefault(c => c != null && c.m_boss && c.m_name == token)?.gameObject.name;
            FoundPrefabs[token] = prefab;
            return prefab;
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                Type api = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(TypeName, false))
                    .FirstOrDefault(t => t != null);
                if (api == null) return;
                FieldInfo version = api.GetField("Version", BindingFlags.Public | BindingFlags.Static);
                if (!(version?.GetRawConstantValue() is int v) || v != KnownVersion)
                {
                    Debug.LogWarning("[Deadheim PvP] BossPassApi do NpcValheim em versao desconhecida: a faixa por chefes conta so as mortes do perfil.");
                    return;
                }
                _known = api.GetMethod("LocalKnown", BindingFlags.Public | BindingFlags.Static);
                _request = api.GetMethod("RequestLocalStatus", BindingFlags.Public | BindingFlags.Static);
                MethodInfo has = api.GetMethod("LocalHasPass", BindingFlags.Public | BindingFlags.Static);
                if (_known != null && _request != null && has != null)
                {
                    _has = has;
                    Debug.Log("[Deadheim PvP] Faixa por chefes: o passe de chefe do NpcValheim tambem conta.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Deadheim PvP] Nao foi possivel ligar a BossPassApi do NpcValheim: " + ex.Message);
            }
        }

        private static object Call(MethodInfo method, params object[] args)
        {
            if (method == null) return null;
            try
            {
                return method.Invoke(null, args);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Deadheim PvP] BossPassApi.{method.Name} falhou: {ex.InnerException?.Message ?? ex.Message}");
                return null;
            }
        }
    }
}
