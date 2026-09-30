// Passo "ajustes" do roteiro solo: a opcao Deadheim do menu do ESC, no jogo de verdade.
//
// Confere o que so da para ver com o Valheim rodando: o botao entrou no menu do ESC sem
// sobrepor os outros, a janela abre dentro da tela, os controles da janela gravam, e a aba
// Servidor fala com o servidor dedicado. Tira fotos de cada tela em <Root>\fotos.
//
// O cliente de teste so e admin quando o run-pvp-test.ps1 roda com -Admin (ele escreve a
// adminlist.txt do servidor). Sem -Admin, o passo confere o lado do jogador comum: sem aba
// Servidor e o servidor negando o pedido forjado.
//
//   run-pvp-test.ps1 -Solo -Steps ajustes           jogador comum
//   run-pvp-test.ps1 -Solo -Steps ajustes -Admin    admin
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Deadheim;
using Deadheim.Ajustes;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PvpTestDriver
{
    public partial class Driver
    {
        private string RootDir => Path.GetDirectoryName(_sync.TrimEnd('\\', '/'));

        private IEnumerator SoloAjustes()
        {
            bool admin = Deadheim.Vanilla.Admin.LocalPlayerIsAdmin();
            Log($"ajustes: admin={admin} tela={Screen.width}x{Screen.height}");
            Log($"ajustes: adminlist no cliente=[{string.Join(", ", ZNet.instance.GetAdminList() ?? new List<string>())}] eu={Splatform.PlatformManager.DistributionPlatform.LocalUser.PlatformUserID}");

            var update = AccessTools.Method(typeof(Menu), "Update");
            Check("ajustes/esc-volta-ao-menu (patch no Menu.Update)",
                update != null && Harmony.GetPatchInfo(update)?.Prefixes.Any(p => p.owner == "Detalhes.deadheim") == true);

            // ------------------------------------------------------------ menu do ESC
            if (!ChamarMenu("Show"))
            {
                Check("ajustes/menu-abre", false, "Menu.Show nao existe nesta versao do jogo");
                yield break;
            }
            yield return WaitFor(Menu.IsVisible, 5f);
            // O botao nasce no primeiro Update do Deadheim com o menu na tela.
            yield return Wait(1f);
            Check("ajustes/menu-abre", Menu.IsVisible());

            Button botao = BotaoDeadheim();
            Log("menu do ESC: " + DescreverMenu());
            Check("ajustes/botao-no-menu", botao != null && botao.gameObject.activeInHierarchy);
            if (botao == null)
            {
                yield return Foto("1-menu-esc-sem-botao");
                ChamarMenu("Hide");
                yield break;
            }
            Check("ajustes/botao-texto", TextoDe(botao) == AjustesMenu.TextoBotao, TextoDe(botao));
            Check("ajustes/botao-sem-sobrepor", SemSobrepor(botao, out string sobrepoe), sobrepoe);
            Check("ajustes/botao-dentro-da-tela", DentroDaTela((RectTransform)botao.transform, out string foraBotao), foraBotao);
            yield return Foto("1-menu-esc");

            // ---------------------------------------------------------------- janela
            botao.onClick.Invoke();
            yield return Wait(0.5f);
            GameObject janela = Campo<GameObject>("_janela");
            Check("ajustes/janela-abre", AjustesJanela.Aberta);
            Check("ajustes/menu-some-atras", AlfaDoMenu() == 0f, $"alfa={AlfaDoMenu()}");
            string foraJanela = "sem janela";
            Check("ajustes/janela-dentro-da-tela", janela != null && DentroDaTela((RectTransform)janela.transform, out foraJanela), foraJanela);
            Check("ajustes/aba-servidor-so-admin", Campo<GameObject>("_botaoServidor").activeSelf == admin);
            yield return Foto("2-meus-ajustes");

            // ----------------------------------------------------------- Meus ajustes
            var locais = AjustesDados.Mods(local: true);
            Log("meus ajustes: " + string.Join(", ", locais.Select(m => $"{m.Nome}({m.Itens})")));
            Check("ajustes/meus-tem-mods", locais.Count > 0);
            string vazou = string.Join(", ", locais.SelectMany(m => AjustesDados.Itens(m.Guid, local: true).Where(i => i.Sincronizado).Select(i => m.Nome + ":" + i.Chave)));
            Check("ajustes/meus-sem-opcao-do-servidor", vazou.Length == 0, vazou);
            Check("ajustes/meus-sem-config-de-servidor-do-deadheim", AjustesDados.Itens(Plugin.PluginGUID, local: true).All(i => i.Chave != "WardRadius"));
            // RaidSystem 2.1.1: o webhook e so do servidor e fica fora dos ajustes do jogador.
            Check("ajustes/meus-sem-webhook-do-raidsystem", AjustesDados.Itens("Detalhes.RaidSystem", local: true).All(i => i.Chave != "Discord Webhook URL"));

            yield return EditarLocal();

            // ---------------------------------------------------------------- Servidor
            if (admin) yield return ServidorComoAdmin();
            else yield return ServidorComoJogador();

            // ---------------------------------------------------------------- fechar
            AjustesMenu.FecharJanela();
            yield return Wait(0.3f);
            Check("ajustes/voltar-fecha-a-janela", !AjustesJanela.Aberta && Menu.IsVisible());
            Check("ajustes/voltar-devolve-o-menu", AlfaDoMenu() == 1f, $"alfa={AlfaDoMenu()}");

            // Menu fechando por fora (ESC, logout) leva a janela junto e devolve o menu.
            botao.onClick.Invoke();
            yield return Wait(0.3f);
            ChamarMenu("Hide");
            yield return Wait(0.5f);
            Check("ajustes/menu-fechado-fecha-a-janela", !AjustesJanela.Aberta && !Menu.IsVisible());
            Check("ajustes/menu-fechado-devolve-o-menu", AlfaDoMenu() == 1f, $"alfa={AlfaDoMenu()}");
        }

        /// <summary>Tecla do menu do RaidSystem (opcao local, texto): muda pelo campo da janela e volta.</summary>
        private IEnumerator EditarLocal()
        {
            const string raid = "Detalhes.RaidSystem";
            ConfigEntryBase tecla = Entrada(raid, "9 - Client", "Menu Key");
            if (tecla == null)
            {
                Log("SKIP ajustes/meus-edita: RaidSystem sem 9 - Client/Menu Key neste teste");
                yield break;
            }

            Selecionar(raid);
            yield return Wait(0.3f);
            object linha = Linha("Menu Key");
            InputField campo = linha == null ? null : Traverse.Create(linha).Field("Campo").GetValue<InputField>();
            Check("ajustes/meus-linha-da-tecla", campo != null);
            if (campo == null) yield break;

            string original = TomlTypeConverter.ConvertToString(tecla.BoxedValue, tecla.SettingType);
            string novo = original == "PageDown" ? "Home" : "PageDown";
            campo.onEndEdit.Invoke(novo);
            yield return Wait(0.3f);
            string atual = TomlTypeConverter.ConvertToString(tecla.BoxedValue, tecla.SettingType);
            Check("ajustes/meus-edita", atual == novo, $"{original} -> {atual}");
            Check("ajustes/meus-salva-no-cfg", File.ReadAllText(tecla.ConfigFile.ConfigFilePath).Contains("Menu Key = " + novo));
            Check("ajustes/meus-status", Status().StartsWith("Salvo"), Status());
            yield return Foto("3-meus-ajustes-editado");

            campo.onEndEdit.Invoke("isso nao e tecla");
            yield return Wait(0.3f);
            Check("ajustes/meus-recusa-invalido", TomlTypeConverter.ConvertToString(tecla.BoxedValue, tecla.SettingType) == novo && campo.text == novo, Status());

            campo.onEndEdit.Invoke(original);
            yield return Wait(0.3f);
            Check("ajustes/meus-desfaz", TomlTypeConverter.ConvertToString(tecla.BoxedValue, tecla.SettingType) == original);
        }

        private IEnumerator ServidorComoJogador()
        {
            MarkServerLog();
            int antes = Plugin.CartographyTableAmount.Value;
            string cfg = ServerCfg();

            // Pedido forjado, sem a janela: o servidor tem que negar sozinho.
            AjustesRede.PedirMods();
            AjustesRede.Definir(Plugin.PluginGUID, "Server config", "CartographyTableAmount", (antes + 7).ToString());
            yield return WaitFor(() => Status().Contains("So admins"), 5f);
            Check("ajustes/jogador-negado", Status().Contains("So admins"), Status());
            yield return Wait(2f);
            Check("ajustes/jogador-nao-muda-o-servidor", Plugin.CartographyTableAmount.Value == antes && ServerCfg() == cfg,
                $"valor={Plugin.CartographyTableAmount.Value}");
            string log = ServerLogSinceMark();
            Check("ajustes/servidor-registra-a-tentativa", log.Contains("sem ser admin"), Tail(log));
        }

        private IEnumerator ServidorComoAdmin()
        {
            MarkServerLog();
            Campo<GameObject>("_botaoServidor").GetComponent<Button>().onClick.Invoke();
            yield return WaitFor(() => Linha("CartographyTableAmount") != null || Status().Length > 0 && !Status().StartsWith("Enviando"), 10f);

            var mods = Campo<System.Collections.Generic.List<ModAjustes>>("_mods");
            Log("servidor: " + string.Join(", ", mods.Select(m => $"{m.Nome}({m.Itens})")));
            Check("ajustes/servidor-lista-mods", mods.Any(m => m.Guid == Plugin.PluginGUID), Status());

            // A aba abre no Deadheim (primeiro da lista); garante e espera as opcoes chegarem.
            Selecionar(Plugin.PluginGUID);
            yield return WaitFor(() => Linha("CartographyTableAmount") != null, 10f);
            Check("ajustes/servidor-opcoes-do-deadheim", Linha("CartographyTableAmount") != null, Status());
            yield return Foto("4-servidor");
            if (Linha("CartographyTableAmount") == null) yield break;

            // Numero, pelo campo da janela.
            int original = Plugin.CartographyTableAmount.Value;
            int novo = original + 23;
            InputField campo = Traverse.Create(Linha("CartographyTableAmount")).Field("Campo").GetValue<InputField>();
            campo.onEndEdit.Invoke(novo.ToString());
            yield return WaitFor(() => Plugin.CartographyTableAmount.Value == novo, 10f);
            Check("ajustes/admin-muda-e-o-servidor-sincroniza", Plugin.CartographyTableAmount.Value == novo, $"{original} -> {Plugin.CartographyTableAmount.Value}");
            Check("ajustes/admin-status", Status().Contains("ja valendo"), Status());
            Check("ajustes/admin-salva-no-cfg-do-servidor", ServerCfg().Contains("CartographyTableAmount = " + novo));
            string log = ServerLogSinceMark();
            Check("ajustes/servidor-registra-quem-mudou", log.Contains($"mudou {Plugin.PluginGUID} [Server config] CartographyTableAmount: '{original}' -> '{novo}'"), Tail(log));
            yield return Foto("5-servidor-editado");

            // Valor invalido: o servidor recusa e a janela volta ao valor real.
            campo.onEndEdit.Invoke("abc");
            yield return WaitFor(() => Status().Contains("recusou"), 10f);
            Check("ajustes/admin-invalido-recusado", Status().Contains("recusou") && campo.text == novo.ToString() && Plugin.CartographyTableAmount.Value == novo, Status());

            // Liga/desliga, pela caixa de marcar.
            object linhaKill = Linha("KillFeed");
            Toggle caixa = linhaKill == null ? null : Traverse.Create(linhaKill).Field("Caixa").GetValue<Toggle>();
            Check("ajustes/admin-caixa-killfeed", caixa != null);
            if (caixa != null)
            {
                bool killAntes = Deadheim.Pvp.PvpConfig.KillFeed.Value;
                caixa.isOn = !killAntes;
                yield return WaitFor(() => Deadheim.Pvp.PvpConfig.KillFeed.Value != killAntes, 10f);
                Check("ajustes/admin-caixa-muda", Deadheim.Pvp.PvpConfig.KillFeed.Value != killAntes);
                caixa.isOn = killAntes;
                yield return WaitFor(() => Deadheim.Pvp.PvpConfig.KillFeed.Value == killAntes, 10f);
                Check("ajustes/admin-caixa-desfaz", Deadheim.Pvp.PvpConfig.KillFeed.Value == killAntes);
            }

            // Botao Padrao, e depois de volta ao valor do comeco.
            Button padrao = Traverse.Create(Linha("CartographyTableAmount")).Field("Padrao").GetValue<Button>();
            int valorPadrao = (int)Plugin.CartographyTableAmount.DefaultValue;
            padrao.onClick.Invoke();
            yield return WaitFor(() => Plugin.CartographyTableAmount.Value == valorPadrao, 10f);
            Check("ajustes/admin-botao-padrao", Plugin.CartographyTableAmount.Value == valorPadrao, Plugin.CartographyTableAmount.Value.ToString());
            if (original != valorPadrao)
            {
                campo.onEndEdit.Invoke(original.ToString());
                yield return WaitFor(() => Plugin.CartographyTableAmount.Value == original, 10f);
            }
            Check("ajustes/admin-desfaz", Plugin.CartographyTableAmount.Value == original);
        }

        // ---------------------------------------------------------------- apoio

        private static bool ChamarMenu(string metodo)
        {
            var m = AccessTools.Method(typeof(Menu), metodo, Type.EmptyTypes);
            if (m == null || Menu.instance == null) return false;
            m.Invoke(Menu.instance, null);
            return true;
        }

        private static Transform DialogoDoMenu()
        {
            object valor = AccessTools.Field(typeof(Menu), "m_menuDialog")?.GetValue(Menu.instance);
            return valor is Component c ? c.transform : (valor as GameObject)?.transform;
        }

        private static Button BotaoDeadheim()
        {
            Transform dialogo = DialogoDoMenu();
            return dialogo == null ? null : dialogo.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Deadheim");
        }

        private static float AlfaDoMenu()
        {
            Transform dialogo = DialogoDoMenu();
            CanvasGroup grupo = dialogo != null ? dialogo.GetComponent<CanvasGroup>() : null;
            return grupo != null ? grupo.alpha : 1f;
        }

        private static string TextoDe(Button botao)
        {
            if (botao == null) return "";
            TMP_Text tmp = botao.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) return tmp.text;
            Text legado = botao.GetComponentInChildren<Text>(true);
            return legado != null ? legado.text : "";
        }

        private static Rect NaTela(RectTransform rect)
        {
            Vector3[] cantos = new Vector3[4];
            rect.GetWorldCorners(cantos);
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(camera, cantos[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(camera, cantos[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static bool DentroDaTela(RectTransform rect, out string detalhe)
        {
            Rect r = NaTela(rect);
            detalhe = $"x={r.xMin:0}..{r.xMax:0} y={r.yMin:0}..{r.yMax:0} tela={Screen.width}x{Screen.height}";
            return r.xMin >= -2f && r.yMin >= -2f && r.xMax <= Screen.width + 2f && r.yMax <= Screen.height + 2f;
        }

        private static bool SemSobrepor(Button botao, out string detalhe)
        {
            Rect meu = NaTela((RectTransform)botao.transform);
            var outros = DialogoDoMenu().GetComponentsInChildren<Button>(false)
                .Where(b => b != botao)
                .Where(b => { Rect r = NaTela((RectTransform)b.transform); return r.xMin < meu.xMax - 1f && r.xMax > meu.xMin + 1f && r.yMin < meu.yMax - 1f && r.yMax > meu.yMin + 1f; })
                .Select(b => b.name)
                .ToList();
            detalhe = outros.Count == 0 ? "" : "sobrepoe: " + string.Join(", ", outros);
            return outros.Count == 0;
        }

        private static string DescreverMenu()
        {
            Transform dialogo = DialogoDoMenu();
            if (dialogo == null) return "(sem m_menuDialog)";
            StringBuilder texto = new StringBuilder($"dialogo={dialogo.name} ");
            foreach (Button b in dialogo.GetComponentsInChildren<Button>(false))
            {
                Rect r = NaTela((RectTransform)b.transform);
                texto.Append($"[{b.name} '{TextoDe(b)}' y={r.yMin:0}..{r.yMax:0}] ");
            }
            return texto.ToString();
        }

        private static T Campo<T>(string nome) => (T)AccessTools.Field(typeof(AjustesJanela), nome).GetValue(null);

        private static string Status()
        {
            Text status = Campo<Text>("_status");
            return status != null ? status.text ?? "" : "";
        }

        private static void Selecionar(string guid)
            => AccessTools.Method(typeof(AjustesJanela), "SelecionarMod").Invoke(null, new object[] { guid });

        /// <summary>A linha da janela com esta chave (Linha e privada; vem por reflexao).</summary>
        private static object Linha(string chave)
        {
            var linhas = (IDictionary)AccessTools.Field(typeof(AjustesJanela), "_linhas").GetValue(null);
            foreach (DictionaryEntry e in linhas)
                if (Traverse.Create(e.Value).Field("Item").GetValue<ItemAjuste>()?.Chave == chave) return e.Value;
            return null;
        }

        private static ConfigEntryBase Entrada(string guid, string secao, string chave)
        {
            if (!Chainloader.PluginInfos.TryGetValue(guid, out var info) || info?.Instance?.Config == null) return null;
            return info.Instance.Config.Select(kv => kv.Value).FirstOrDefault(e => e.Definition.Section == secao && e.Definition.Key == chave);
        }

        private string ServerCfg()
        {
            try { return File.ReadAllText(Path.Combine(RootDir, "server", "BepInEx", "config", "Detalhes.Deadheim.cfg")); }
            catch (Exception ex) { return "(ilegivel: " + ex.Message + ")"; }
        }

        private IEnumerator Foto(string nome)
        {
            string pasta = Path.Combine(RootDir, "fotos");
            Directory.CreateDirectory(pasta);
            string arquivo = Path.Combine(pasta, $"ajustes-{nome}.png");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(arquivo);
            yield return Wait(0.5f);
            Log("foto: " + arquivo);
        }
    }

    // Diagnostico: a lista de admins que o servidor manda ao cliente (ZNet.RPC_AdminList).
    [HarmonyPatch(typeof(ZNet), "RPC_AdminList")]
    internal static class DiagAdminList
    {
        private static void Postfix(ZNet __instance)
            => BepInEx.Logging.Logger.CreateLogSource("PvpTestDriver").LogInfo($"[PVPTEST] RPC_AdminList recebido: [{string.Join(", ", __instance.GetAdminList())}]");
    }
}
