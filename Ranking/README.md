# Ranking do mundo

Abre por três caminhos, todos na mesma janela:

| Onde | Como |
| --- | --- |
| **Tab** (inventário) → Central Deadheim → **Ranking** | o botão mostra a sua posição na Lenda (`Você é #12 de 87 na Lenda`) |
| **ESC → Ranking** | logo abaixo de ESC → Deadheim; o ESC fecha o ranking e volta ao menu |
| `/ranking` | no chat |

A janela segue o visual das telas dos NPCs (`Shared/ValheimUi`: painel de madeira, fontes Norse e
Averia, botões e ícones do jogo).

## O que a janela tem

- **Categorias** (esquerda), agrupadas, cada uma com a sua posição nela (top 3 em ouro, prata e bronze):
  - Geral: **Lenda**, que junta tudo com pesos (`PesosLenda`); o detalhe diz de onde vêm os pontos de cada um (`chefes 40% · caça 30% · PvP 15%`).
  - Combate: Abates PvP (com K/D e PK), Guerra (pontos de guilda do RaidSystem), Chefes, Caçador.
  - Progresso: Habilidades (soma dos níveis), Nível (só com EpicMMO), Tempo de jogo.
  - Ofício: Construtor, Artesão, Lenhador, Minerador, Domador.
  - Exploração: Andarilho (distância), Navegador.
  - Riqueza: Fortuna (moedas; `MostrarRiqueza`).
  - Mundo: **Feitos do mundo**, o primeiro a derrotar cada chefe, com data e quantos já derrotaram.
- **Temporada / Semana**: a semana começa na segunda 00:00 do fuso do servidor (`FusoHoras`) e conta só o que cresceu nela.
- **Vikings / Guildas**: qualquer categoria por guilda (soma dos membros, com o destaque de cada uma).
- **Filtro**: todos, só online, minha guilda. **Busca** por nome ou guilda. As posições continuam as do ranking inteiro.
- **Pódio** dos três primeiros e tabela com a **seta do dia** (`+2` subiu, `-1` desceu, `novo`, comparando com a meia-noite).
- **A sua linha**, sempre visível embaixo: posição, valor, a seta e **quanto falta para passar quem está na sua frente** (`faltam 56 pts para passar Bia (#11)`), com o botão "Ir para mim".
- **Ficha do viking**: clique em qualquer nome (tabela ou pódio) para ver a posição dele em todas as categorias, os feitos, a guilda, se é Aesir e quando foi visto.
- Atalhos: setas ↑/↓ trocam a categoria; ESC fecha a ficha e depois a janela.
- Online com o ponto verde, Aesir com a marca dourada, guilda entre colchetes.

## De onde vêm os números

| Número | Fonte | Conferido |
| --- | --- | --- |
| Abates e mortes de PvP, contador de PK | `PvpStore` do servidor | é do servidor |
| Pontos e conquistas da guerra | `RaidSystem.ScoreManager`, por reflexão (sem RaidSystem a categoria some) | é do servidor |
| Tempo de jogo | o servidor conta quem está conectado | é do servidor |
| Monstros, mortes, construções, fabricações, árvores, mineração, distância, navegação, domados, chefes | o perfil do personagem (`PlayerProfile.m_playerStats`, que o jogo já registra), relatado pelo cliente a cada minuto | o servidor confere |
| Habilidades, nível, moedas | o personagem, relatado pelo cliente | teto absoluto |

O que o servidor confere em cada relato (`RankingRegras`, testado em `Testing/RankingSemJogo`):

- nada que é do servidor é aceito do cliente, e chave desconhecida é ignorada;
- contador nunca desce e sobe no máximo o que dá para jogar no tempo desde o último relato
  (ex.: 10 monstros/s, 120 m/s de distância, com folga mínima de 2 min);
- o id vem da ZDO do personagem de quem mandou (`PvpPeer`, com o remetente já conferido pelo `RemetenteRpc`), nunca do pacote;
- o primeiro relato de um personagem entra inteiro (é o histórico dele) e **não conta na semana**.

