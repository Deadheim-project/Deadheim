using BepInEx.Configuration;
using System;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Teclas das acoes Aesir que antes so saiam por comando digitado (decisao do usuario, 02/11/2026:
    /// o jogador nao precisa escrever comando). As do bau valem com o bau na mira ou aberto, como o
    /// "[E] Abrir" do jogo, e aparecem no texto do bau (AesirDicas); as outras valem de qualquer lugar
    /// e a cama lembra as do retreat. Os comandos continuam funcionando.
    ///
    /// Cada jogador tem as suas (nao sincronizadas): troca em ESC > Deadheim > Meus ajustes ou no cfg.
    /// So as do bau vem com tecla (interacao com o objeto na mira, como o E do jogo), com Alt para nao
    /// cair em tecla do jogo (E abre o bau, Shift+E empilha). As de qualquer lugar vem vazias, seguindo
    /// os "menus sem tecla" do 7.9.0: a Central Deadheim (Tab) tem esses botoes.
    /// </summary>
    internal static class AesirTeclas
    {
        public static ConfigEntry<KeyboardShortcut> Depositar;
        public static ConfigEntry<KeyboardShortcut> Ordenar;
        public static ConfigEntry<KeyboardShortcut> Etiqueta;
        public static ConfigEntry<KeyboardShortcut> Retreat;
        public static ConfigEntry<KeyboardShortcut> Retreat2;
        public static ConfigEntry<KeyboardShortcut> Plantas;
        public static ConfigEntry<KeyboardShortcut> Transmog;

        public static void Bind(ConfigFile config)
        {
            const string secao = "Aesir - Teclas";
            // As do bau tem tecla, como o E de abrir: sao usadas olhando para o bau, e o texto dele
            // mostra a tecla. Nao estao na Central (02/11/2026, decisao do usuario). As de qualquer
            // lugar (abaixo) vem vazias, como os menus desde o 7.9.0.
            Depositar = config.Bind(secao, "Depositar", new KeyboardShortcut(KeyCode.G, KeyCode.LeftAlt),
                "Aesir 14: com um bau na mira (ou aberto), guarda nos baus por perto o que eles ja tem. Vazio = sem tecla (/depositar).");
            Ordenar = config.Bind(secao, "Ordenar", new KeyboardShortcut(KeyCode.O, KeyCode.LeftAlt),
                "Aesir 15: com o bau aberto, ordena os itens por nome. Vazio = sem tecla (/ordenar).");
            Etiqueta = config.Bind(secao, "Etiqueta", new KeyboardShortcut(KeyCode.N, KeyCode.LeftAlt),
                "Aesir 16: com um bau na mira, abre a caixa para dar nome a ele. Vazio = sem tecla (/etiqueta <nome>).");
            Retreat = config.Bind(secao, "Retreat", KeyboardShortcut.Empty,
                "Volta ao ponto de retreat gravado na cama (o mesmo que /retreat e o botao da Central). Vazio = sem tecla.");
            Retreat2 = config.Bind(secao, "Retreat2", KeyboardShortcut.Empty,
                "Aesir 18: volta ao segundo ponto de retreat (o mesmo que /retreat 2). Vazio = sem tecla.");
            Plantas = config.Bind(secao, "Plantas", KeyboardShortcut.Empty,
                "Aesir 24: abre e fecha a janela de plantas salvas (o mesmo que /planta). Vazio = sem tecla.");
            Transmog = config.Bind(secao, "Transmog", KeyboardShortcut.Empty,
                "Aesir 35: abre a janela do transmog (o mesmo que /transmog). Vazio = sem tecla.");
        }

        // ------------------------------------------------------------------ leitura das teclas

        private static bool Shift() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        private static bool Ctrl() => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        private static bool Alt() => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);

        private enum Grupo { Nenhum, Shift, Ctrl, Alt }

        private static Grupo GrupoDe(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.LeftShift: case KeyCode.RightShift: return Grupo.Shift;
                case KeyCode.LeftControl: case KeyCode.RightControl: return Grupo.Ctrl;
                case KeyCode.LeftAlt: case KeyCode.RightAlt: case KeyCode.AltGr: return Grupo.Alt;
                default: return Grupo.Nenhum;
            }
        }

        /// <summary>
        /// A tecla principal desceu agora com exatamente os modificadores do atalho (esquerdo ou direito
        /// tanto faz). Diferente do KeyboardShortcut.IsDown do BepInEx, segurar W andando nao atrapalha;
        /// e um modificador a mais muda o atalho (Alt+H nao dispara no Shift+Alt+H).
        /// </summary>
        internal static bool Apertou(ConfigEntry<KeyboardShortcut> entrada)
        {
            if (entrada == null) return false;
            KeyboardShortcut atalho = entrada.Value;
            if (atalho.MainKey == KeyCode.None || !Input.GetKeyDown(atalho.MainKey)) return false;
            bool querShift = false, querCtrl = false, querAlt = false;
            foreach (KeyCode m in atalho.Modifiers)
            {
                switch (GrupoDe(m))
                {
                    case Grupo.Shift: querShift = true; break;
                    case Grupo.Ctrl: querCtrl = true; break;
                    case Grupo.Alt: querAlt = true; break;
                    default: if (!Input.GetKey(m)) return false; break;
                }
            }
            return Shift() == querShift && Ctrl() == querCtrl && Alt() == querAlt;
        }

        private static string NomeDa(KeyCode k)
        {
            switch (GrupoDe(k))
            {
                case Grupo.Shift: return "Shift";
                case Grupo.Ctrl: return "Ctrl";
                case Grupo.Alt: return "Alt";
            }
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((int)(k - KeyCode.Alpha0)).ToString();
            return k.ToString();
        }

        /// <summary>"Shift + Alt + H": modificadores na frente, como o jogo escreve "Shift + E".</summary>
        internal static string Nome(KeyboardShortcut atalho)
        {
            if (atalho.MainKey == KeyCode.None) return "";
            string texto = "";
            foreach (Grupo g in new[] { Grupo.Shift, Grupo.Ctrl, Grupo.Alt })
                foreach (KeyCode m in atalho.Modifiers)
                    if (GrupoDe(m) == g) { texto += NomeDa(m) + " + "; break; }
            foreach (KeyCode m in atalho.Modifiers)
                if (GrupoDe(m) == Grupo.Nenhum) texto += NomeDa(m) + " + ";
            return texto + NomeDa(atalho.MainKey);
        }

        /// <summary>Uma linha de dica no formato do jogo; vazia se a tecla estiver desligada (None).</summary>
        internal static string Dica(ConfigEntry<KeyboardShortcut> entrada, string acao)
        {
            if (entrada == null || entrada.Value.MainKey == KeyCode.None) return "";
            return "\n[<color=yellow><b>" + Nome(entrada.Value) + "</b></color>] " + acao;
        }

        // ------------------------------------------------------------------ acoes

        private static void Tela(string texto) => Player.m_localPlayer?.Message(MessageHud.MessageType.Center, texto);

        /// <summary>Chamado pelo Plugin.Update. As acoes conferem VIP e a chave de cada item por dentro.</summary>
        internal static void Tick()
        {
            try
            {
                AesirBaus.Conferir();

                Player player = Player.m_localPlayer;
                if (player == null || player.IsDead()) return;
                // Digitando ou com menu na frente, a letra e do texto, nao da acao.
                // (Ui.IsTyping cobre chat, console, caixa de texto e campos como a busca da bancada.)
                if (Vanilla.Ui.IsTyping() || Menu.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible()) return;

                // Bau: o aberto ou, com o inventario fechado, o da mira.
                bool inventario = InventoryGui.IsVisible();
                Container aberto = AesirBaus.Aberto();
                Container bau = aberto ?? (inventario ? null : AesirBaus.NaMira());
                if (bau != null)
                {
                    if (Apertou(Depositar)) { AesirBaus.Depositar(Tela); return; }
                    if (Apertou(Ordenar))
                    {
                        if (aberto != null) AesirBaus.Ordenar(Tela, aberto);
                        else Tela("Abra o bau para ordenar.");
                        return;
                    }
                    if (Apertou(Etiqueta)) { AesirBaus.PedirEtiqueta(Tela, bau); return; }
                }

                // As de qualquer lugar nao valem com o inventario aberto.
                if (inventario) return;
                if (Apertou(Retreat)) { Deadheim.Retreat.Usar(1, Tela); return; }
                if (Apertou(Retreat2)) { Deadheim.Retreat.Usar(2, Tela); return; }
                if (Apertou(Plantas))
                {
                    if (PlantasUi.IsOpen) PlantasUi.Close();
                    else PlantasUi.Open();
                    return;
                }
                if (Apertou(Transmog)) Deadheim.Transmog.HandleCommand(new[] { "transmog" });
            }
            catch (Exception e) { AesirLog.Erro(0, e); }
        }
    }
}
