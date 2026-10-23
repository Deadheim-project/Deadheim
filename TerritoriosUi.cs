using Deadheim.Vanilla;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Janela do admin para os territorios (IMGUI, como a TrocaUi): lista, criacao no ponto onde
    /// o admin esta, e edicao de raio, regras e jogadores permitidos. Abre com /territorio.
    /// </summary>
    internal class TerritoriosUi : MonoBehaviour
    {
        private static TerritoriosUi _instance;
        internal static bool IsOpen => _instance != null && _instance._aberta;

        private bool _aberta;
        private Rect _rect = new Rect(120f, 90f, 640f, 470f);
        private Vector2 _scrollLista, _scrollPermitidos;

        private string _selecionado;
        private string _novoNome = "";
        private string _novoRaio = "50";
        private string _raio = "";
        private bool _construir, _estruturas, _seguro, _interacao;
        private List<string> _permitidos = new List<string>();
        private string _novoPermitido = "";

        internal static void Abrir()
        {
            if (!Admin.LocalPlayerIsAdmin()) return;
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
            if (_instance == null)
            {
                GameObject go = new GameObject("DeadheimTerritoriosUi");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<TerritoriosUi>();
            }
            _instance._aberta = true;
        }

        private void Update()
        {
            if (!_aberta) return;
            if (Player.m_localPlayer == null || Input.GetKeyDown(KeyCode.Escape)) _aberta = false;
        }

        private void OnGUI()
        {
            if (!_aberta || Player.m_localPlayer == null) return;
            _rect = GUILayout.Window(0xD28, _rect, Desenhar, "Territorios");
            Event e = Event.current;
            if (e != null && e.isMouse && _rect.Contains(e.mousePosition)) e.Use();
        }

        private Territorios.Territorio Atual
            => Territorios.Todos.FirstOrDefault(t => t.Nome == _selecionado);

        private void Selecionar(Territorios.Territorio t)
        {
            _selecionado = t.Nome;
            _raio = t.Raio.ToString("0", CultureInfo.InvariantCulture);
            _construir = t.Construir; _estruturas = t.Estruturas; _seguro = t.Seguro; _interacao = t.Interacao;
            _permitidos = new List<string>(t.Permitidos);
        }

        private void Desenhar(int id)
        {
            GUILayout.BeginHorizontal();

            // ---- coluna da esquerda: lista e criacao
            GUILayout.BeginVertical(GUILayout.Width(230f));
            GUILayout.Label($"Territorios ({Territorios.Todos.Count})");
            _scrollLista = GUILayout.BeginScrollView(_scrollLista, GUILayout.Height(300f));
            foreach (Territorios.Territorio t in Territorios.Todos.ToArray())
                if (GUILayout.Toggle(t.Nome == _selecionado, $"{t.Nome}  ({t.Raio:0} m)", GUI.skin.button) && t.Nome != _selecionado)
                    Selecionar(t);
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.Label("Novo territorio (onde voce esta):");
            GUILayout.BeginHorizontal();
            _novoNome = GUILayout.TextField(_novoNome, 32, GUILayout.Width(120f)).Replace(" ", "_");
            _novoRaio = GUILayout.TextField(_novoRaio, 5, GUILayout.Width(45f));
            GUILayout.Label("m");
            GUILayout.EndHorizontal();
            GUI.enabled = _novoNome.Length > 0 && float.TryParse(_novoRaio, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            if (GUILayout.Button("Criar aqui"))
            {
                float raio = float.Parse(_novoRaio, CultureInfo.InvariantCulture);
                Territorios.PedirCriar(_novoNome, Player.m_localPlayer.transform.position, raio);
                _selecionado = _novoNome; // seleciona quando a lista nova chegar
                _novoNome = "";
            }
            GUI.enabled = true;
            GUILayout.EndVertical();

            // ---- coluna da direita: edicao
            GUILayout.BeginVertical();
            Territorios.Territorio atual = Atual;
            if (atual == null)
            {
                GUILayout.Label("Escolha um territorio na lista ou crie um novo.");
            }
            else
            {
                // Territorio recem-criado: a selecao veio antes dos dados.
                if (_raio.Length == 0) Selecionar(atual);

                GUILayout.Label($"<b>{atual.Nome}</b>   centro ({atual.X:F0}, {atual.Z:F0})");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Raio (m):", GUILayout.Width(70f));
                _raio = GUILayout.TextField(_raio, 5, GUILayout.Width(60f));
                GUILayout.EndHorizontal();

                GUILayout.Space(4f);
                _construir = GUILayout.Toggle(_construir, "Bloquear construcao e desmonte");
                _estruturas = GUILayout.Toggle(_estruturas, "Estruturas invulneraveis");
                _seguro = GUILayout.Toggle(_seguro, "Zona segura de PvP");
                _interacao = GUILayout.Toggle(_interacao, "Bloquear stands, itens no chao e baus");

                GUILayout.Space(4f);
                GUILayout.Label("Jogadores permitidos (constroem e mexem mesmo com o bloqueio):");
                _scrollPermitidos = GUILayout.BeginScrollView(_scrollPermitidos, GUILayout.Height(120f));
                foreach (string nome in _permitidos.ToArray())
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(nome, GUILayout.Width(220f));
                    if (GUILayout.Button("Tirar")) _permitidos.Remove(nome);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                GUILayout.BeginHorizontal();
                _novoPermitido = GUILayout.TextField(_novoPermitido, 32, GUILayout.Width(180f));
                GUI.enabled = _novoPermitido.Trim().Length > 0 && !_novoPermitido.Contains(",");
                if (GUILayout.Button("Permitir"))
                {
                    string jogador = _novoPermitido.Trim();
                    if (!_permitidos.Any(n => string.Equals(n, jogador, System.StringComparison.OrdinalIgnoreCase)))
                        _permitidos.Add(jogador);
                    _novoPermitido = "";
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                bool raioOk = float.TryParse(_raio, NumberStyles.Float, CultureInfo.InvariantCulture, out float raio);
                GUI.enabled = raioOk;
                if (GUILayout.Button("Aplicar"))
                    Territorios.PedirEditar(atual.Nome, raio, _construir, _estruturas, _seguro, _interacao, _permitidos);
                GUI.enabled = true;
                if (GUILayout.Button("Desfazer")) Selecionar(atual);
                if (GUILayout.Button("Remover"))
                {
                    Territorios.PedirRemover(atual.Nome);
                    _selecionado = null;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            if (GUILayout.Button("Fechar")) _aberta = false;
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