Quem trapacear no primeiro relato aparece na Temporada: o admin tira com `/rankingadmin ocultar <nome>`
(volta com `mostrar`; `apagar` zera os dados e o próximo relato recomeça o histórico).

## Feitos do mundo

Quando um relato mostra um chefe que o personagem nunca tinha derrotado e ninguém ainda tem o feito,
ele fica com o feito, o servidor anuncia para todos (centro da tela e chat) e o log registra.
Chefe que já estava no perfil no primeiro relato (derrotado antes de o ranking existir) vira feito
"antes do ranking", sem anúncio. A ordem dos chefes é a do `[PvP - Chefes] BossOrder`.

## Config (`Detalhes.Deadheim.cfg`, seção `[Ranking]`, sincronizada)

| Chave | Padrão | O que faz |
| --- | --- | --- |
| `RankingAtivo` | true | Liga o ranking (Central, ESC e `/ranking`) |
| `PesosLenda` | ver abaixo | Quanto vale cada unidade na Lenda |
| `MostrarRiqueza` | true | Mostra a categoria Fortuna |
| `FusoHoras` | -3 | Fuso da virada da semana (segunda 00:00) e da seta do dia (meia-noite) |
| `DiasInativo` | 60 | Quem não entra há mais dias sai da lista (os dados ficam e voltam quando ele entrar). 0 = nunca sai |

`PesosLenda` padrão:
`skills=2|chefes=150|chefes.k=10|monstros=1|pvp.k=15|guerra=5|build=0.2|craft=0.5|arvores=0.5|mina=0.1|dist=0.002|mar=0.004|domados=5|online=0.0005`
(1 nível de habilidade = 2 pts, chefe diferente = 150, 500 m andados = 1, 1 hora conectado = 1,8...).
Moedas ficam fora da Lenda de propósito.

## Arquivos

| Arquivo | O que faz |
| --- | --- |
| `RankingDados.cs` | Núcleo sem Unity: registros, regras de relato, semana, categorias, classificação, guildas, números no jeito brasileiro e o arquivo |
| `RankingRede.cs` | Config, RPCs `DH_Ranking_ToServer` / `DH_Ranking_ToClient`, coleta no cliente, servidor (relatos, PvP, guerra, tempo online, virada da semana e do dia, feitos, foto comprimida), `/ranking` e `/rankingadmin` |
| `RankingJanela.cs` | A janela e a ficha |

Servidor: `BepInEx/config/Deadheim/ranking-<mundo>.txt`, uma linha por registro (`R` cabeçalho,
`J` jogador, `F` feito), campos `chave=valor` separados por TAB, salvo a cada 2 min, com o mundo e
ao desligar. Edite só com o servidor desligado. Linha ilegível é guardada num `.linhas-ignoradas-*`
antes de qualquer save.

A foto que vai ao cliente é montada uma vez a cada 8 s e dividida entre quem pede (refeita na hora
para quem acabou de relatar), comprimida, com as chaves numa tabela. O cliente classifica sozinho:
trocar categoria, período, visão, filtro ou busca não usa a rede. Com a janela aberta ela se renova
a cada 20 s.

## Testes

- **Sem o jogo** (`Testing/RankingSemJogo`, 50 checagens): semana e dia no fuso, teto por segundo,
  contador que não desce, chave do servidor recusada, primeiro relato, feitos, virada da semana,
  setas, empate, guildas, Lenda, números e o arquivo (ida e volta, linha quebrada, nome com TAB).

      dotnet run -c Release --project Testing\RankingSemJogo

- **No jogo** (falta): abrir pela Central, pelo ESC e por `/ranking`; trocar categoria, período,
  visão, filtro e busca; ficha; ESC; conferir no log do cliente a linha
  `Ranking: contadores do perfil: ...` (os nomes que o jogo usa) e, no servidor, o
  `ranking-<mundo>.txt` depois de um minuto.
