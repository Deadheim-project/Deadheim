# Menus sem tecla: Central Deadheim e aba Admin (7.9.0)

Os menus nao abrem mais por teclas espalhadas em cada mod. Sao dois lugares:

| Onde | Quem | O que tem |
| --- | --- | --- |
| **Tab** (inventario) -> **Central Deadheim** | todos | Montarias, Guilda, Ranking de Guerra, Diario de missoes, Arena, Troca de moedas e as funcoes Aesir |
| **ESC -> Deadheim -> Admin** | so admin | Ferramentas de admin (painel, montarias, territorios, totem, rede, comandos) e a config do servidor |

## Central Deadheim (hub do inventario)

Painel ao lado do inventario (direita por padrao; `[Central] Lado`), aberto sempre que o inventario
esta na tela (Tab ou um bau aberto). Cabecalho com o nome do personagem e o selo AESIR/VIAJANTE.

- **Menus**: Montarias (era o U), Guilda (PageUp), Ranking de Guerra (PageDown), Diario de missoes
  (J), Arena, Troca de moedas (`/trocar`). Cada botao mostra uma linha de estado: a guilda do
  jogador, a habilidade de montaria...
- **Aesir**: Loja Deadcoins (era o F7), Retreat e Retreat 2 (com a recarga na linha de estado),
  Depositar nos baus, Ordenar bau aberto (so com um bau aberto), Transmog (se nao exigir o NPC),
  Plantas salvas. Para quem nao e Aesir os botoes aparecem travados (icone apagado, marca AESIR);
  clicar abre a janela do Aesir com a funcao destacada.
- **Seja Aesir / Voce e Aesir**: janela com todos os beneficios, agrupados, e como apoiar (preco,
  instrucoes, PIX e Discord com botao Copiar, pacotes de Deadcoins). Beneficio cuja chave o
  servidor desligou nao aparece.

Linha que o servidor desligou (ou cujo mod nao esta instalado) some. Botao que nao pode ser usado
agora (sem guilda, retreat em recarga, sem bau aberto) fica apagado e, ao clicar, diz o motivo na
linha de baixo da Central.

### Config (`Detalhes.Deadheim.cfg`, secao `[Central]`)

| Chave | Onde vale | O que faz |
| --- | --- | --- |
| `MostrarNoInventario` | jogador | Liga/desliga a Central |
| `Lado` | jogador | `Direita` ou `Esquerda` |
| `AesirPreco` | servidor (sincronizado) | Preco do Aesir na janela |
| `DoacaoComo` | servidor | Instrucoes de doacao |
| `DoacaoPix` | servidor | Chave PIX (vazio = nao aparece) |
| `DoacaoDiscord` | servidor | Convite do Discord (vazio = nao aparece) |
| `DoacaoPacotes` | servidor | Pacotes de Deadcoins separados por `\|` |

O admin muda tudo isso na aba Admin, ao vivo, sem reiniciar. **Preencha `DoacaoPix` e
`DoacaoDiscord` antes de abrir o servidor.**

## Teclas sem padrao

As teclas que abriam menus continuam na config de cada mod, mas **vazias por padrao** (None). Quem
quiser um atalho define a tecla em ESC > Deadheim > Meus ajustes. Os nomes das chaves mudaram para a
tecla gravada num cfg antigo nao voltar sozinha.

| Era | Mod (versao) | Chave nova (vazia) | Agora abre por |
| --- | --- | --- | --- |
| PageUp | RaidSystem 2.2.4 | `[9 - Client] Menu Shortcut` | Central: Guilda |
| PageDown | RaidSystem 2.2.4 | `[9 - Client] Scoreboard Shortcut` | Central: Ranking de Guerra |
| J | NpcValheim 0.1.90 | `[Quests] AtalhoDiario` | Central: Diario de missoes |
| F7 | NpcValheim 0.1.90 | `[VIP] AtalhoLoja` | Central: Loja Deadcoins (Aesir) |
| F8 | NpcValheim 0.1.90 | `[Admin] AtalhoPainel` | Admin: Painel de Admin |
| U | Montarias 0.3.27 | `[Geral] AtalhoMenu` | Central: Montarias |
| F8 (fixo no codigo) | Montarias 0.3.27 | `[Geral] AtalhoAdmin` | Admin: Montarias (admin) |
| Ctrl+F9 (fixo no codigo) | Montarias 0.3.27 | `[Geral] AtalhoFotos` | comando `montarias_fotos` |
| F8 | AdaptiveNet 0.5.4 | `[Diagnostics] OverlayShortcut` | Admin: Painel de rede |
| F9 | AdaptiveNet 0.5.4 | `[Diagnostics] IncidentMarkerShortcut` | Admin: Marcar incidente de rede |

As janelas sao abertas por reflexao (`Integracoes.cs`), com os mesmos passos que a tecla fazia; se
um mod renomear algo, so aquele botao falha e o log diz o nome que sumiu. As dicas do Montarias que
citavam a tecla dizem "Central (Tab)" quando ela esta vazia.

Continuam: H (montar, e entrar na arena), P / Shift+P na cama, G perto do barco, Shift+E na planta,
E nos NPCs. Os comandos de chat tambem continuam (`/retreat`, `/depositar`, `/territorio`...).

## Arquivos

| Arquivo | O que faz |
| --- | --- |
| `MenuHub.cs` | A Central: entradas, estado de cada botao, clique, config `[Central]` |
| `AesirJanela.cs` | Janela do Aesir: beneficios e doacao |
| `FerramentasAdmin.cs` | As ferramentas da aba Admin (desenhadas pela `Ajustes/AjustesJanela.cs`) |
| `Integracoes.cs` | Abre as janelas do NpcValheim, Montarias e AdaptiveNet por reflexao |
| `MenuBridge.cs` | Delegates que o RaidSystem preenche (painel da guilda, ranking) |

## Correcao junto

`/depositar` (Aesir 14) duplicava itens: o bau recebia a pilha inteira e o jogador perdia 1. Agora
sai do inventario exatamente o que entrou, o bau e tomado (posse da ZDO) antes de mexer, bau aberto
por outra pessoa fica de fora e item equipado nao e depositado. A versao minima do servidor subiu
para 7.9.0 para todo mundo estar com o conserto.
