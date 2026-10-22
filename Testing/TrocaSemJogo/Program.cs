// Teste sem o jogo do nucleo da troca (Troca/TrocaMotor.cs). A regra de ouro: cada item existe
// em um lugar so (oferta em custodia ou entrega pendente), em qualquer caminho, inclusive
// cancelamento, queda e restart.
using Deadheim.Troca;
using System;
using System.Collections.Generic;
using System.Linq;

internal static class Program
{
    private static int _pass;
    private static int _fail;

    private static void Check(string name, bool ok, string detail = "")
    {
        if (ok) _pass++;
        else _fail++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (ok || detail.Length == 0 ? "" : "  (" + detail + ")"));
    }

    private const long A = 100, B = 200, C = 300;

    private static TrocaItem Item(string id, int qtd = 1, string dados = null)
        => new TrocaItem { Id = id, Nome = "Item " + id, Qtd = qtd, Dados = dados ?? ("dados-" + id) };

    private static TrocaMotor Aberta(out TrocaSessao s)
    {
        var m = new TrocaMotor();
        m.Pedir(A, B, 0);
        m.Aceitar(B, 1);
        s = m.SessaoDe(A);
        m.Efeitos.Clear();
        return m;
    }

    /// <summary>Todos os ids de item que o motor guarda (ofertas + entregas pendentes).</summary>
    private static List<string> TodosIds(TrocaMotor m)
    {
        var ids = new List<string>();
        foreach (var s in new[] { m.SessaoDe(A), m.SessaoDe(B), m.SessaoDe(C) }.Where(x => x != null).Distinct())
        {
            ids.AddRange(s.OfertaA.Select(i => i.Id));
            ids.AddRange(s.OfertaB.Select(i => i.Id));
        }
        foreach (var e in m.Pendentes) ids.AddRange(e.Itens.Select(i => i.Id));
        return ids;
    }

    private static IEnumerable<TrocaEntrega> Entregas(TrocaMotor m, long para)
        => m.Efeitos.Where(e => e.Tipo == TrocaEfeitoTipo.Entrega && e.Para == para).Select(e => e.Entrega);

    private static int Main()
    {
        // ---- pedido ----
        {
            var m = new TrocaMotor();
            Check("pedir a si mesmo recusa", m.Pedir(A, A, 0) == "si_mesmo");
            Check("pedir ok", m.Pedir(A, B, 0) == null);
            Check("efeito de pedido vai ao alvo", m.Efeitos.Count == 1 && m.Efeitos[0].Tipo == TrocaEfeitoTipo.Pedido && m.Efeitos[0].Para == B);
            Check("quem pediu fica ocupado", m.Pedir(A, C, 0) == "voce_ocupado");
            Check("alvo com pedido fica ocupado", m.Pedir(C, B, 0) == "alvo_ocupado");
            Check("so o alvo aceita", m.Aceitar(A, 1) == "sem_pedido");
            Check("oferecer antes de aceitar recusa", m.Oferecer(A, Item("x"), 1) == "sem_sessao");
            Check("aceitar abre a oferta", m.Aceitar(B, 1) == null && m.SessaoDe(A).Fase == TrocaFase.Oferta);

            var r = new TrocaMotor();
            r.Pedir(A, B, 0);
            Check("recusar fecha e libera os dois", r.Recusar(B) == null && r.SessaoDe(A) == null && r.SessaoDe(B) == null);
            Check("liberado pode pedir de novo", r.Pedir(A, C, 0) == null);

            var t = new TrocaMotor();
            t.Pedir(A, B, 0);
            t.Tick(29);
            Check("pedido vive 30s", t.SessaoDe(A) != null);
            t.Tick(30);
            Check("pedido expira em 30s", t.SessaoDe(A) == null && t.SessaoDe(B) == null);
        }

        // ---- oferta ----
        {
            var m = Aberta(out var s);
            Check("oferecer ok", m.Oferecer(A, Item("a1"), 2) == null && s.OfertaA.Count == 1 && s.Versao == 1);
            Check("oferecer avisa os dois lados", m.Efeitos.Count(e => e.Tipo == TrocaEfeitoTipo.Sessao) == 2);
            Check("id repetido recusa", m.Oferecer(A, Item("a1"), 2) == "item_repetido");
            Check("id repetido pelo outro lado recusa", m.Oferecer(B, Item("a1"), 2) == "item_repetido");
            Check("item sem dados recusa", m.Oferecer(A, new TrocaItem { Id = "z", Qtd = 1, Dados = "" }, 2) == "item_invalido");
            Check("qtd zero recusa", m.Oferecer(A, Item("z", 0), 2) == "item_invalido");
            Check("dados enormes recusam", m.Oferecer(A, Item("z", 1, new string('x', TrocaMotor.MaxDados + 1)), 2) == "item_invalido");
            for (int i = 2; i <= TrocaMotor.MaxItens; i++) m.Oferecer(A, Item("a" + i), 3);
            Check("oferta lota em MaxItens", s.OfertaA.Count == TrocaMotor.MaxItens && m.Oferecer(A, Item("extra"), 3) == "cheio");

            m.Efeitos.Clear();
            Check("retirar devolve ao dono como entrega pendente", m.Retirar(A, "a1", 4) == null && s.OfertaA.All(i => i.Id != "a1")
                && Entregas(m, A).Single().Itens.Single().Id == "a1");
            Check("retirar item que nao esta recusa", m.Retirar(A, "a1", 4) == "item_inexistente");
            Check("retirar item do outro lado recusa", m.Retirar(B, "a2", 4) == "item_inexistente");
        }

        // ---- descartar ----
        {
            var m = Aberta(out var s);
            m.Oferecer(A, Item("a1"), 2);
            int v = s.Versao;
            m.Travar(A, v, 3);
            m.Efeitos.Clear();
            Check("descartar tira da oferta", m.Descartar(A, "a1", 4) == null && s.OfertaA.Count == 0);
            Check("descartar NAO devolve (o item ja nao estava com o dono)", !m.Pendentes.Any() && !m.Efeitos.Any(e => e.Tipo == TrocaEfeitoTipo.Entrega));
            Check("descartar zera as travas", !s.TravaA && s.Versao == v + 1);
            Check("descartar item inexistente recusa", m.Descartar(A, "a1", 4) == "item_inexistente");
            Check("id descartado nao fica preso", m.Oferecer(A, Item("a1"), 5) == null);
        }

        // ---- travar e confirmar ----
        {
            var m = Aberta(out var s);
            Check("travar sem nada de nenhum lado recusa", m.Travar(A, 0, 2) == "vazia");
            m.Oferecer(A, Item("a1"), 2);
            m.Oferecer(B, Item("b1"), 2);
            int v = s.Versao;
            Check("travar com versao velha recusa", m.Travar(A, v - 1, 3) == "versao_antiga");
            Check("confirmar sem as duas travas recusa", m.Confirmar(A, v, 3) == "sem_trava");
            m.Travar(A, v, 3);
            Check("confirmar com so uma trava recusa", m.Confirmar(A, v, 3) == "sem_trava");
            m.Travar(B, v, 3);
            Check("as duas travadas", s.TravaA && s.TravaB);
            m.Confirmar(A, v, 4);
            Check("confirmacao de um so nao conclui", m.SessaoDe(A) != null && s.ConfA && !s.ConfB);

            // golpe: B troca a oferta depois de A confirmar
            m.Oferecer(B, Item("b2"), 5);
            Check("mudar a oferta zera travas e confirmacoes", !s.TravaA && !s.TravaB && !s.ConfA && !s.ConfB && s.Versao == v + 1);
            Check("A confirmar a versao que viu recusa", m.Confirmar(A, v, 6) == "versao_antiga" || m.Confirmar(A, v, 6) == "sem_trava");
            Check("nada foi entregue ainda", !m.Pendentes.Any());
        }

        // ---- conclusao ----
        {
            var m = Aberta(out var s);
            m.Oferecer(A, Item("a1"), 2);
            m.Oferecer(A, Item("a2"), 2);
            m.Oferecer(B, Item("b1"), 2);
            int v = s.Versao;
            m.Travar(A, v, 3); m.Travar(B, v, 3);
            m.Confirmar(A, v, 4);
            m.Efeitos.Clear();
            Check("segunda confirmacao conclui", m.Confirmar(B, v, 4) == null && m.SessaoDe(A) == null && m.SessaoDe(B) == null);
            Check("A recebe o que B deu", Entregas(m, A).Single().Itens.Select(i => i.Id).SequenceEqual(new[] { "b1" }));
            Check("B recebe o que A deu", Entregas(m, B).Single().Itens.Select(i => i.Id).OrderBy(x => x).SequenceEqual(new[] { "a1", "a2" }));
            Check("os dois recebem Fim concluida", m.Efeitos.Count(e => e.Tipo == TrocaEfeitoTipo.Fim && e.Motivo == "concluida") == 2);
            Check("itens preservam os dados", Entregas(m, B).Single().Itens.Any(i => i.Id == "a1" && i.Dados == "dados-a1" && i.Qtd == 1));
            Check("nenhum item duplicado", TodosIds(m).Count == TodosIds(m).Distinct().Count() && TodosIds(m).Count == 3);
            Check("confirmar de novo nao executa outra vez", m.Confirmar(B, v, 5) == "sem_sessao");
        }

        // ---- presente de mao unica ----
        {
            var m = Aberta(out var s);
            m.Oferecer(A, Item("a1"), 2);
            int v = s.Versao;
            m.Travar(A, v, 3); m.Travar(B, v, 3);
            m.Confirmar(A, v, 4); m.Efeitos.Clear();
            m.Confirmar(B, v, 4);
            Check("oferta de um lado so e um presente", Entregas(m, B).Count() == 1 && !Entregas(m, A).Any());
        }

        // ---- cancelamento ----
        {
            var m = Aberta(out var s);
            m.Oferecer(A, Item("a1"), 2);
            m.Oferecer(B, Item("b1"), 2);
            m.Efeitos.Clear();
            Check("cancelar fecha para os dois", m.Cancelar(B, "saiu") == null && m.SessaoDe(A) == null);
            Check("cada dono recebe o que era dele", Entregas(m, A).Single().Itens.Single().Id == "a1" && Entregas(m, B).Single().Itens.Single().Id == "b1");
            Check("Fim leva o motivo", m.Efeitos.Count(e => e.Tipo == TrocaEfeitoTipo.Fim && e.Motivo == "saiu") == 2);
            Check("cancelar sem sessao e inofensivo", m.Cancelar(B, "saiu") == "sem_sessao");
            Check("nada duplicado", TodosIds(m).Count == 2);

            var t = Aberta(out var s2);
            t.Oferecer(A, Item("a1"), 2);
            t.Tick(2 + TrocaMotor.InativaSeg - 1);
            Check("sessao parada vive ate InativaSeg", t.SessaoDe(A) != null);
            t.Efeitos.Clear();
            t.Tick(2 + TrocaMotor.InativaSeg);
            Check("sessao parada expira e devolve a oferta", t.SessaoDe(A) == null && Entregas(t, A).Single().Itens.Single().Id == "a1");
        }

        // ---- entregas pendentes: confirmacao e reenvio ----
        {
            var m = Aberta(out var s);
            m.Oferecer(A, Item("a1"), 2);
            m.Cancelar(A, "x");
            var e = m.Pendentes.Single();
            Check("entrega pendente fica ate o Recebido", m.Pendentes.Count() == 1);
            Check("Recebido de outro jogador nao apaga", !m.Recebido(B, e.Id) && m.Pendentes.Count() == 1);
            m.Efeitos.Clear();
            m.Reenviar(A);
            Check("Reenviar manda de novo a quem nao confirmou", Entregas(m, A).Single().Id == e.Id);
            m.Efeitos.Clear();
            m.Reenviar(B);
            Check("Reenviar nao manda o que e de outro", !m.Efeitos.Any());
            Check("Recebido do dono apaga", m.Recebido(A, e.Id) && !m.Pendentes.Any());
            Check("Recebido duas vezes e inofensivo", !m.Recebido(A, e.Id));
        }

        // ---- persistencia ----
        {
            var m = Aberta(out var s);
            m.Oferecer(A, Item("a1", 5, "dados com\ttab\ne linha"), 2);
            m.Oferecer(B, Item("b1"), 2);
            m.Retirar(B, "b1", 3);                       // b1 vira entrega pendente de B
            string texto = m.Serializar();

            var r = new TrocaMotor();
            Check("carregar le o arquivo", r.Carregar(texto));
            var porDono = r.Pendentes.GroupBy(e => e.Dono).ToDictionary(g => g.Key, g => g.SelectMany(e => e.Itens).ToList());
            Check("restart devolve a oferta da sessao aberta ao dono", porDono[A].Single().Id == "a1" && porDono[A].Single().Qtd == 5);
            Check("dados com tab e quebra de linha sobrevivem", porDono[A].Single().Dados == "dados com\ttab\ne linha");
            Check("entrega pendente anterior sobrevive", porDono[B].Single().Id == "b1");
            Check("nao ha sessao depois do restart", r.SessaoDe(A) == null && r.SessoesAtivas == 0);
            Check("ids de entrega nao se repetem depois de carregar",
                r.Pendentes.Select(e => e.Id).Distinct().Count() == r.Pendentes.Count());

            long maior = r.Pendentes.Max(e => e.Id);
            r.Pedir(A, B, 0); r.Aceitar(B, 1);
            r.Oferecer(A, Item("novo"), 2);
            r.Cancelar(A, "x");
            Check("entrega nova nao colide com as carregadas", r.Pendentes.Max(e => e.Id) > maior && r.Pendentes.Count() == 3);
            Check("id de item carregado continua em uso", r.Pedir(A, B, 0) == null && r.Aceitar(B, 1) == null && r.Oferecer(A, Item("a1"), 2) == "item_repetido");

            Check("serializar duas vezes seguidas e estavel", m.Serializar() == texto);
        }

        // ---- arquivo ruim ----
        {
            var r = new TrocaMotor();
            Check("texto vazio nao lanca", !r.Carregar("") && !r.Pendentes.Any());
            Check("texto de outra versao e ignorado", !r.Carregar("TROCA9\nE\t1\t2\n") && !r.Pendentes.Any());
            Check("lixo nao lanca", !r.Carregar("TROCA1\nE\tabc\n") );
            string bom = Aberta(out var s) is var m && m.Oferecer(A, Item("a1"), 2) == null && m.Oferecer(A, Item("a2"), 2) == null ? m.Serializar() : "";
            var cortado = new TrocaMotor();
            bool ok = cortado.Carregar(bom + "I\t!!!nao-base64!!!\tx\t1\ty\n");
            Check("base64 quebrado nao lanca e mantem o que veio antes", !ok && cortado.Pendentes.Sum(e => e.Itens.Count) == 2);
        }

        // ---- varias sessoes ao mesmo tempo ----
        {
            var m = new TrocaMotor();
            m.Pedir(A, B, 0); m.Aceitar(B, 0);
            m.Pedir(C, 400, 0); m.Aceitar(400, 0);
            m.Oferecer(A, Item("a1"), 1);
            m.Oferecer(C, Item("c1"), 1);
            Check("duas sessoes sao independentes", m.SessoesAtivas == 2 && m.SessaoDe(A).OfertaA.Count == 1 && m.SessaoDe(C).OfertaA.Count == 1 && m.SessaoDe(B).OfertaB.Count == 0);
            m.Cancelar(A, "x");
            Check("cancelar uma nao mexe na outra", m.SessaoDe(C) != null && m.SessaoDe(C).OfertaA.Count == 1);
            Check("um jogador de uma sessao nao mexe na oferta de outra", m.Retirar(400, "a1", 1) == "item_inexistente");
        }

        Console.WriteLine($"\n{_pass} ok, {_fail} falha(s)");
        return _fail == 0 ? 0 : 1;
    }
}
