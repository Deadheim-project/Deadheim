// Passo "forja" do roteiro solo: Forja de Potencial (UpgradeStation do Valheim 1.0) com a
// Garantia de Refino, no jogo de verdade.
//
// A estacao fica num local do mundo; o teste cria uma ao lado do jogador. Uma tentativa passa
// pelo caminho do jogador (painel aberto, receita escolhida, botao, contagem do tempo); as
// outras chamam o DoCrafting direto, o mesmo que o botao chama no fim da contagem.
//
//   run-pvp-test.ps1 -Solo -Steps forja
using Deadheim;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

namespace PvpTestDriver
{
    /// <summary>Durante o passo forja: o raio do efeito da forja nao fere o jogador de teste.</summary>
    [HarmonyLib.HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class RaioDaForja
    {
        internal static bool Ignorar;

        [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.First)]
        private static bool Prefix(Character __instance, HitData hit)
            => !(Ignorar && __instance == Player.m_localPlayer && hit != null && hit.m_damage.m_lightning > 0f);
    }

    public partial class Driver
    {
        private const string ForjaArma = "SwordIron";

        private CraftingStation _forja;

        private IEnumerator SoloForja()
        {
            // ----------------------------------------------------------- item e estacao
            GameObject garantia = ObjectDB.instance.GetItemPrefab(Forja.GarantiaPrefab);
            ItemDrop.ItemData.SharedData shared = garantia?.GetComponent<ItemDrop>()?.m_itemData.m_shared;
            Check("forja/item-garantia-existe", shared != null && shared.m_name == Forja.GarantiaNome, shared?.m_name ?? "(sem prefab)");
            Check("forja/garantia-nao-vende-no-mercador", shared != null && shared.m_value == 0, $"valor={shared?.m_value}");
            Sprite pedra = ObjectDB.instance.GetItemPrefab("Thunderstone")?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_icons.FirstOrDefault();
            Check("forja/garantia-com-icone-proprio", shared?.m_icons?.FirstOrDefault() != null && shared.m_icons[0] != pedra);

            GameObject estacao = ZNetScene.instance.GetPrefab("UpgradeStation");
            Check("forja/estacao-do-jogo", estacao != null && estacao.GetComponentInChildren<CraftingStation>(true)?.m_upgrader == true);
            Recipe receita = ObjectDB.instance.GetRecipe(ObjectDB.instance.GetItemPrefab(ForjaArma).GetComponent<ItemDrop>().m_itemData);
            Piece.Requirement idoloReq = receita?.m_resources.FirstOrDefault(r => r.m_upgraderResource);
            Check("forja/receita-pede-idolo", idoloReq != null, receita == null ? "sem receita" : idoloReq?.m_resItem.name ?? "sem idolo");
            if (estacao == null || idoloReq == null) yield break;
            string idolo = idoloReq.m_resItem.m_itemData.m_shared.m_name;
            float chanceDoJogo = idoloReq.m_resItem.m_itemData.m_shared.m_upgradeChance;
            Log($"forja: idolo={idoloReq.m_resItem.name} chance={chanceDoJogo} quebra={idoloReq.m_resItem.m_itemData.m_shared.m_breakChance}");
            Check("forja/config-padrao", Forja.GarantiaAtiva.Value && Mathf.Approximately(Forja.ChanceComGarantia.Value, 1f)
                && Forja.NivelMaximo.Value == 25 && Forja.GarantiasPara(25) == 1,
                $"ativa={Forja.GarantiaAtiva.Value} chance={Forja.ChanceComGarantia.Value} teto={Forja.NivelMaximo.Value} custo25={Forja.GarantiasPara(25)}");

            yield return MoveTo(_openA);
            MoveDummy(_openA + Vector3.right * 30f);
            // O efeito da forja (fx_UpgradeStation_Success/Fail) da raio em quem refina: 10 no
            // sucesso, 60 na falha. Varias tentativas seguidas matam o personagem novo de 25 de vida,
            // e o modo deus faz o AzuAntiCheat fechar o jogo: o RaioDaForja ignora esse dano.
            RaioDaForja.Ignorar = true;
            // O painel so lista receita conhecida; quem tem a espada de ferro ja a conhece.
            Me.AddKnownRecipe(receita);
            Me.UnequipAllItems();
            Inventory inv = Me.GetInventory();
            inv.RemoveAll();
            GameObject go = Instantiate(estacao, Ground(Me.transform.position + Me.transform.forward * 1f), Quaternion.identity);
            _forja = go.GetComponentInChildren<CraftingStation>();
            // O jogo fecha o painel se o jogador sair de m_useDistance (1,7 m) do componente, e a
            // estrutura e grande: o componente fica a 1 m do jogador e os colisores nao empurram.
            go.transform.position += Me.transform.position + Me.transform.forward * 1f - _forja.transform.position;
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            yield return Wait(1f);
            Log($"forja: estacao '{_forja.m_name}' a {Vector3.Distance(Me.transform.position, _forja.transform.position):0.00} m (uso {_forja.m_useDistance})");

            ItemDrop.ItemData arma = Give(ForjaArma, 1, 4);
            Give(idoloReq.m_resItem.name, 20, 1);
            Give(Forja.GarantiaPrefab, 10, 1);
            Check("forja/estoque-inicial", Garantias() == 10 && Idolos(idolo) == 20 && arma != null && arma.m_quality == 4,
                $"garantias={Garantias()} idolos={Idolos(idolo)} arma={arma?.m_quality}");

            // ------------------------------------------- pelo painel, como o jogador faz
            Me.SetCraftingStation(_forja);
            InventoryGui.instance.Show(null);
            yield return Wait(1f);
            bool selecionou = SelecionarNoPainel(arma);
            yield return Wait(0.5f);
            InventoryGui gui = InventoryGui.instance;
            string aviso = gui.m_itemCraftType.text;
            string botao = gui.m_craftButton.GetComponentInChildren<TMP_Text>()?.text ?? "";
            Check("forja/painel-na-forja", Me.GetCurrentCraftingStation() == _forja && selecionou, $"estacao={Me.GetCurrentCraftingStation()?.m_name} selecionou={selecionou}");
            Check("forja/painel-mostra-garantia", aviso.Contains(Forja.GarantiaNome) && aviso.Contains("100%") && aviso.Contains("usa 1, tem 10"), aviso);
            Check("forja/botao-refinar-com-garantia", botao == "Refinar com garantia" && gui.m_craftButton.interactable, $"'{botao}' ativo={gui.m_craftButton.interactable}");
            yield return FotoForja("1-painel-com-garantia");

            gui.OnCraftPressed();
            // A forja demora m_upgraderDuration + nivel * m_upgraderDurationPerLevel (8 + 5 s).
            yield return WaitFor(() => Garantias() == 9, 30f);
            arma = Arma();
            Check("forja/botao-sobe-com-garantia", arma != null && arma.m_quality == 5 && Garantias() == 9 && Idolos(idolo) == 19,
                $"arma={arma?.m_quality} garantias={Garantias()} idolos={Idolos(idolo)}");
            Check("forja/chance-do-idolo-volta", Mathf.Approximately(idoloReq.m_resItem.m_itemData.m_shared.m_upgradeChance, chanceDoJogo),
                idoloReq.m_resItem.m_itemData.m_shared.m_upgradeChance.ToString());
            yield return FotoForja("2-depois-de-refinar");

            // ---------------------------------------------- 100%: oito seguidas sem quebrar
            int subiu = 0;
            for (int i = 0; i < 8; i++)
            {
                int antes = Arma()?.m_quality ?? 0;
                Refinar(receita, Arma());
                if (Arma()?.m_quality == antes + 1) subiu++;
                yield return null;
            }
            arma = Arma();
            Check("forja/garantia-sobe-sempre", subiu == 8 && arma?.m_quality == 13, $"subiu={subiu}/8 nivel={arma?.m_quality}");
            Check("forja/gasta-uma-por-nivel", Garantias() == 1 && Idolos(idolo) == 11, $"garantias={Garantias()} idolos={Idolos(idolo)}");

            // ------------------------------------------- custo por nivel (cfg do servidor)
            MarkServerLog();
            SetServerConfig("GarantiasPorNivel", "1:1,14:2");
            yield return WaitFor(() => Forja.GarantiasPara(14) == 2, 20f);
            Check("forja/custo-por-nivel-chega-ao-vivo", Forja.GarantiasPara(13) == 1 && Forja.GarantiasPara(14) == 2,
                $"13={Forja.GarantiasPara(13)} 14={Forja.GarantiasPara(14)}");

            // Tem 1, o nivel 14 pede 2: barra, nao arrisca o item.
            Refinar(receita, Arma());
            yield return null;
            Check("forja/faltam-garantias-barra", Arma()?.m_quality == 13 && Garantias() == 1 && Idolos(idolo) == 11,
                $"arma={Arma()?.m_quality} garantias={Garantias()} idolos={Idolos(idolo)}");

            Give(Forja.GarantiaPrefab, 3, 1);
            Refinar(receita, Arma());
            yield return null;
            Check("forja/gasta-o-custo-do-nivel", Arma()?.m_quality == 14 && Garantias() == 2 && Idolos(idolo) == 10,
                $"arma={Arma()?.m_quality} garantias={Garantias()} idolos={Idolos(idolo)}");
            SetServerConfig("GarantiasPorNivel", "1:1");
            yield return WaitFor(() => Forja.GarantiasPara(14) == 1, 20f);

            // ----------------------------------------------------- chance configuravel
            SetServerConfig("ChanceComGarantia", "0");
            yield return WaitFor(() => Forja.ChanceComGarantia.Value == 0f, 20f);
            Refinar(receita, Arma());
            yield return null;
            Check("forja/chance-0-quebra-e-gasta", Arma() == null && Garantias() == 1 && Idolos(idolo) == 9,
                $"arma={Arma()?.m_quality} garantias={Garantias()} idolos={Idolos(idolo)}");
            SetServerConfig("ChanceComGarantia", "1");
            yield return WaitFor(() => Forja.ChanceComGarantia.Value == 1f, 20f);

            // ------------------------------------- desligada: forja do jogo, nao gasta garantia
            arma = Give(ForjaArma, 1, 4);
            Forja.UsarGarantia.Value = false;
            Refinar(receita, arma);
            yield return null;
            Check("forja/jogador-desliga-nao-gasta", Garantias() == 1 && Idolos(idolo) == 8 && !inv.ContainsItem(arma),
                $"garantias={Garantias()} idolos={Idolos(idolo)} resultado={(Arma() == null ? "quebrou" : "nivel " + Arma().m_quality)}");
            Forja.UsarGarantia.Value = true;
            foreach (ItemDrop.ItemData sobra in inv.GetAllItems().Where(i => i.m_dropPrefab?.name == ForjaArma).ToList()) inv.RemoveItem(sobra);

            SetServerConfig("GarantiaAtiva", "false");
            yield return WaitFor(() => !Forja.GarantiaAtiva.Value, 20f);
            arma = Give(ForjaArma, 1, 4);
            Refinar(receita, arma);
            yield return null;
            Check("forja/servidor-desliga-nao-gasta", Garantias() == 1 && Idolos(idolo) == 7 && !inv.ContainsItem(arma),
                $"garantias={Garantias()} idolos={Idolos(idolo)}");
            SetServerConfig("GarantiaAtiva", "true");
            yield return WaitFor(() => Forja.GarantiaAtiva.Value, 20f);
            foreach (ItemDrop.ItemData sobra in inv.GetAllItems().Where(i => i.m_dropPrefab?.name == ForjaArma).ToList()) inv.RemoveItem(sobra);

            // ------------------------------------------------------------------- teto
            arma = Give(ForjaArma, 1, 25);
            Refinar(receita, arma);
            yield return null;
            Check("forja/teto-25-barra", inv.ContainsItem(arma) && arma.m_quality == 25 && Garantias() == 1 && Idolos(idolo) == 7,
                $"arma={arma.m_quality} garantias={Garantias()} idolos={Idolos(idolo)}");
            Me.SetCraftingStation(_forja);
            InventoryGui.instance.Show(null);
            yield return Wait(1f);
            SelecionarNoPainel(arma);
            yield return Wait(0.5f);
            Check("forja/teto-no-painel", gui.m_itemCraftType.text.Contains("Nível máximo") && !gui.m_craftButton.interactable,
                $"'{gui.m_itemCraftType.text}' ativo={gui.m_craftButton.interactable}");
            yield return FotoForja("3-teto");

            Check("forja/servidor-recarregou", ServerLogSinceMark().Contains("Config recarregada de Detalhes.Deadheim.cfg"));
            InventoryGui.instance.Hide();
            ZNetScene.instance.Destroy(go);
            inv.RemoveAll();
            yield return Wait(1f);
            RaioDaForja.Ignorar = false;
        }

