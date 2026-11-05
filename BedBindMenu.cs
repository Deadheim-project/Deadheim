using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Fase 3: menu da cama. A tecla de definir ponto (Retreat.TeclaDoPonto) olhando para a propria cama
    /// abre esta janelinha (IMGUI) com Hearthstone / Retreat 1 / Retreat 2: mostra se cada ponto esta
    /// definido e grava o ponto atual do jogador ao clicar, confirmando na tela. Fecha com ESC ou ao se
    /// afastar da cama.
    /// </summary>
    internal class BedBindMenu : MonoBehaviour
    {
        private static BedBindMenu _instance;
        internal static bool IsOpen => _instance != null && _instance._aberta;

        private bool _aberta;
        private Vector3 _camaPos;
        private string _ultimo = "";
        private Rect _rect = new Rect(0f, 0f, 380f, 190f);

        internal static void Abrir(Bed cama)
        {
            if (cama == null) return;
            if (_instance == null)
            {
                GameObject go = new GameObject("DeadheimBedBindMenu");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<BedBindMenu>();
            }
            _instance._aberta = true;
            _instance._camaPos = cama.transform.position;
            _instance._ultimo = "";
            _instance._rect.x = (Screen.width - _instance._rect.width) / 2f;
            _instance._rect.y = Screen.height * 0.3f;
        }

        private void Update()
        {
            if (!_aberta) return;
            Player p = Player.m_localPlayer;
            if (p == null || Input.GetKeyDown(KeyCode.Escape) || Vector3.Distance(p.transform.position, _camaPos) > 8f)
                _aberta = false;
        }

        private void OnGUI()
        {
            if (!_aberta || Player.m_localPlayer == null) return;
            _rect = GUILayout.Window(0xD31, _rect, Desenhar, "Definir ponto nesta cama");
            Event e = Event.current;
            if (e != null && e.isMouse && _rect.Contains(e.mousePosition)) e.Use();
        }

        private void Desenhar(int id)
        {
            bool vip = VipList.VipListApi.IsLocalPlayerVip();
            bool dois = AesirConfig.Aesir18DoisPontos != null && AesirConfig.Aesir18DoisPontos.Value;
            Linha(0, "Hearthstone", true, "");
            Linha(1, "Retreat 1", vip, "so Aesir");
            Linha(2, "Retreat 2", vip && dois, vip ? "desligado neste servidor" : "so Aesir");
            GUILayout.Space(4f);
            if (_ultimo.Length > 0) GUILayout.Label("<color=#7CFC00>" + _ultimo + "</color>");
            if (GUILayout.Button("Fechar")) _aberta = false;
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void Linha(int ponto, string nome, bool disponivel, string motivo)
        {
            Vector3 atual = Retreat.GetHearthStonePosition(ponto);
            string estado = !disponivel ? motivo
                : atual == Vector3.zero ? "não definido"
                : $"definido ({atual.x:0}, {atual.z:0})";
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{nome}</b>: {estado}", GUILayout.Width(250f));
            GUI.enabled = disponivel;
            if (GUILayout.Button(atual == Vector3.zero ? "Definir aqui" : "Redefinir"))
            {
                Retreat.SetHearthStonePosition(ponto);
                _ultimo = nome + " definido nesta cama";
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, _ultimo, 0, null);
                if (ponto != 0) AesirLog.D(18, "gravou o ponto " + ponto + " de retreat (menu da cama).");
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }
}
