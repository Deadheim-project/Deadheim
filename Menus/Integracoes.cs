using BepInEx.Bootstrap;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Deadheim.Menus
{
    /// <summary>
    /// Abre as janelas dos outros mods do launcher (NpcValheim, Montarias, AdaptiveNet) a partir
    /// do hub do inventario e da aba Admin. O Deadheim nao referencia esses assemblies: tudo e
    /// por reflexao, com os mesmos passos que a tecla de cada mod fazia (as teclas agora vem vazias
    /// na config de cada mod). Cada metodo devolve null quando abriu, ou a mensagem para o jogador.
    ///
    /// Se um mod mudar um nome, so aquele botao para de funcionar (e o log diz qual nome sumiu);
    /// o resto do hub segue.
    /// </summary>
    internal static class Integracoes
    {
        internal const string NpcValheimGuid = "com.npcvalheim.mod";
        internal const string MontariasGuid = "com.valheimmontarias.mod";
        internal const string AdaptiveNetGuid = "Detalhes.AdaptiveNet";

        internal static bool Carregado(string guid) => Chainloader.PluginInfos.ContainsKey(guid);

        public static bool TemNpcValheim => Carregado(NpcValheimGuid);
        public static bool TemMontarias => Carregado(MontariasGuid);
        public static bool TemAdaptiveNet => Carregado(AdaptiveNetGuid);
        // A Central pergunta isto a cada meio segundo: TipoPorNome guarda o resultado (TypeByName varria
        // todos os assemblies a cada chamada e travava a abertura do inventario).
        public static bool TemGuilds => TipoPorNome("Guilds.Interface") != null;

        /// <summary>
        /// Painel do mod Guilds (blaxxun): o mesmo que a tecla dele abre -- sem guilda a tela de criar/procurar,
        /// com guilda o painel de gestao. ESC fecha (o Update do proprio Guilds cuida).
        /// </summary>
        public static string AbrirGuilds() => Tentar("o painel da guilda", () =>
        {
            Type ui = Tipo("Guilds.Interface");
            Type api = Tipo("Guilds.API");
            MethodInfo own = AccessTools.Method(api, "GetOwnGuild");
            Exigir(own, "Guilds.API.GetOwnGuild");
            string campo = own.Invoke(null, null) == null ? "NoGuildUI" : "GuildManagementUI";
            FieldInfo f = AccessTools.Field(ui, campo);
            Exigir(f, "Guilds.Interface." + campo);
            GameObject painel = f.GetValue(null) as GameObject;
            Exigir(painel, "Guilds.Interface." + campo + " (ainda nao carregado)");
            painel.SetActive(true);
        });

        // ------------------------------------------------------------------ NpcValheim

        /// <summary>A Loja Deadcoins de qualquer lugar (era o F7). O servidor confere o VIP de novo a cada pedido.</summary>
        public static string AbrirLojaDeadcoins()
        {
            if (!TemNpcValheim) return "A Loja Deadcoins vem do mod NpcValheim, que nao esta instalado.";
            return Tentar("Loja Deadcoins", () =>
            {
                Type viewBase = Tipo("NpcValheim.UI.NpcViewBase");
                object view = Criar("NpcValheim.UI.DeadcoinShopView");
                MethodInfo abrir = AccessTools.Method(Tipo("NpcValheim.UI.UiRoot"), "OpenStandalone",
                    new[] { typeof(string), typeof(string), viewBase, typeof(Player) });
                Exigir(abrir, "UiRoot.OpenStandalone(titulo, aba, view, player)");
                // O titulo e o mesmo do atalho antigo: a janela se reconhece por ele.
                abrir.Invoke(null, new object[] { "Loja Deadcoins", "Deadcoins", view, Player.m_localPlayer });
            });
        }

        /// <summary>Diario de missoes (era o J).</summary>
        public static string AbrirDiario()
        {
            if (!TemNpcValheim) return "O diario de missoes vem do mod NpcValheim, que nao esta instalado.";
            return Tentar("Diario de missoes", () =>
            {
                Type tipo = Tipo("NpcValheim.UI.QuestJournal");
                if (AccessTools.Property(tipo, "IsOpen")?.GetValue(null) is bool aberto && aberto) return;
                object instancia = AccessTools.Field(tipo, "_instance")?.GetValue(null);
                Exigir(instancia, "QuestJournal._instance");
                MethodInfo abrir = AccessTools.Method(tipo, "Open");
                Exigir(abrir, "QuestJournal.Open");
                abrir.Invoke(instancia, null);
            });
        }

        /// <summary>Painel da arena: status, times e ranking.</summary>
        public static string AbrirArena()
        {
            if (!TemNpcValheim) return "A arena vem do mod NpcValheim, que nao esta instalado.";
            return Tentar("Arena", () => Invocar("NpcValheim.UI.ArenaHud", "TogglePanel"));
        }

        /// <summary>
        /// Painel de Admin (era o F8 do NpcValheim): mensagem global, jogadores, buffs, bestiario e
        /// arenas, na mesma janela dos NPCs. O servidor confere a adminlist a cada pedido.
        /// </summary>
        public static string AbrirPainelAdmin()
        {
            if (!TemNpcValheim) return "O Painel de Admin vem do mod NpcValheim, que nao esta instalado.";
            return Tentar("Painel de Admin", () =>
            {
                InvocarOpcional("NpcValheim.UI.AdminPanelNet", "ClearStatus");
                AbrirPaginas("Painel de Admin", new[]
                {
                    ("Mensagem", "NpcValheim.UI.AdminMessageView"),
                    ("Jogadores", "NpcValheim.UI.AdminPlayersView"),
                    ("Buffs", "NpcValheim.UI.AdminBuffView"),
                    ("Bestiário", "NpcValheim.UI.BestiaryAdminView"),
                    ("Arena", "NpcValheim.UI.ArenaSetupView"),
                });
                InvocarOpcional("NpcValheim.Arena.ArenaClient", "RequestData");
            });
        }

        private static void AbrirPaginas(string titulo, (string Rotulo, string Tipo)[] paginas)
        {
            Type uiRoot = Tipo("NpcValheim.UI.UiRoot");
            Type viewBase = Tipo("NpcValheim.UI.NpcViewBase");
            Type tupla = typeof(ValueTuple<,>).MakeGenericType(typeof(string), viewBase);
            IList lista = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(tupla));

            // Uma pagina que sumiu numa versao nova do NpcValheim fica de fora; as outras abrem.
            foreach ((string rotulo, string nomeTipo) in paginas)
            {
                Type tipoView = TipoPorNome(nomeTipo);
                if (tipoView == null)
                {
                    Debug.LogWarning($"[Deadheim] Menus: {nomeTipo} nao existe nesta versao do NpcValheim; a aba '{rotulo}' fica de fora.");
                    continue;
                }
                lista.Add(Activator.CreateInstance(tupla, rotulo, Activator.CreateInstance(tipoView, true)));
            }
            if (lista.Count == 0) throw new MissingMemberException("nenhuma pagina do painel encontrada");

            MethodInfo abrir = AccessTools.Method(uiRoot, "OpenStandalone",
                new[] { typeof(string), typeof(IList<>).MakeGenericType(tupla), typeof(Player) });
            Exigir(abrir, "UiRoot.OpenStandalone(titulo, paginas, player)");
            abrir.Invoke(null, new object[] { titulo, lista, Player.m_localPlayer });
        }

        // ------------------------------------------------------------------- Montarias

        /// <summary>Menu de montarias (era o U).</summary>
        public static string AbrirMontarias()
        {
            if (!TemMontarias) return "O menu de montarias vem do mod Montarias, que nao esta instalado.";
            return Tentar("Montarias", () => Invocar("ValheimMontarias.MountMenu", "Open"));
        }

        /// <summary>Menu de pets (lista, bonus e invocar/guardar), que mora no mod Montarias.</summary>
        public const string InventarioGuid = "Deadheim.InventoryUI";
        public static bool TemInventario => Carregado(InventarioGuid);

        /// <summary>Icone Ficha: abre/fecha o painel do mod Deadheim Inventory UI (sem fechar o inventario).</summary>
        public static string AlternarFicha()
        {
            if (!TemInventario) return "A ficha vem do mod Deadheim Inventory UI, que nao esta instalado.";
            return Tentar("a ficha do personagem", () => Invocar("Deadheim.InventoryUI.InventoryUiApi", "Alternar"));
        }

        private static PropertyInfo _fichaSempre;

        /// <summary>Estado da marca "abrir sempre" da ficha (lido por reflexao, guardado o PropertyInfo).</summary>
        public static bool FichaSempre()
        {
            try
            {
                if (_fichaSempre == null)
                    _fichaSempre = TipoPorNome("Deadheim.InventoryUI.InventoryUiApi")?.GetProperty("Sempre");
                return _fichaSempre != null && (bool)_fichaSempre.GetValue(null, null);
            }
            catch (Exception) { return false; }
        }

        public static string AbrirPets()
        {
            if (!TemMontarias) return "O menu de pets vem do mod Montarias, que nao esta instalado.";
            return Tentar("Pets", () => Invocar("ValheimMontarias.Pets.PetMenu", "Open"));
        }

        /// <summary>Aba de admin das montarias (era o F8 fixo no codigo do Montarias).</summary>
        public static string AbrirMontariasAdmin()
        {
            if (!TemMontarias) return "O mod Montarias nao esta instalado.";
            return Tentar("Montarias (admin)", () =>
            {
                MethodInfo abrir = AccessTools.Method(Tipo("ValheimMontarias.MountMenu"), "OpenAdmin");
                Exigir(abrir, "MountMenu.OpenAdmin");
                abrir.Invoke(null, new object[abrir.GetParameters().Length]);
            });
        }

        /// <summary>"Montaria: Aprendiz", lido da MontariasApi (publica). Vazio se nao der.</summary>
        private static MethodInfo _mkKnown, _mkRank, _mkName;
        private static bool _mkResolvido;

        public static string ResumoMontarias()
        {
            if (!TemMontarias) return "";
            try
            {
                // Os metodos sao resolvidos uma vez: a Central chama isto duas vezes por segundo.
                if (!_mkResolvido)
                {
                    Type api = TipoPorNome("ValheimMontarias.MontariasApi");
                    if (api == null) return "";
                    _mkKnown = AccessTools.Method(api, "LocalKnown");
                    _mkRank = AccessTools.Method(api, "LocalRank");
                    _mkName = AccessTools.Method(api, "RankName");
                    _mkResolvido = true;
                }
                if (!(_mkKnown?.Invoke(null, null) is bool sabe) || !sabe) return "";
                if (!(_mkRank?.Invoke(null, null) is int rank)) return "";
                string nome = _mkName?.Invoke(null, new object[] { rank }) as string;
                return string.IsNullOrEmpty(nome) ? "" : "Habilidade: " + nome;
            }
            catch (Exception)
            {
                return "";
            }
        }

        // ----------------------------------------------------------------- AdaptiveNet

        public static bool PainelRedeVisivel
        {
            get
            {
                if (!TemAdaptiveNet) return false;
                FieldInfo campo = AccessTools.Field(TipoPorNome("AdaptiveNet.DiagnosticsOverlay"), "_visible");
                return campo?.GetValue(null) is bool v && v;
            }
        }

        /// <summary>Liga/desliga o painel de diagnostico de rede (era o F8 do AdaptiveNet).</summary>
        public static string AlternarPainelRede()
        {
            if (!TemAdaptiveNet) return "O mod AdaptiveNet nao esta instalado.";
            return Tentar("Painel de rede", () =>
            {
                FieldInfo campo = AccessTools.Field(Tipo("AdaptiveNet.DiagnosticsOverlay"), "_visible");
                Exigir(campo, "DiagnosticsOverlay._visible");
                campo.SetValue(null, !(campo.GetValue(null) is bool v && v));
            });
        }

        /// <summary>Pede ao servidor para guardar o historico de rede de agora (era o F9 do AdaptiveNet).</summary>
        public static string MarcarIncidenteRede()
        {
            if (!TemAdaptiveNet) return "O mod AdaptiveNet nao esta instalado.";
            return Tentar("Marcar incidente", () =>
            {
                MethodInfo marcar = AccessTools.Method(Tipo("AdaptiveNet.NetworkRuntime"), "RequestManualIncidentMarker");
                Exigir(marcar, "NetworkRuntime.RequestManualIncidentMarker");
                marcar.Invoke(null, new object[] { Time.unscaledTimeAsDouble });
            });
        }

        // ---------------------------------------------------------------- reflexao

        private static string Tentar(string oQue, Action acao)
        {
            if (Player.m_localPlayer == null) return "Entre no mundo primeiro.";
            try
            {
                acao();
                return null;
            }
            catch (Exception ex)
            {
                Exception real = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                Debug.LogWarning($"[Deadheim] Menus: nao consegui abrir '{oQue}': {real}");
                return $"Nao consegui abrir {oQue}. Atualize os mods pelo launcher.";
            }
        }

        private static readonly Dictionary<string, Type> _tipos = new Dictionary<string, Type>();

        /// <summary>
        /// O tipo pelo nome completo, guardado. Nao usa AccessTools.TypeByName: ele varre os tipos de
        /// todos os assemblies, e no cliente sem YamlDotNet (opcional no launcher) a varredura do
        /// NpcValheim falha em NpcConfigStore/QuestStore e o HarmonyX escreve um aviso enorme no log a
        /// cada chamada (a Central chamava a cada meio segundo). Assembly.GetType nao varre.
        /// </summary>
        internal static Type TipoPorNome(string nome)
        {
            if (_tipos.TryGetValue(nome, out Type tipo)) return tipo;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { tipo = assembly.GetType(nome, false); }
                catch (Exception) { tipo = null; }
                if (tipo != null) break;
            }
            _tipos[nome] = tipo;
            return tipo;
        }

        private static Type Tipo(string nome)
        {
            Type tipo = TipoPorNome(nome);
            if (tipo == null) throw new MissingMemberException(nome);
            return tipo;
        }

        private static object Criar(string nome) => Activator.CreateInstance(Tipo(nome), true);

        private static void Invocar(string tipo, string metodo)
        {
            MethodInfo m = AccessTools.Method(Tipo(tipo), metodo);
            Exigir(m, tipo + "." + metodo);
            m.Invoke(null, new object[m.GetParameters().Length]);
        }

        private static void InvocarOpcional(string tipo, string metodo)
        {
            Type t = TipoPorNome(tipo);
            MethodInfo m = t != null ? AccessTools.Method(t, metodo) : null;
            m?.Invoke(null, new object[m.GetParameters().Length]);
        }

        private static void Exigir(object algo, string nome)
        {
            if (algo == null) throw new MissingMemberException(nome);
        }
    }
}
