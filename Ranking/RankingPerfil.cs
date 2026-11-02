using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Deadheim.Ranking
{
    /// <summary>
    /// Le os numeros de um PlayerProfile: os contadores que o proprio jogo registra
    /// (m_playerStats: abates, construcoes, distancia, arvores...), os abates de cada chefe e, do
    /// pacote do personagem (m_playerData), as habilidades e as moedas.
    ///
    /// O mesmo codigo serve o cliente (o perfil do jogador local, relatado a cada minuto) e o
    /// servidor (os perfis que o ServerCharacters guarda em disco, de todo mundo, online ou nao).
    /// O perfil e lido por reflexao: ele mudou de forma entre versoes do jogo (dicionario ou lista
    /// de dicionarios por preset de mundo), e um nome que sumir zera so aquela categoria.
    /// </summary>
    internal static class RankingPerfil
    {
        private static bool _chavesLogadas;

        /// <summary>Contadores do perfil e chefes, no formato do relato (Chave.*).</summary>
        public static void LerEstatisticas(PlayerProfile perfil, IDictionary<string, double> destino)
        {
            if (perfil == null || destino == null) return;
            object stats = Primeiro(Campo(perfil, "m_playerStats"));
            Dictionary<string, double> contadores = Contadores(stats);

            if (!_chavesLogadas && contadores.Count > 0)
            {
                _chavesLogadas = true;
                Debug.Log("[Deadheim] Ranking: contadores do perfil: " + string.Join(", ", contadores.Keys.OrderBy(k => k)));
            }

            Copiar(contadores, destino, Chave.Monstros, "EnemyKills");
            Copiar(contadores, destino, Chave.Mortes, "Deaths");
            Copiar(contadores, destino, Chave.Construcoes, "Builds");
            if (!Copiar(contadores, destino, Chave.Fabricacoes, "CraftsOrUpgrades"))
                destino[Chave.Fabricacoes] = Ler(contadores, "Crafts") + Ler(contadores, "Upgrades");
            Copiar(contadores, destino, Chave.Arvores, "Tree", "TreeChops");
            Copiar(contadores, destino, Chave.Mineracao, "MineHits", "Mines");
            if (!Copiar(contadores, destino, Chave.Distancia, "DistanceTraveled"))
                destino[Chave.Distancia] = Ler(contadores, "DistanceWalk") + Ler(contadores, "DistanceRun")
                                           + Ler(contadores, "DistanceSail") + Ler(contadores, "DistanceAir");
            Copiar(contadores, destino, Chave.Navegacao, "DistanceSail");
            Copiar(contadores, destino, Chave.Domados, "CreatureTamed");

            // Chefes: abates por nome interno, no mesmo lugar que a faixa de PvP le (PvpBosses).
            IDictionary inimigos = Primeiro(Campo(stats, "m_enemyStats")) as IDictionary;
            if (inimigos == null) return;
            int distintos = 0;
            double abates = 0d;
            foreach (string chefe in Pvp.PvpBosses.Order)
            {
                if (!inimigos.Contains(chefe)) continue;
                double n = Convert.ToDouble(inimigos[chefe]);
                if (n <= 0d) continue;
                distintos++;
                abates += n;
                destino[Chave.PrefixoChefe + chefe] = n;
            }
            destino[Chave.Chefes] = distintos;
            destino[Chave.ChefesAbates] = abates;
        }

        // ------------------------------------------------------- pacote do personagem

        private static bool _consumoProcurado;
        private static MethodInfo _ateHabilidades, _ateInventario;

        /// <summary>
        /// O ServerCharacters tem, prontos, os leitores que pulam o pacote do personagem ate as
        /// habilidades e ate o inventario (ServerSide.ConsumePlayerSaveUntilSkills/Inventory,
        /// montados por transpiler do proprio Player.Load). Sem ele, so os contadores.
        /// </summary>
        private static void ProcurarConsumo()
        {
            if (_consumoProcurado) return;
            _consumoProcurado = true;
            Type lado = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("ServerCharacters.ServerSide", false))
                .FirstOrDefault(t => t != null);
            _ateHabilidades = lado?.GetMethod("ConsumePlayerSaveUntilSkills", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            _ateInventario = lado?.GetMethod("ConsumePlayerSaveUntilInventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Debug.Log(_ateHabilidades != null
                ? "[Deadheim] Ranking: habilidades e moedas dos perfis offline lidas pelo ServerCharacters."
                : "[Deadheim] Ranking: ServerCharacters nao encontrado: dos perfis offline so os contadores (habilidades e moedas chegam quando o jogador entra).");
        }

        /// <summary>Soma e maior nivel de habilidade, e moedas, do pacote do personagem (servidor).</summary>
        public static void LerPersonagem(PlayerProfile perfil, IDictionary<string, double> destino)
        {
            byte[] dados = Campo(perfil, "m_playerData") as byte[];
            if (dados == null || dados.Length == 0) return;
            ProcurarConsumo();

            if (_ateHabilidades != null)
            {
                try
                {
                    var pkg = new ZPackage(dados);
                    _ateHabilidades.Invoke(null, new object[] { pkg });
                    // Formato do Skills.Save: versao, quantas, e cada uma: tipo, nivel, acumulado (versao >= 2).
                    int versao = pkg.ReadInt();
                    int quantas = pkg.ReadInt();
                    if (quantas >= 0 && quantas <= 1000)
                    {
                        double soma = 0d, maior = 0d;
                        for (int i = 0; i < quantas; i++)
                        {
                            pkg.ReadInt();
                            float nivel = pkg.ReadSingle();
                            if (versao >= 2) pkg.ReadSingle();
                            if (nivel <= 0f || float.IsNaN(nivel)) continue;
                            soma += nivel;
                            if (nivel > maior) maior = nivel;
                        }
                        destino[Chave.Habilidades] = Math.Floor(soma);
                        destino[Chave.HabilidadeMaior] = Math.Floor(maior);
                    }
                }
                catch (Exception ex) { Debug.LogWarning("[Deadheim] Ranking: habilidades do perfil ilegiveis: " + Raiz(ex)); }
            }

            if (_ateInventario != null)
            {
                try
                {
                    var pkg = new ZPackage(dados);
                    _ateInventario.Invoke(null, new object[] { pkg });
                    // Grade folgada: o inventario do Deadheim pode ter linhas a mais (VIP).
                    var inventario = new Inventory("RankingPerfil", null, 8, 10);
                    inventario.Load(pkg);
                    double moedas = 0d;
                    foreach (ItemDrop.ItemData item in inventario.GetAllItems())
                        if (Moedas.EhMoeda(item)) moedas += item.m_stack;
                    destino[Chave.Moedas] = moedas;
                }
                catch (Exception ex) { Debug.LogWarning("[Deadheim] Ranking: inventario do perfil ilegivel: " + Raiz(ex)); }
            }
        }

        private static string Raiz(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            return ex.Message;
        }

        // ------------------------------------------------------------- reflexao

        internal static object Campo(object dono, string nome)
        {
            if (dono == null) return null;
            FieldInfo campo = dono.GetType().GetField(nome, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return campo?.GetValue(dono);
        }

        /// <summary>Lista ou vetor (um por preset de mundo): o primeiro. Outro valor: ele mesmo.</summary>
        private static object Primeiro(object valor)
        {
            if (valor is IDictionary) return valor;
            if (valor is IList lista) return lista.Count > 0 ? lista[0] : null;
            return valor;
        }

        /// <summary>O dicionario de contadores (chave = PlayerStatType): m_stats, ou o primeiro com chave enum.</summary>
        private static Dictionary<string, double> Contadores(object stats)
        {
            var saida = new Dictionary<string, double>(StringComparer.Ordinal);
            if (stats == null) return saida;

            IDictionary mapa = stats as IDictionary ?? Primeiro(Campo(stats, "m_stats")) as IDictionary;
            if (mapa == null)
            {
                foreach (FieldInfo campo in stats.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    IDictionary candidato = Primeiro(campo.GetValue(stats)) as IDictionary;
                    Type tipo = candidato?.GetType();
                    if (tipo != null && tipo.IsGenericType && tipo.GetGenericArguments()[0].IsEnum)
                    {
                        mapa = candidato;
                        break;
                    }
                }
            }
            if (mapa == null) return saida;

            foreach (DictionaryEntry e in mapa)
            {
                if (e.Key == null) continue;
                try { saida[e.Key.ToString()] = Convert.ToDouble(e.Value); }
                catch (Exception) { }
            }
            return saida;
        }

        private static double Ler(Dictionary<string, double> contadores, string nome)
        {
            double v;
            return contadores.TryGetValue(nome, out v) ? v : 0d;
        }

        private static bool Copiar(Dictionary<string, double> contadores, IDictionary<string, double> destino, string chave, params string[] nomes)
        {
            foreach (string nome in nomes)
            {
                double v;
                if (!contadores.TryGetValue(nome, out v)) continue;
                destino[chave] = v;
                return true;
            }
            return false;
        }
    }
}
