using HarmonyLib;
using UnityEngine;

namespace Deadheim
{
    /// <summary>Janela das plantas salvas (IMGUI) e aviso de progresso. Abre com /planta.</summary>
    public class PlantasUi : MonoBehaviour
    {
        private static PlantasUi _instance;
        internal static bool IsOpen => _instance != null && _instance._open;

        private bool _open;
        private Rect _rect = new Rect(80f, 80f, 380f, 440f);
        private string _nameField = "";
        private float _radius = -1f;
        private string _confirmDelete;
        private Vector2 _scroll;

        internal static void Ensure()
        {
            if (_instance != null) return;
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
            var go = new GameObject("DeadheimPlantasUi");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PlantasUi>();
        }

        internal static void Open()
        {
            Ensure();
            if (_instance == null) return;
            if (!Plantas.Enabled.Value) { Plantas.Status = "Plantas desabilitadas no servidor."; }
            _instance._open = true;
            _instance._confirmDelete = null;
            if (_instance._radius < 0f) _instance._radius = Plantas.CaptureRadius.Value;
            Plantas.RequestList();
        }

        internal static void Close()
        {
            if (_instance != null) _instance._open = false;
        }

        private void Update()
        {
            if (Player.m_localPlayer == null) { _open = false; return; }
            if (_open && Input.GetKeyDown(KeyCode.Escape)) _open = false;
            Plantas.Tick();
        }

        private void OnGUI()
        {
            if (Player.m_localPlayer == null) return;

            Plantas.Run run = Plantas.Current;
            if (run != null || Plantas.Starting) DrawProgress(run);
            if (!_open) return;

            _rect = GUILayout.Window(0xD24, _rect, DrawWindow, "Plantas salvas (Aesir)");
            // Janela por cima do jogo: o clique nela nao pode virar ataque/construcao.
            Event e = Event.current;
            if (e != null && e.isMouse && _rect.Contains(e.mousePosition)) e.Use();
        }

        private void DrawProgress(Plantas.Run run)
        {
            string text = run == null
                ? "Planta: aguardando o servidor..."
                : "Planta '" + run.Name + "': " + run.Idx + "/" + run.Pecas.Count
                  + (string.IsNullOrEmpty(run.Waiting) ? "" : "\n" + run.Waiting) + "\n(/planta cancelar)";
            var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            GUI.Box(new Rect(Screen.width / 2f - 190f, 60f, 380f, 64f), text, style);
        }

        private void DrawWindow(int id)
        {
            bool busy = Plantas.Current != null || Plantas.Starting;
            bool vip = VipList.VipListApi.IsLocalPlayerVip();

            if (!vip)
                GUILayout.Label("Plantas salvas sao exclusivas dos Aesir (VIP). Voce ainda pode ver e apagar as suas.");

            GUILayout.Label("Plantas guardadas (" + Plantas.Saved.Count + "/" + Plantas.MaxSaved.Value + "):");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(170f));
            if (!Plantas.ListLoaded) GUILayout.Label("Carregando...");
            else if (Plantas.Saved.Count == 0) GUILayout.Label("Nenhuma planta salva.");
            foreach (Plantas.Entry en in Plantas.Saved.ToArray())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(en.Name + " (" + en.Count + " pecas)", GUILayout.Width(190f));
                GUI.enabled = vip && !busy;
                if (GUILayout.Button("Colocar"))
                {
                    _open = false;
                    Plantas.StartPlace(en.Name);
                }
                GUI.enabled = true;
                bool confirming = _confirmDelete == en.Name;
                if (GUILayout.Button(confirming ? "Confirmar?" : "Apagar"))
                {
                    if (confirming) { Plantas.RequestDelete(en.Name); _confirmDelete = null; }
                    else _confirmDelete = en.Name;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            GUILayout.Label("Salvar as pecas que voce construiu perto de voce:");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Nome", GUILayout.Width(50f));
            _nameField = GUILayout.TextField(_nameField ?? "", 24);
            GUILayout.EndHorizontal();

            float max = Mathf.Min(Plantas.HardMaxRadius, Mathf.Max(3f, Plantas.CaptureRadius.Value * 2f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Raio: " + Mathf.Round(_radius) + " m", GUILayout.Width(90f));
            _radius = GUILayout.HorizontalSlider(_radius, 3f, max);
            GUILayout.EndHorizontal();

            GUI.enabled = vip && !busy;
            if (GUILayout.Button("Salvar planta aqui"))
                Plantas.Capture(_nameField, Mathf.Round(_radius));
            GUI.enabled = true;

            GUILayout.Label("Colocar: olhe para o chao onde a planta deve ficar. Cada peca gasta material e entra "
                + "numa fila de 2 por segundo; ande ate a obra se ela for grande. Nao vale em combate, zona de raid nem ward alheio.",
                GUILayout.Height(80f));

            if (!string.IsNullOrEmpty(Plantas.Status)) GUILayout.Label(Plantas.Status);

            GUILayout.BeginHorizontal();
            if (busy && GUILayout.Button("Cancelar colocacao")) Plantas.CancelPlace();
            if (GUILayout.Button("Fechar")) _open = false;
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
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