        private ItemDrop.ItemData Give(string prefab, int amount, int quality)
            => Me.GetInventory().AddItem(prefab, amount, quality, 0, Me.GetPlayerID(), Me.GetPlayerName(), false);

        private ItemDrop.ItemData Arma()
            => Me.GetInventory().GetAllItems().FirstOrDefault(i => i.m_dropPrefab != null && i.m_dropPrefab.name == ForjaArma);

        private int Garantias() => Me.GetInventory().CountItems(Forja.GarantiaNome, -1, false);

        private int Idolos(string nome) => Me.GetInventory().CountItems(nome, -1, false);

        /// <summary>O fim do botao Refinar: o DoCrafting com a receita e o item escolhidos, na forja.</summary>
        private void Refinar(Recipe receita, ItemDrop.ItemData item)
        {
            InventoryGui gui = InventoryGui.instance;
            Me.SetCraftingStation(_forja);
            gui.m_craftRecipe = receita;
            gui.m_craftUpgradeItem = item;
            gui.m_craftVariant = 0;
            gui.m_multiCrafting = false;
            gui.DoCrafting(Me);
        }

        private bool SelecionarNoPainel(ItemDrop.ItemData item)
        {
            InventoryGui gui = InventoryGui.instance;
            gui.UpdateCraftingPanel(true);
            for (int i = 0; i < gui.m_availableRecipes.Count; i++)
            {
                if (gui.m_availableRecipes[i].ItemData != item) continue;
                gui.SetRecipe(i, true);
                return true;
            }
            Log("forja: receitas no painel = " + string.Join(", ", gui.m_availableRecipes.Select(r => $"{r.Recipe?.name}:{r.ItemData?.m_quality}")));
            return false;
        }

        private IEnumerator FotoForja(string nome)
        {
            string pasta = Path.Combine(RootDir, "fotos");
            Directory.CreateDirectory(pasta);
            string arquivo = Path.Combine(pasta, $"forja-{nome}.png");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(arquivo);
            yield return Wait(0.5f);
            Log("foto: " + arquivo);
        }
    }
}
