using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim.Troca
{
    /// <summary>
    /// Janela da troca (IMGUI, como a PlantasUi): pedido que chegou, e a troca em si com a sua
    /// oferta, a do outro, o seu inventario e os passos Travar -> Confirmar.
    /// </summary>
    internal class TrocaUi : MonoBehaviour
    {
        private const float PedidoValeSeg = 35f;

        private static TrocaUi _instance;
        internal static bool IsOpen => _instance != null && TrocaRede.Atual != null;

        private Rect _rect = new Rect(120f, 90f, 640f, 520f);
        private Rect _rectPedido = new Rect(0f, 120f, 340f, 110f);
        private Vector2 _scrollInv;

        internal static void Ensure()
        {
            if (_instance != null) return;
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
            var go = new GameObject("DeadheimTrocaUi");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TrocaUi>();
        }

        private void Update()
        {
            if (Player.m_localPlayer == null) return;
            if (TrocaRede.PedidoDe != null && Time.time - TrocaRede.PedidoEm > PedidoValeSeg) TrocaRede.PedidoDe = null;
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) TrocaRede.Cancelar();
        }

        private void OnGUI()
        {
            if (Player.m_localPlayer == null) return;

            if (TrocaRede.PedidoDe != null && TrocaRede.Atual == null)
            {
                _rectPedido.x = Screen.width / 2f - _rectPedido.width / 2f;
                _rectPedido = GUILayout.Window(0xD26, _rectPedido, DrawPedido, "Pedido de troca");
                Bloquear(_rectPedido);
            }

            if (!IsOpen) return;
            _rect = GUILayout.Window(0xD27, _rect, DrawTroca, "Troca com " + TrocaRede.Atual.Outro);
            Bloquear(_rect);
        }

        // Janela por cima do jogo: o clique nela nao pode virar ataque/construcao.
        private static void Bloquear(Rect rect)
        {
            Event e = Event.current;
            if (e != null && e.isMouse && rect.Contains(e.mousePosition)) e.Use();
        }

        private void DrawPedido(int id)
        {
            GUILayout.Label(TrocaRede.PedidoDe + " quer trocar itens com voce.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Aceitar")) TrocaRede.Aceitar();
            if (GUILayout.Button("Recusar")) TrocaRede.Recusar();
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawTroca(int id)
        {
            TrocaRede.Vista v = TrocaRede.Atual;
            if (v == null) return;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label($"Sua oferta ({v.Minha.Count}/{TrocaMotor.MaxItens})  {Estado(v.MinhaTrava, v.MinhaConf)}");
            foreach (TrocaItem item in v.Minha.ToArray())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Rotulo(item), GUILayout.Width(220f));
                // Mexer na oferta com as travas ligadas zera tudo: de proposito, e o servidor avisa.
                if (GUILayout.Button("Retirar")) TrocaRede.Retirar(item.Id);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label($"Oferta de {v.Outro}  {Estado(v.OutroTrava, v.OutroConf)}");
            foreach (TrocaItem item in v.Dele) GUILayout.Label(Rotulo(item));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Seu inventario (clique para oferecer):");
            _scrollInv = GUILayout.BeginScrollView(_scrollInv, GUILayout.Height(190f));
            Inventory inv = Player.m_localPlayer.GetInventory();
            bool cheio = v.Minha.Count >= TrocaMotor.MaxItens;
            foreach (ItemDrop.ItemData item in inv.GetAllItems().ToArray())
            {
                bool pendente = TrocaRede.Pendente(item);
                GUI.enabled = !cheio && !pendente;
                string nome = Localization.instance.Localize(item.m_shared.m_name);
                string extra = (item.m_stack > 1 ? " x" + item.m_stack : "") + (item.m_quality > 1 ? "  (nv " + item.m_quality + ")" : "")
                    + (item.m_equipped ? "  [equipado]" : "");
                if (GUILayout.Button(nome + extra, GUILayout.ExpandWidth(true))) TrocaRede.Oferecer(item);
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            bool algo = v.Minha.Count + v.Dele.Count > 0;
            bool guardando = v.Minha.Concat(v.Dele).Any(i => !i.Seguro);
            GUILayout.BeginHorizontal();
            GUI.enabled = algo && !guardando && !v.MinhaTrava;
            if (GUILayout.Button("1. Travar oferta")) TrocaRede.Travar();
            GUI.enabled = v.MinhaTrava && v.OutroTrava && !v.MinhaConf && !guardando;
            if (GUILayout.Button("2. Confirmar troca")) TrocaRede.Confirmar();
            GUI.enabled = true;
            if (GUILayout.Button("Cancelar")) TrocaRede.Cancelar();
            GUILayout.EndHorizontal();

            GUILayout.Label(guardando ? "Guardando os itens no servidor..." : Dica(v));
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private static string Estado(bool trava, bool conf) => conf ? "<color=#7CFC00>[confirmou]</color>" : trava ? "<color=#ffd700>[travou]</color>" : "";

        private static string Dica(TrocaRede.Vista v)
        {
            if (v.MinhaConf) return "Voce confirmou. Aguardando " + v.Outro + "...";
            if (v.MinhaTrava && !v.OutroTrava) return "Aguardando " + v.Outro + " travar a oferta...";
            if (v.MinhaTrava && v.OutroTrava) return "Confira a oferta dele e confirme. Qualquer mudanca zera tudo.";
            return "Monte a oferta e trave. Se a oferta mudar, as travas voltam a zero.";
        }

        private static string Rotulo(TrocaItem item)
        {
            string[] partes = (item.Nome ?? "").Split('|');
            string nome = partes[0];
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(nome) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop?.m_itemData?.m_shared != null) nome = Localization.instance.Localize(drop.m_itemData.m_shared.m_name);
            int q = partes.Length > 1 && int.TryParse(partes[1], out int qq) ? qq : 1;
            return nome + (item.Qtd > 1 ? " x" + item.Qtd : "") + (q > 1 ? "  (nv " + q + ")" : "");
        }

        // Com a janela aberta o personagem nao anda nem ataca, e o cursor fica livre.
        [HarmonyPatch(typeof(Player), nameof(Player.TakeInput))]
        private static class TakeInputPatch
        {
            private static void Postfix(Player __instance, ref bool __result)
            {
                if (IsOpen && __instance == Player.m_localPlayer) __result = false;
            }
        }

        [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
        private static class MouseCapturePatch
        {
            private static bool Prefix()
            {
                if (!IsOpen) return true;
                ZCursor.LockState = CursorLockMode.None;
                ZCursor.Show();
                return false;
            }
        }
    }
}
