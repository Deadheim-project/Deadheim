using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Deadheim.Ajustes
{
    /// <summary>Um mod com configuracao, como aparece na coluna da esquerda da janela.</summary>
    internal sealed class ModAjustes
    {
        public string Guid = "";
        public string Nome = "";
        public string Versao = "";
        public int Itens;
    }

    internal enum TipoEditor
    {
        Texto = 0,
        LigaDesliga = 1,
        Lista = 2
    }

    /// <summary>
    /// Uma opcao de config pronta para a janela. Valor, padrao e opcoes vao em texto, no
    /// mesmo formato do .cfg (TomlTypeConverter): assim a lista do servidor atravessa a rede
    /// sem o cliente precisar ter os tipos dos mods que so o servidor tem.
    /// </summary>
    internal sealed class ItemAjuste
    {
        public string Secao = "";
        public string Chave = "";
        public string Descricao = "";
        public string Tipo = "";
        public TipoEditor Editor;
        public string[] Opcoes = new string[0];
        public string Faixa = "";
        public string Valor = "";
        public string Padrao = "";
        public bool Sincronizado;
        public bool SomenteLeitura;

        public string Id => Secao + "\n" + Chave;

        public void Escrever(ZPackage pkg)
        {
            pkg.Write(Secao);
            pkg.Write(Chave);
            pkg.Write(Descricao);
            pkg.Write(Tipo);
            pkg.Write((int)Editor);
            pkg.Write(Opcoes.Length);
            foreach (string opcao in Opcoes) pkg.Write(opcao ?? "");
            pkg.Write(Faixa);
            pkg.Write(Valor);
            pkg.Write(Padrao);
            pkg.Write(Sincronizado);
            pkg.Write(SomenteLeitura);
        }

        public static ItemAjuste Ler(ZPackage pkg)
        {
            ItemAjuste item = new ItemAjuste
            {
                Secao = pkg.ReadString(),
                Chave = pkg.ReadString(),
                Descricao = pkg.ReadString(),
                Tipo = pkg.ReadString(),
                Editor = (TipoEditor)pkg.ReadInt()
            };
            int opcoes = pkg.ReadInt();
            item.Opcoes = new string[opcoes];
            for (int i = 0; i < opcoes; i++) item.Opcoes[i] = pkg.ReadString();
            item.Faixa = pkg.ReadString();
            item.Valor = pkg.ReadString();
            item.Padrao = pkg.ReadString();
            item.Sincronizado = pkg.ReadBool();
            item.SomenteLeitura = pkg.ReadBool();
            return item;
        }
    }

    /// <summary>
    /// Le e grava a config dos mods carregados neste processo (cliente ou servidor), do
    /// jeito que o ConfigurationManager faz: pelos ConfigFile que o BepInEx ja tem abertos.
    ///
    /// "Local" e o que o jogador pode mexer: tudo que nao e sincronizado pelo servidor. O
    /// ServerSync (que Deadheim, RaidSystem, Guilds, Groups, CLLC etc. usam) marca as opcoes
    /// sincronizadas com um SyncedConfigEntry nas Tags; o Jotunn, com IsAdminOnly. Os dois
    /// sao lidos por nome e reflexao porque cada mod traz a sua copia dessas classes.
    /// </summary>
    internal static class AjustesDados
    {
        // Descricao enorme so pesa no pacote; a janela mostra duas linhas.
        private const int MaxDescricao = 500;

        // Enum com mais valores que isto (KeyCode tem centenas) vira campo de texto.
        private const int MaxOpcoesLista = 40;

        public static List<ModAjustes> Mods(bool local)
        {
            List<ModAjustes> mods = new List<ModAjustes>();
            foreach (PluginInfo info in Plugins())
            {
                int itens = Entradas(info.Instance.Config).Count(e => Mostra(e, local));
                if (itens == 0) continue;

                mods.Add(new ModAjustes
                {
                    Guid = info.Metadata.GUID,
                    Nome = NomeAmigavel(info),
                    Versao = info.Metadata.Version?.ToString() ?? "",
                    Itens = itens
                });
            }

            // Deadheim primeiro, depois os mods proprios (Detalhes.*), depois o resto.
            return mods
                .OrderBy(m => m.Guid == Plugin.PluginGUID ? 0 : m.Guid.StartsWith("Detalhes.", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                .ThenBy(m => m.Nome, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<ItemAjuste> Itens(string guid, bool local)
        {
            ConfigFile config = Config(guid);
            if (config == null) return new List<ItemAjuste>();

            // Ordena por secao (os mods numeram as secoes, "1 - Geral") e mantem, dentro
            // de cada uma, a ordem em que o autor fez o Bind.
            return Entradas(config)
                .Where(e => Mostra(e, local))
                .OrderBy(e => e.Definition.Section, StringComparer.OrdinalIgnoreCase)
                .Select(Descrever)
                .ToList();
        }

        /// <summary>
        /// Grava <paramref name="valor"/> (texto no formato do .cfg) na opcao. O Value do
        /// ConfigEntry dispara o SettingChanged, e e ele que faz tudo "ao vivo": o mod reage,
        /// o arquivo e salvo e, no servidor, o ServerSync manda o valor novo aos jogadores.
        /// </summary>
        public static bool Aplicar(string guid, string secao, string chave, string valor, bool local,
            out string anterior, out string atual, out string erro)
        {
            anterior = atual = erro = "";

            ConfigEntryBase entrada = Encontrar(guid, secao, chave);
            if (entrada == null)
            {
                erro = "Opcao nao encontrada.";
                return false;
            }

            if (!Mostra(entrada, local) || SomenteLeitura(entrada))
            {
                erro = local ? "Essa opcao e do servidor." : "Essa opcao e somente leitura.";
                return false;
            }

            object novo;
            try
            {
                novo = TomlTypeConverter.ConvertToValue(valor ?? "", entrada.SettingType);
            }
            catch (Exception)
            {
                erro = $"Valor invalido para {TipoAmigavel(entrada.SettingType)}.";
                return false;
            }

            anterior = Texto(entrada, entrada.BoxedValue);
            try
            {
                entrada.BoxedValue = novo;
                if (!entrada.ConfigFile.SaveOnConfigSet) entrada.ConfigFile.Save();
            }
            catch (Exception ex)
            {
                // Um SettingChanged do proprio mod que estoura nao desfaz o valor, mas avisa.
                atual = Texto(entrada, entrada.BoxedValue);
                erro = "O mod recusou o valor: " + ex.Message;
                return false;
            }

            atual = Texto(entrada, entrada.BoxedValue);
            return true;
        }

        // ------------------------------------------------------------------ leitura

        // So o ConfigFile interessa, e ele e objeto comum: "?." em vez do == da Unity.
        private static IEnumerable<PluginInfo> Plugins()
            => Chainloader.PluginInfos.Values.Where(p => p?.Instance?.Config != null);

        private static ConfigFile Config(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            return Chainloader.PluginInfos.TryGetValue(guid, out PluginInfo info) ? info?.Instance?.Config : null;
        }

        private static List<ConfigEntryBase> Entradas(ConfigFile config)
            => config.Select(kv => kv.Value).Where(e => e != null).ToList();

        private static ConfigEntryBase Encontrar(string guid, string secao, string chave)
        {
            ConfigFile config = Config(guid);
            if (config == null) return null;
            return Entradas(config).FirstOrDefault(e => e.Definition.Section == secao && e.Definition.Key == chave);
        }

        private static bool Mostra(ConfigEntryBase entrada, bool local)
        {
            if (Atributo(entrada, "Browsable") is bool visivel && !visivel) return false;
            return !local || !Sincronizada(entrada);
        }

        private static ItemAjuste Descrever(ConfigEntryBase entrada)
        {
            Type tipo = entrada.SettingType;
            ItemAjuste item = new ItemAjuste
            {
                Secao = entrada.Definition.Section ?? "",
                Chave = entrada.Definition.Key ?? "",
                Descricao = Cortar(entrada.Description?.Description ?? "", MaxDescricao),
                Tipo = TipoAmigavel(tipo),
                Valor = Texto(entrada, entrada.BoxedValue),
                Padrao = Texto(entrada, entrada.DefaultValue),
                Sincronizado = Sincronizada(entrada),
                SomenteLeitura = SomenteLeitura(entrada)
            };

            AcceptableValueBase aceitos = entrada.Description?.AcceptableValues;
            Array lista = Propriedade(aceitos, "AcceptableValues") as Array;

            if (tipo == typeof(bool))
            {
                item.Editor = TipoEditor.LigaDesliga;
            }
            else if (lista != null && lista.Length > 0)
            {
                item.Editor = TipoEditor.Lista;
                item.Opcoes = lista.Cast<object>().Select(v => Texto(entrada, v)).ToArray();
            }
            else if (tipo.IsEnum && !tipo.IsDefined(typeof(FlagsAttribute), false) && Enum.GetNames(tipo).Length <= MaxOpcoesLista)
            {
                item.Editor = TipoEditor.Lista;
                item.Opcoes = Enum.GetNames(tipo);
            }
            else
            {
                item.Editor = TipoEditor.Texto;
                object min = Propriedade(aceitos, "MinValue");
                object max = Propriedade(aceitos, "MaxValue");
                if (min != null && max != null) item.Faixa = $"{Texto(entrada, min)} a {Texto(entrada, max)}";
            }

            return item;
        }

        /// <summary>O valor como o .cfg escreve. Sem conversor registrado, o ToString.</summary>
        private static string Texto(ConfigEntryBase entrada, object valor)
        {
            try
            {
                return TomlTypeConverter.ConvertToString(valor, entrada.SettingType) ?? "";
            }
            catch (Exception)
            {
                return valor?.ToString() ?? "";
            }
        }

        private static string NomeAmigavel(PluginInfo info)
        {
            string nome = info.Metadata.Name ?? info.Metadata.GUID;
            // O Deadheim e o RaidSystem usam o GUID como nome ("Detalhes.Deadheim").
            if (nome == info.Metadata.GUID && nome.Contains("."))
                nome = nome.Substring(nome.LastIndexOf('.') + 1);
            return nome;
        }

        private static string TipoAmigavel(Type tipo)
        {
            if (tipo == typeof(bool)) return "liga/desliga";
            if (tipo == typeof(string)) return "texto";
            if (tipo == typeof(int) || tipo == typeof(long) || tipo == typeof(short) || tipo == typeof(byte)) return "numero inteiro";
            if (tipo == typeof(float) || tipo == typeof(double) || tipo == typeof(decimal)) return "numero";
            if (tipo.Name == "KeyCode") return "tecla";
            if (tipo.Name == "KeyboardShortcut") return "atalho (ex.: F1 + LeftControl)";
            if (tipo.Name == "Color") return "cor (RRGGBBAA)";
            if (tipo.IsEnum) return "lista";
            return tipo.Name;
        }

        private static string Cortar(string texto, int max)
            => texto.Length <= max ? texto : texto.Substring(0, max - 3) + "...";

        // ------------------------------------------------------ marcas nas Tags

        /// <summary>
        /// Sincronizada pelo servidor: SyncedConfigEntry do ServerSync com SynchronizedConfig
        /// ligado, ou IsAdminOnly do Jotunn.
        /// </summary>
        private static bool Sincronizada(ConfigEntryBase entrada)
        {
            foreach (object tag in Tags(entrada))
            {
                if (HerdaDe(tag.GetType(), "OwnConfigEntryBase"))
                {
                    FieldInfo campo = tag.GetType().GetField("SynchronizedConfig", BindingFlags.Public | BindingFlags.Instance);
                    if (campo == null || campo.GetValue(tag) is bool sincronizada && sincronizada) return true;
                }
            }
            return Atributo(entrada, "IsAdminOnly") is bool adminOnly && adminOnly;
        }

        /// <summary>ReadOnly do ConfigurationManagerAttributes (o ServerSync liga quando trava).</summary>
        private static bool SomenteLeitura(ConfigEntryBase entrada)
            => Atributo(entrada, "ReadOnly") is bool somenteLeitura && somenteLeitura;

        /// <summary>Campo de algum ConfigurationManagerAttributes nas Tags; null se nenhum define.</summary>
        private static object Atributo(ConfigEntryBase entrada, string nome)
        {
            foreach (object tag in Tags(entrada))
            {
                Type tipo = tag.GetType();
                if (tipo.Name != "ConfigurationManagerAttributes") continue;

                FieldInfo campo = tipo.GetField(nome, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object valor = campo?.GetValue(tag);
                if (valor != null) return valor;
            }
            return null;
        }

        private static IEnumerable<object> Tags(ConfigEntryBase entrada)
            => entrada.Description?.Tags?.Where(t => t != null) ?? Enumerable.Empty<object>();

        private static bool HerdaDe(Type tipo, string nome)
        {
            for (Type t = tipo; t != null; t = t.BaseType)
                if (t.Name == nome) return true;
            return false;
        }

        private static object Propriedade(object alvo, string nome)
        {
            if (alvo == null) return null;
            try
            {
                return alvo.GetType().GetProperty(nome, BindingFlags.Public | BindingFlags.Instance)?.GetValue(alvo, null);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
