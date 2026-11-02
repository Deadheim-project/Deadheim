using System;

namespace Deadheim.Menus
{
    /// <summary>
    /// O que outros mods entregam ao hub do inventario e a aba Admin, no mesmo jeito do
    /// PvpBridge: quem depende do Deadheim (RaidSystem) preenche os delegates no Awake dele,
    /// e o Deadheim chama sem precisar referenciar aquele assembly. Null = o mod nao esta
    /// instalado (ou e uma versao antiga): o botao aparece desligado, com o motivo.
    /// </summary>
    public static class MenuBridge
    {
        /// <summary>RaidSystem: painel da guilda (membros, territorios, descricao). Era o PageUp.</summary>
        public static Action AbrirGuilda;

        /// <summary>RaidSystem: Ranking de Guerra (jogadores e guildas). Era o PageDown.</summary>
        public static Action AbrirRanking;
    }
}
