using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Deadheim.Troca
{
    internal enum TrocaFase { Pedido, Oferta }

    /// <summary>
    /// Um item em custodia, como os NPCs guardam: nome do prefab ("Wood"), qualidade e quantidade.
    /// Quem recebe ganha o item novo do prefab (durabilidade cheia, sem estilo nem nome de quem fez).
    /// </summary>
    internal sealed class TrocaItem
    {
        public string Id = "";
        /// <summary>Nome do prefab, nunca o nome traduzido ("$item_wood").</summary>
        public string Nome = "";
        public int Qualidade = 1;
        public int Qtd;
        /// <summary>Quem ofereceu. Entrega ao proprio Origem = devolucao; ao outro = recebimento.</summary>
        public long Origem;
        /// <summary>
        /// O dono ja tirou o item do inventario E gravou o personagem (avisou "persistido"). Sem isso a
        /// troca nao conclui: matar o jogo antes do save traria o item de volta junto com o do destino.
        /// </summary>
        public bool Seguro;
    }

    /// <summary>
    /// Itens que o servidor deve a um jogador (troca concluida, oferta devolvida). Fica guardada
    /// ate o cliente confirmar com Recebido: se ele cair no caminho, o servidor reenvia no login.
    /// </summary>
    internal sealed class TrocaEntrega
    {
        public long Id;
        public long Dono;
        public List<TrocaItem> Itens = new List<TrocaItem>();
    }

    internal sealed class TrocaSessao
    {
        public long Id;
        public long A;
        public long B;
        public TrocaFase Fase;
        public List<TrocaItem> OfertaA = new List<TrocaItem>();
        public List<TrocaItem> OfertaB = new List<TrocaItem>();
        public bool TravaA, TravaB, ConfA, ConfB;
        /// <summary>Sobe a cada mudanca de oferta; travar/confirmar so vale para a versao que o jogador viu.</summary>
        public int Versao;
        public double Atividade;

        public long Outro(long jogador) => jogador == A ? B : A;
        public List<TrocaItem> Oferta(long jogador) => jogador == A ? OfertaA : OfertaB;
        public bool Trava(long jogador) => jogador == A ? TravaA : TravaB;
        public bool Conf(long jogador) => jogador == A ? ConfA : ConfB;
    }

    internal enum TrocaEfeitoTipo
    {
        /// <summary>Pedido novo: perguntar a <see cref="TrocaEfeito.Para"/> se aceita.</summary>
        Pedido,
        /// <summary>Estado da sessao mudou: redesenhar a janela de <see cref="TrocaEfeito.Para"/>.</summary>
        Sessao,
        /// <summary>Entregar os itens ao jogador (e reenviar ate chegar o Recebido).</summary>
        Entrega,
        /// <summary>A janela de <see cref="TrocaEfeito.Para"/> fecha, com o motivo.</summary>
        Fim,
    }

    internal sealed class TrocaEfeito
    {
        public TrocaEfeitoTipo Tipo;
        public long Para;
        public TrocaSessao Sessao;
        public TrocaEntrega Entrega;
        public string Motivo;
    }

    /// <summary>
    /// Nucleo da troca entre jogadores, sem Unity: o servidor e a fonte da verdade da sessao.
    /// Cada item existe em um lugar so por vez (oferta em custodia, entrega pendente), nunca em dois.
    /// Os metodos devolvem null quando deu certo ou o codigo do erro; o que o mundo deve fazer
    /// (avisar, entregar, fechar a janela) sai em <see cref="Efeitos"/>.
    /// </summary>
    internal sealed class TrocaMotor
    {
        public const int MaxItens = 8;
        public const int MaxNome = 64;
        public const double PedidoSeg = 30;
        public const double InativaSeg = 180;

        private const string Cabecalho = "TROCA2";
        /// <summary>Arquivo de antes da troca por nome: o item ia inteiro, e o nome era "prefab|qualidade".</summary>
        private const string CabecalhoV1 = "TROCA1";

        private readonly Dictionary<long, TrocaSessao> _porJogador = new Dictionary<long, TrocaSessao>();
        private readonly Dictionary<long, TrocaEntrega> _entregas = new Dictionary<long, TrocaEntrega>();
        private long _proxSessao = 1;
        private long _proxEntrega = 1;

        public readonly List<TrocaEfeito> Efeitos = new List<TrocaEfeito>();

        public int SessoesAtivas => _porJogador.Values.Distinct().Count();
        public IEnumerable<TrocaEntrega> Pendentes => _entregas.Values;
        public List<TrocaSessao> Sessoes => _porJogador.Values.Distinct().ToList();
        public TrocaSessao SessaoDe(long jogador) => _porJogador.TryGetValue(jogador, out var s) ? s : null;

        // ---------------- pedido ----------------

        public string Pedir(long a, long b, double agora)
        {
            if (a == b) return "si_mesmo";
            if (_porJogador.ContainsKey(a)) return "voce_ocupado";
            if (_porJogador.ContainsKey(b)) return "alvo_ocupado";
            var s = new TrocaSessao { Id = _proxSessao++, A = a, B = b, Fase = TrocaFase.Pedido, Atividade = agora };
            _porJogador[a] = s;
            _porJogador[b] = s;
            Efeitos.Add(new TrocaEfeito { Tipo = TrocaEfeitoTipo.Pedido, Para = b, Sessao = s });
            return null;
        }

        public string Aceitar(long b, double agora)
        {
            var s = SessaoDe(b);
            if (s == null || s.Fase != TrocaFase.Pedido || s.B != b) return "sem_pedido";
            s.Fase = TrocaFase.Oferta;
            s.Atividade = agora;
            Avisar(s);
            return null;
        }

        public string Recusar(long b)
        {
            var s = SessaoDe(b);
            if (s == null || s.Fase != TrocaFase.Pedido || s.B != b) return "sem_pedido";
            Encerrar(s, "recusou");
            return null;
        }

        // ---------------- oferta ----------------

        public string Oferecer(long jogador, TrocaItem item, double agora)
        {
            var s = SessaoDe(jogador);
            if (s == null || s.Fase != TrocaFase.Oferta) return "sem_sessao";
            if (item == null || string.IsNullOrEmpty(item.Id) || item.Qtd <= 0 || item.Qualidade < 1) return "item_invalido";
            if (string.IsNullOrEmpty(item.Nome) || item.Nome.Length > MaxNome) return "item_invalido";
            var oferta = s.Oferta(jogador);
            if (oferta.Count >= MaxItens) return "cheio";
            if (IdEmUso(item.Id)) return "item_repetido";
            var copia = Copia(item);
            copia.Origem = jogador;
            copia.Seguro = false;
            oferta.Add(copia);
            Mudou(s, agora);
            return null;
        }

        /// <summary>O cliente tirou os itens do inventario e gravou o personagem: ja podem ser trocados.</summary>
        public string Persistido(long jogador, IEnumerable<string> ids, double agora)
        {
            var s = SessaoDe(jogador);
            if (s == null || s.Fase != TrocaFase.Oferta) return "sem_sessao";
            bool mudou = false;
            foreach (string id in ids)
            {
                var item = s.Oferta(jogador).Find(x => x.Id == id);
                if (item == null || item.Seguro) continue;
                item.Seguro = true;
                mudou = true;
            }
            if (mudou) { s.Atividade = agora; Avisar(s); }
            return null;
        }

        private static bool TudoSeguro(TrocaSessao s) => s.OfertaA.All(i => i.Seguro) && s.OfertaB.All(i => i.Seguro);

        public string Retirar(long jogador, string itemId, double agora)
        {
            var s = SessaoDe(jogador);
            if (s == null || s.Fase != TrocaFase.Oferta) return "sem_sessao";
            var oferta = s.Oferta(jogador);
            int i = oferta.FindIndex(x => x.Id == itemId);
            if (i < 0) return "item_inexistente";
            var item = oferta[i];
            oferta.RemoveAt(i);
            Devolver(jogador, new List<TrocaItem> { item });
            Mudou(s, agora);
            return null;
        }

        /// <summary>
        /// Tira da oferta SEM devolver: o cliente avisa que o item ja nao estava no inventario dele
        /// (largou, moveu) quando a custodia foi confirmada; devolver aqui duplicaria o item.
        /// </summary>
        public string Descartar(long jogador, string itemId, double agora)
        {
            var s = SessaoDe(jogador);
            if (s == null || s.Fase != TrocaFase.Oferta) return "sem_sessao";
            if (s.Oferta(jogador).RemoveAll(x => x.Id == itemId) == 0) return "item_inexistente";
            Mudou(s, agora);
            return null;
        }

        // ---------------- confirmacao ----------------

        public string Travar(long jogador, int versaoVista, double agora)
        {
            var s = SessaoDe(jogador);
            if (s == null || s.Fase != TrocaFase.Oferta) return "sem_sessao";
            if (versaoVista != s.Versao) return "versao_antiga";
            if (s.OfertaA.Count + s.OfertaB.Count == 0) return "vazia";
            if (!TudoSeguro(s)) return "aguarde";
            if (jogador == s.A) s.TravaA = true; else s.TravaB = true;
            s.Atividade = agora;
            Avisar(s);
            return null;
        }

        public string Confirmar(long jogador, int versaoVista, double agora)
        {
            var s = SessaoDe(jogador);
            if (s == null || s.Fase != TrocaFase.Oferta) return "sem_sessao";
            if (versaoVista != s.Versao) return "versao_antiga";
            if (!(s.TravaA && s.TravaB)) return "sem_trava";
            if (!TudoSeguro(s)) return "aguarde";
            if (jogador == s.A) s.ConfA = true; else s.ConfB = true;
            s.Atividade = agora;
            if (s.ConfA && s.ConfB)
            {
                // A sessao some ANTES de qualquer entrega sair: nao ha segunda execucao possivel.
                Remover(s);
                Devolver(s.A, s.OfertaB);
                Devolver(s.B, s.OfertaA);
                Efeitos.Add(new TrocaEfeito { Tipo = TrocaEfeitoTipo.Fim, Para = s.A, Sessao = s, Motivo = "concluida" });
                Efeitos.Add(new TrocaEfeito { Tipo = TrocaEfeitoTipo.Fim, Para = s.B, Sessao = s, Motivo = "concluida" });
                return null;
            }
            Avisar(s);
            return null;
        }

        // ---------------- fim, relogio, entregas ----------------

        /// <summary>Cancela a troca do jogador (fechou a janela, morreu, saiu, ficou longe).</summary>
        public string Cancelar(long jogador, string motivo)
        {
            var s = SessaoDe(jogador);
            if (s == null) return "sem_sessao";
            Encerrar(s, motivo);
            return null;
        }

        public void Tick(double agora)
        {
            foreach (var s in _porJogador.Values.Distinct().ToList())
            {
                double limite = s.Fase == TrocaFase.Pedido ? PedidoSeg : InativaSeg;
                if (agora - s.Atividade >= limite) Encerrar(s, "expirou");
            }
        }

        /// <summary>O cliente confirmou que pos os itens no inventario (ou no chao): a divida some.</summary>
        public bool Recebido(long jogador, long entregaId)
        {
            if (!_entregas.TryGetValue(entregaId, out var e) || e.Dono != jogador) return false;
            _entregas.Remove(entregaId);
            return true;
        }

        /// <summary>Reenvia o que o jogador ainda nao confirmou (login, renascimento).</summary>
        public void Reenviar(long jogador)
        {
            foreach (var e in _entregas.Values.Where(x => x.Dono == jogador).OrderBy(x => x.Id))
                Efeitos.Add(new TrocaEfeito { Tipo = TrocaEfeitoTipo.Entrega, Para = jogador, Entrega = e });
        }

        // ---------------- persistencia ----------------

        /// <summary>
        /// Estado para o disco: as entregas pendentes mais as ofertas das sessoes abertas, que num
        /// restart voltam a seus donos (a janela nao sobrevive ao restart, os itens sim).
        /// </summary>
        public string Serializar()
        {
            var todas = _entregas.Values.OrderBy(e => e.Id).ToList();
            long prox = _proxEntrega;
            foreach (var s in _porJogador.Values.Distinct())
            {
                if (s.OfertaA.Count > 0) todas.Add(new TrocaEntrega { Id = prox++, Dono = s.A, Itens = s.OfertaA });
                if (s.OfertaB.Count > 0) todas.Add(new TrocaEntrega { Id = prox++, Dono = s.B, Itens = s.OfertaB });
            }
            var sb = new StringBuilder();
            sb.Append(Cabecalho).Append('\n');
            sb.Append("N\t").Append(prox.ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (var e in todas)
            {
                sb.Append("E\t").Append(e.Id.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(e.Dono.ToString(CultureInfo.InvariantCulture)).Append('\n');
                foreach (var i in e.Itens)
                    sb.Append("I\t").Append(B64(i.Id)).Append('\t').Append(B64(i.Nome)).Append('\t')
                      .Append(i.Qualidade.ToString(CultureInfo.InvariantCulture)).Append('\t')
                      .Append(i.Qtd.ToString(CultureInfo.InvariantCulture)).Append('\t')
                      .Append(i.Origem.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Le o estado gravado. Texto vazio, de outra versao ou quebrado devolve false e nunca lanca;
        /// as entregas lidas antes do ponto quebrado ficam. O arquivo TROCA1 (item inteiro) vira
        /// nome + qualidade + quantidade, que ele ja guardava ao lado dos dados.
        /// </summary>
        public bool Carregar(string texto)
        {
            _entregas.Clear();
            _porJogador.Clear();
            if (string.IsNullOrEmpty(texto)) return false;
            var linhas = texto.Split('\n');
            string cabecalho = linhas[0].TrimEnd('\r');
            if (cabecalho != Cabecalho && cabecalho != CabecalhoV1) return false;
            bool v1 = cabecalho == CabecalhoV1;
            TrocaEntrega atual = null;
            long maxId = 0;
            bool ok = true;
            try
            {
                foreach (var bruta in linhas.Skip(1))
                {
                    var linha = bruta.TrimEnd('\r');
                    if (linha.Length == 0) continue;
                    var p = linha.Split('\t');
                    if (p[0] == "N") _proxEntrega = Math.Max(1, long.Parse(p[1], CultureInfo.InvariantCulture));
                    else if (p[0] == "E")
                    {
                        atual = new TrocaEntrega { Id = long.Parse(p[1], CultureInfo.InvariantCulture), Dono = long.Parse(p[2], CultureInfo.InvariantCulture) };
                        _entregas[atual.Id] = atual;
                        maxId = Math.Max(maxId, atual.Id);
                    }
                    else if (p[0] == "I" && atual != null)
                        atual.Itens.Add(v1 ? ItemV1(p, atual.Dono) : new TrocaItem
                        {
                            Id = D64(p[1]),
                            Nome = D64(p[2]),
                            Qualidade = Math.Max(1, int.Parse(p[3], CultureInfo.InvariantCulture)),
                            Qtd = int.Parse(p[4], CultureInfo.InvariantCulture),
                            Origem = long.Parse(p[5], CultureInfo.InvariantCulture),
                            Seguro = true,
                        });
                }
            }
            catch (Exception)
            {
                ok = false;
            }
            _proxEntrega = Math.Max(_proxEntrega, maxId + 1);
            return ok;
        }

        // ---------------- internos ----------------

        private void Mudou(TrocaSessao s, double agora)
        {
            s.Versao++;
            s.TravaA = s.TravaB = s.ConfA = s.ConfB = false;
            s.Atividade = agora;
            Avisar(s);
        }

        private void Avisar(TrocaSessao s)
        {
            Efeitos.Add(new TrocaEfeito { Tipo = TrocaEfeitoTipo.Sessao, Para = s.A, Sessao = s });
            Efeitos.Add(new TrocaEfeito { Tipo = TrocaEfeitoTipo.Sessao, Para = s.B, Sessao = s });
        }

        private void Remover(TrocaSessao s)
        {
            _porJogador.Remove(s.A);
            _porJogador.Remove(s.B);
        }

        private void Encerrar(TrocaSessao s, string motivo)
        {
            Remover(s);
            Devolver(s.A, s.OfertaA);
            Devolver(s.B, s.OfertaB);
            Efeitos.Add(new TrocaEfeito { Tipo = TrocaEfeitoTipo.Fim, Para = s.A, Sessao = s, Motivo = motivo });
            Efeitos.Add(new TrocaEfeito { Tipo = TrocaEfeitoTipo.Fim, Para = s.B, Sessao = s, Motivo = motivo });
        }

        private void Devolver(long dono, List<TrocaItem> itens)
        {
            if (itens.Count == 0) return;
            var e = new TrocaEntrega { Id = _proxEntrega++, Dono = dono, Itens = new List<TrocaItem>(itens) };
            _entregas[e.Id] = e;
            Efeitos.Add(new TrocaEfeito { Tipo = TrocaEfeitoTipo.Entrega, Para = dono, Entrega = e });
        }

        private bool IdEmUso(string id)
            => _porJogador.Values.Any(s => s.OfertaA.Any(i => i.Id == id) || s.OfertaB.Any(i => i.Id == id))
               || _entregas.Values.Any(e => e.Itens.Any(i => i.Id == id));

        /// <summary>Linha TROCA1: "I id nome|qualidade qtd dados [origem]". Os dados do item inteiro ficam para tras.</summary>
        private static TrocaItem ItemV1(string[] p, long dono)
        {
            string[] nome = D64(p[2]).Split('|');
            return new TrocaItem
            {
                Id = D64(p[1]),
                Nome = nome[0],
                Qualidade = nome.Length > 1 && int.TryParse(nome[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int q) ? Math.Max(1, q) : 1,
                Qtd = int.Parse(p[3], CultureInfo.InvariantCulture),
                Origem = p.Length > 5 ? long.Parse(p[5], CultureInfo.InvariantCulture) : dono,
                Seguro = true,
            };
        }

        private static TrocaItem Copia(TrocaItem i) => new TrocaItem { Id = i.Id, Nome = i.Nome ?? "", Qualidade = i.Qualidade, Qtd = i.Qtd, Origem = i.Origem, Seguro = i.Seguro };
        private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));
        private static string D64(string s) => Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}
