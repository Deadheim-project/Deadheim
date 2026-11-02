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

## Teclas tiradas

| Tecla | Mod | Agora |
| --- | --- | --- |
| PageUp / PageDown | RaidSystem | Central: Guilda / Ranking de Guerra (tirado no proprio RaidSystem, 2.2.4) |
| J | NpcValheim | Central: Diario de missoes |
| F7 | NpcValheim | Central: Loja Deadcoins (Aesir) |
| F8 | NpcValheim | Admin: Painel de Admin |
| U | Montarias | Central: Montarias |
| F8 (fixo no codigo) | Montarias | Admin: Montarias (admin) |
| F8 / F9 | AdaptiveNet | Admin: Painel de rede / Marcar incidente |

O Deadheim nao compila esses mods: `Atalhos.cs` zera a tecla no cliente (KeyCode.None /
KeyboardShortcut.Empty) no primeiro frame, de novo se o mod recarregar o cfg, e esconde a opcao de
"Meus ajustes". O F8 fixo do Montarias e barrado por patch no `AdminMenu.Toggle`. As janelas sao
abertas por reflexao (`Integracoes.cs`), com os mesmos passos que a tecla fazia; se um mod renomear
algo, so aquele botao falha e o log diz o nome que sumiu.

Continuam: H (montar, e entrar na arena), P / Shift+P na cama, G perto do barco, Shift+E na planta,
E nos NPCs. Os comandos de chat tambem continuam (`/retreat`, `/depositar`, `/territorio`...).

## Arquivos

| Arquivo | O que faz |
| --- | --- |
| `MenuHub.cs` | A Central: entradas, estado de cada botao, clique, config `[Central]` |
| `AesirJanela.cs` | Janela do Aesir: beneficios e doacao |
| `FerramentasAdmin.cs` | As ferramentas da aba Admin (desenhadas pela `Ajustes/AjustesJanela.cs`) |
| `Integracoes.cs` | Abre as janelas do NpcValheim, Montarias e AdaptiveNet por reflexao |
| `Atalhos.cs` | Zera as teclas dos outros mods e barra o F8 do Montarias |
| `MenuBridge.cs` | Delegates que o RaidSystem preenche (painel da guilda, ranking) |

## Correcao junto

`/depositar` (Aesir 14) duplicava itens: o bau recebia a pilha inteira e o jogador perdia 1. Agora
sai do inventario exatamente o que entrou, o bau e tomado (posse da ZDO) antes de mexer, bau aberto
por outra pessoa fica de fora e item equipado nao e depositado. A versao minima do servidor subiu
para 7.9.0 para todo mundo estar com o conserto.
