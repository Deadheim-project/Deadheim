// Passo "bancada" do roteiro solo: busca na lista de receitas, quantidade com - e + e o tempo de
// fabricacao, numa Bancada de verdade, pelo painel que o jogador usa.
//
//   run-pvp-test.ps1 -Solo -Steps bancada
using Deadheim.Craft;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

namespace PvpTestDriver
{
    public partial class Driver
    {
        private IEnumerator SoloBancada()
        {
            GameObject prefab = ZNetScene.instance.GetPrefab("piece_workbench");
            CraftingStation modelo = prefab ? prefab.GetComponentInChildren<CraftingStation>(true) : null;
            // Uma receita da bancada que empilha (o lote cabe num slot) e sem melhoria.
            Recipe receita = ObjectDB.instance.m_recipes.FirstOrDefault(r => r && r.m_enabled && r.m_item
                && r.m_craftingStation && modelo && r.m_craftingStation.m_name == modelo.m_name
                && r.m_item.m_itemData.m_shared.m_maxStackSize >= 20 && r.m_resources.Length > 0);
            Check("bancada/receita", receita != null, receita?.name ?? "(nenhuma)");
            if (receita == null) yield break;
            string nome = Localization.instance.Localize(receita.m_item.m_itemData.m_shared.m_name);
            Log($"bancada: receita={receita.name} item='{nome}' x{receita.m_amount}");

            yield return MoveTo(_openA);
            Me.UnequipAllItems();
            Inventory inv = Me.GetInventory();
            inv.RemoveAll();
            GameObject go = Instantiate(prefab, Ground(Me.transform.position + Me.transform.forward * 1f), Quaternion.identity);
            CraftingStation bancada = go.GetComponentInChildren<CraftingStation>();
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            Me.AddKnownRecipe(receita);
            const int lote = 3;
            foreach (Piece.Requirement req in receita.m_resources)
                Give(req.m_resItem.name, req.m_amount * lote, 1);
            yield return Wait(1f);

            // ------------------------------------------------------------------ painel
            Me.SetCraftingStation(bancada);
            InventoryGui gui = InventoryGui.instance;
            gui.Show(null);
            yield return Wait(1f);
            Check("bancada/busca-montada", CraftPatches.Campo != null && CraftPatches.Campo.gameObject.activeInHierarchy);
            Check("bancada/quantidade-montada", CraftPatches.Controles != null);
            DumpUi(gui.m_crafting, 0);
            yield return FotoBancada("1-painel");

            // ------------------------------------------------------------------- busca
            int todas = gui.m_availableRecipes.Count;
            string pedaco = nome.Substring(0, Mathf.Min(4, nome.Length)).ToUpperInvariant();
            CraftPatches.Campo.text = pedaco;
            yield return Wait(0.5f);
            bool soCombina = gui.m_availableRecipes.All(r => CraftPatches.Combina(r.Recipe));
            Check("bancada/busca-filtra", soCombina && gui.m_availableRecipes.Any(r => r.Recipe == receita) && gui.m_availableRecipes.Count <= todas,
                $"'{pedaco}': {gui.m_availableRecipes.Count} de {todas}");
            yield return FotoBancada("2-busca");

            CraftPatches.Campo.text = "zzzqqq";
            yield return Wait(0.3f);
            Check("bancada/busca-sem-resultado", gui.m_availableRecipes.Count == 0, gui.m_availableRecipes.Count.ToString());
            CraftPatches.Campo.text = nome;
            yield return Wait(0.3f);
            int indice = gui.m_availableRecipes.FindIndex(r => r.Recipe == receita);
            gui.SetRecipe(indice, true);
            yield return Wait(0.3f);

            // ------------------------------------------------- digitando nao anda nem fecha
            CraftPatches.Campo.ActivateInputField();
            yield return null;
            yield return null;
            PlayerController controle = Me.GetComponent<PlayerController>();
            Check("bancada/digitando-nao-anda", CraftPatches.Digitando && controle != null && !controle.TakeInput(),
                $"digitando={CraftPatches.Digitando}");
            CraftPatches.Campo.DeactivateInputField();
            yield return null;

            // -------------------------------------------------------------- quantidade
            Check("bancada/quantidade-visivel", CraftPatches.Controles.activeInHierarchy && CraftPatches.Quantidade == 1);
            CraftPatches.Menos.onClick.Invoke();
            Check("bancada/quantidade-minimo-1", CraftPatches.Quantidade == 1, CraftPatches.Quantidade.ToString());
            CraftPatches.Mais.onClick.Invoke();
            CraftPatches.Mais.onClick.Invoke();
            yield return Wait(0.3f);
            string botao = gui.m_craftButton.GetComponentInChildren<TMP_Text>()?.text ?? "";
            Check("bancada/quantidade-3", CraftPatches.Quantidade == lote && gui.m_multiCraftAmount == lote && botao.EndsWith("x " + lote),
                $"quantidade={CraftPatches.Quantidade} lote={gui.m_multiCraftAmount} botao='{botao}' ativo={gui.m_craftButton.interactable}");
            Check("bancada/da-para-3", gui.m_craftButton.interactable);
            yield return FotoBancada("3-quantidade");

            CraftPatches.Mais.onClick.Invoke();
            yield return Wait(0.3f);
            Check("bancada/sem-material-para-4", !gui.m_craftButton.interactable, $"quantidade={CraftPatches.Quantidade}");
            CraftPatches.Menos.onClick.Invoke();
            yield return Wait(0.3f);

            // ---------------------------------------------------------------- fabricar
            Check("bancada/tempo-0.25", Mathf.Approximately(gui.m_craftDuration, 0.25f) && Mathf.Approximately(gui.m_multiCraftDuration, 0.25f),
                $"{gui.m_craftDuration}/{gui.m_multiCraftDuration}");
            string item = receita.m_item.m_itemData.m_shared.m_name;
            int antes = inv.CountItems(item);
            float inicio = Time.time;
            gui.OnCraftPressed();
            yield return WaitFor(() => inv.CountItems(item) > antes, 5f);
            float levou = Time.time - inicio;
            int feitos = inv.CountItems(item) - antes;
            bool gastou = receita.m_resources.All(r => inv.CountItems(r.m_resItem.m_itemData.m_shared.m_name) == 0);
            // O bonus da skill pode dar a mais; nunca a menos.
            Check("bancada/fabricou-3", feitos >= receita.m_amount * lote && gastou, $"feitos={feitos} gastou={gastou}");
            Check("bancada/rapido", levou < 1.5f, $"{levou:0.00}s");
            yield return FotoBancada("4-depois");

            // ------------------------------------------------------------ ao fechar
            gui.Hide();
            yield return Wait(0.5f);
            Check("bancada/busca-limpa-ao-fechar", CraftPatches.Busca == "" && CraftPatches.Campo.text == "");

            ZNetScene.instance.Destroy(go);
            inv.RemoveAll();
            yield return Wait(1f);
        }

        private static void DumpUi(Transform t, int nivel)
        {
            if (nivel > 3) return;
            RectTransform r = t as RectTransform;
            Log($"ui: {new string(' ', nivel * 2)}{t.name} ativo={t.gameObject.activeSelf}"
                + (r ? $" min={r.anchorMin} max={r.anchorMax} offMin={r.offsetMin} offMax={r.offsetMax} tam={r.rect.size}" : ""));
            foreach (Transform filho in t) DumpUi(filho, nivel + 1);
        }

        private IEnumerator FotoBancada(string nome)
        {
            string pasta = Path.Combine(RootDir, "fotos");
            Directory.CreateDirectory(pasta);
            string arquivo = Path.Combine(pasta, $"bancada-{nome}.png");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(arquivo);
            yield return Wait(0.5f);
            Log("foto: " + arquivo);
        }
    }
}
