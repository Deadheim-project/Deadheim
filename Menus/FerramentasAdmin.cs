using System;
using System.Collections.Generic;

namespace Deadheim.Menus
{
    /// <summary>
    /// Tudo o que e de admin, num lugar so (7.9.0): a primeira entrada da aba Admin do
    /// ESC -> Deadheim (AjustesJanela). Antes cada coisa tinha a sua tecla ou comando: F8 do
    /// NpcValheim (painel), F8 do Montarias (aba de admin), F8/F9 do AdaptiveNet, /territorio,
    /// /totem_destino. As teclas agora vem vazias na config de cada mod (quem quiser define uma);
    /// os comandos continuam, para quem prefere.
    ///
    /// O servidor confere a adminlist em cada pedido de verdade (painel, territorios, cfg): estes
    /// botoes so aparecem para admin porque a aba so aparece para admin.
    /// </summary>
    internal static class FerramentasAdmin
    {
        internal sealed class Ferramenta
        {
            public string Titulo;
            public string Descricao;
            public string Botao = "Abrir";
            /// <summary>null = sempre disponivel.</summary>
            public Func<bool> Disponivel;
            /// <summary>Por que nao esta disponivel (mostrado no lugar do botao).</summary>
            public string Ausente;
            /// <summary>Fecha o menu do ESC antes (janelas que abrem por cima do jogo).</summary>
            public bool FechaMenu = true;
            /// <summary>Devolve a mensagem para o admin, ou null.</summary>
            public Func<string> Acao;
        }

        public static List<Ferramenta> Todas() => new List<Ferramenta>
        {
            new Ferramenta
            {
                Titulo = "Painel de Admin",
                Descricao = "Mensagem global em faixa no meio da tela, ir até ou puxar um jogador, buff e cura em área, bestiário e arenas.",
                Disponivel = () => Integracoes.TemNpcValheim,
                Ausente = "Precisa do NpcValheim",
                Acao = Integracoes.AbrirPainelAdmin
            },
            new Ferramenta
            {
                Titulo = "Montarias (admin)",
                Descricao = "Perfis das montarias, ranks de montaria e concessões a jogadores.",
                Disponivel = () => Integracoes.TemMontarias,
                Ausente = "Precisa do Montarias",
                Acao = Integracoes.AbrirMontariasAdmin
            },
            new Ferramenta
            {
                Titulo = "Territórios",
                Descricao = "Lista os territórios, cria um no ponto onde você está e edita raio, regras e jogadores permitidos.",
                Acao = () => { TerritoriosUi.Abrir(); return null; }
            },
            new Ferramenta
            {
                Titulo = "Chegada do Totem da Cidade",
                Descricao = "Grava a sua posição atual como o ponto de chegada do Totem da Cidade (o servidor salva na cfg).",
                Botao = "Gravar aqui",
                FechaMenu = false,
                Acao = TotemCidade.GravarDestino
            },
            new Ferramenta
            {
                Titulo = "Painel de rede",
                Descricao = "Liga ou desliga o diagnóstico de rede na tela (AdaptiveNet).",
                Botao = "Ligar/desligar",
                Disponivel = () => Integracoes.TemAdaptiveNet,
                Ausente = "Precisa do AdaptiveNet",
                Acao = Integracoes.AlternarPainelRede
            },
            new Ferramenta
            {
                Titulo = "Marcar incidente de rede",
                Descricao = "Pede ao servidor para guardar o histórico de rede de agora. Use logo depois de um travamento.",
                Botao = "Marcar",
                FechaMenu = false,
                Disponivel = () => Integracoes.TemAdaptiveNet,
                Ausente = "Precisa do AdaptiveNet",
                Acao = () => Integracoes.MarcarIncidenteRede() ?? "Pedido enviado: o servidor guarda o histórico de rede de agora."
            },
        };

        /// <summary>Os comandos de admin que nao tem janela (chat com Enter, ou console F5).</summary>
        public const string Comandos =
            "<color=#ffa13c>/pvpadmin zona</color>  onde você está: zona, bioma, castelo e marcas de PvP\n" +
            "<color=#ffa13c>/pvpadmin imune <min></color> | <color=#ffa13c>limpar</color>  muda a própria imunidade (teste)\n" +
            "<color=#ffa13c>/pvpadmin pk <min> [jogador]</color>  marca PK (-1 permanente, 0 limpa)\n" +
            "<color=#ffa13c>/pvpadmin bounty <jogador> <moedas></color>  bounty paga pela casa\n" +
            "<color=#ffa13c>/pvpadmin pve <jogador></color>  tira do PvE na hora\n" +
            "<color=#ffa13c>/territorio</color>  a mesma janela de Territórios, ou os subcomandos\n" +
            "<color=#ffa13c>/totem_destino</color>  o mesmo botão da chegada do totem\n" +
            "<color=#ffa13c>/batizar remover <nome></color>  apaga um lugar batizado do mapa";
    }
}
