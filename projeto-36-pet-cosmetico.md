# Item 36: Pet cosmético (projeto)

Um bichinho que acompanha o jogador. Só visual: não ataca, não leva dano, não carrega nada, não bloqueia ninguém e não muda nada no PvP.

## Decisão central: pet no cliente, não criatura do servidor

Há duas formas de fazer, e eu recomendo a primeira.

| | A. Visual no cliente (recomendada) | B. Criatura do servidor (como as montarias) |
|---|---|---|
| O que é | Objeto só visual, criado no PC de quem vê, que segue o jogador | Criatura real (`Character` com IA e `ZNetView`) |
| Combate | Não existe para o jogo: sem colisor, sem alvo, sem vida | Precisa de blindagem contra dano, alvo e agressão de monstros |
| PvP | Zero risco: não vira escudo, isca nem esconderijo | Risco de abuso (bloquear passagem, atrair monstros, tank) |
| Peso no servidor | Nenhum | Uma criatura por pet, salva no mundo |
| Esforço | médio | grande, e a IA do Valheim briga com o que se quer |
| Contra | Cada cliente anima o seu; sem interação física | |

Pela regra do projeto (nenhuma vantagem de combate), a opção A.

## Como funciona (opção A)

1. **Posse.** Cada pet é um item nativo individual (`Pet_<id>`), negociável e não consumido ao usar. A posse acompanha o item no inventário; o pet ativo fica na chave `dh_pet` do ZDO do jogador.
2. **Quem vê.** Cada cliente, para cada jogador a até 40 m, lê `dh_pet` e cria o modelo visual local. Se o valor mudar, troca. Se o jogador sair do alcance, apaga.
3. **O modelo.** Cópia do prefab de uma criatura do jogo, sem `Character`, sem IA, sem `ZNetView`, sem colisores. Ficam só as malhas, o `Animator` e o tamanho ajustado. Ou seja, nunca é um objeto de rede.
4. **Movimento.** Um script simples: fica a 2–3 m atrás do dono, no chão do terreno, aponta para onde anda, e passa ao `Animator` a velocidade (anda, corre, parado). Parado por 5 s, senta ou deita se o bicho tiver a animação. Se o dono se afasta mais de 30 m, usa portal, teleporta, monta ou entra em barco, o pet reaparece ao lado dele. Sobre montaria, vai no ombro do jogador só se for ave, senão some até desmontar.
5. **Nome.** `/pet nome <texto>` põe um nome em cima do pet, visível a todos.

## Comandos e uso

- `/pet`: lista os pets que você tem.
- `/pet <id>`: chama aquele pet.
- `/pet off`: guarda o pet.
- `/pet nome <texto>`: nomeia.

## Catálogo inicial (a confirmar no assembly do jogo)

| Pet | Prefab provável | Como se ganha |
|---|---|---|
| Galinha | `Hen` | Aesir |
| Pintinho | `Chicken` | Aesir |
| Leitão | filhote de javali | Aesir |
| Filhote de lobo | `Wolf_cub` | Ficha de Pet |
| Bezerro de lox | filhote de lox | Ficha de Pet |
| Corvo no ombro | `Raven` | Ficha de Pet |
| Pet da temporada | a definir | Passe de temporada |
| Pet de Halloween | a definir | Pacote de Halloween |

Os nomes exatos dos prefabs e as animações de cada um precisam ser conferidos abrindo o assembly do jogo.

## Venda

- **Loja Deadcoins:** cada item individual de pet custa 150 DC na configuração padrão.
- O item não é consumido ao invocar. Negociá-lo transfere a posse para o outro jogador.
- **Temporada e Halloween:** um pet exclusivo em cada, permanente.
- Sem expiração enquanto o jogador guardar o item; ao negociá-lo, a posse passa ao comprador.

## O que o pet NÃO faz (regras)

- Não ataca, não tem vida, não aparece como alvo, não é salvo no mundo.
- Não tem colisor: não bloqueia porta, escada, portal nem passagem.
- Não esconde o jogador: tamanho máximo na altura do joelho.
- Não aparece na arena, no battleground nem em zona de raid (some e volta depois).
- No máximo 12 pets visíveis ao mesmo tempo por cliente (os mais perto); o resto não é desenhado.
- Sem som próprio, para não ser usado para marcar posição de ninguém.

## Config (servidor, sincronizada)

`PetEnabled`, `PetRenderDistance` (40), `PetMaxVisible` (12), `PetCatalog` (`id=prefab;escala;origem`), `PetHiddenZones`.

## Onde entra no código

- **Deadheim:** `Pets.cs` (modelo, movimento, sincronização e comando); `ClonedItems.cs` registra um item por pet.
- **NpcValheim:** itens de pet individuais em `[DeadcoinShop] Items` (150 DC cada).
- **Launcher/guia/catálogo:** nova linha no guia (VIP e Deadcoins) e no catálogo.

## Etapas

1. **Um pet de ponta a ponta** (galinha): modelo, movimento, `/pet`, sincronização com outro jogador.
2. **Catálogo completo** e Ficha de Pet, com a lista de prefabs conferida.
3. **Nome, corvo no ombro, regras de zona e limite de pets.**
4. **Integração** com Halloween e temporada.

Esforço total: de 3 a 4 dias, mais teste no jogo (animações de cada bicho variam e só se vê rodando).

## Riscos

- **Animações:** cada criatura usa parâmetros de `Animator` diferentes (velocidade, andar, correr). Cada pet do catálogo precisa de um ajuste e de teste visual.
- **Prefab limpo:** tirar `Character` e IA sem quebrar o modelo é o ponto mais delicado.
- **Campos privados do jogo:** usar `AccessTools`, e rodar a checagem de acesso privado antes do release.
- **Sincronização:** o ZDO do jogador precisa de uma chave nova; vale testar com dois clientes.

## Decisões para você

1. Aesir ganha 1 pet de graça (recomendado) ou todos os pets são fichas?
2. Pets de ficha: 150 DC está bom?
3. O pet pode aparecer em PvP aberto (recomendado) ou some enquanto o jogador está em combate?
4. Quer o corvo no ombro (mais bonito, mais trabalho) já na primeira versão?
