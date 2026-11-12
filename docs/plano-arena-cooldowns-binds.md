# Plano — arena, cooldowns, binds e pets (2026-11-04)

Ordem: **bugs primeiro** (arena + travada do TAB), depois **cooldowns e binds**, por último
**pets e recompensas**. Cada fase termina num checkpoint de teste local antes de seguir;
release/deploy só no fim de cada fase que for para produção (com `/pre-release`).

**Regra dos checkboxes:** abrir o PR assim que a implementação do item terminar (compilando),
sem esperar o teste no jogo. Marcar o item como feito (`[x]`) assim que o PR dele for aberto,
com o link do PR logo abaixo. O teste no jogo e o merge ficam para o checkpoint da fase.

**Execução automática (`docs/run-plan.ps1`):** uma sessão por seção (`### x.y`, ou a fase
inteira quando ela não tem seções), um PR por seção cobrindo todos os checkboxes dela.
Itens `- [ ] (manual)` são do usuário (teste/medição no jogo) e o script pula.
`<!-- run-plan: model=... -->` logo abaixo do título da fase escolhe o modelo da fase.

Repos envolvidos:
- **montarias** — menu de pets (`PetMenu`), só na 1.6.
- **npc-valheim** — toda a arena (`NpcValheim/Arena/*`, `UI/Arena*`).
- **Deadheim** — retreat (`Retreat.cs`, `Pvp/PvpModule.cs`), Hearthstone (`Hearthstone/`),
  cama, pets (`Pets.cs`), Central do TAB (`Menus/MenuHub.cs`, `Menus/Integracoes.cs`), PvE (`Pvp/PvpPve.cs`).

---

## Fase 0 — Inventário (em andamento)
- [x] Fechar a branch `fix/inventario-delete` (lixeira) que já está aberta.
      PR: https://github.com/Deadheim-project/Deadheim/pull/2

**Checkpoint 0:** lixeira testada no jogo, PR mergeado.

---

## Fase 1 — Arena: correções (npc-valheim, + PvE no Deadheim)

### 1.1 Exploit: jogador "morre" e volta no mesmo lugar com o monstro atacando
Causa provável: `ArenaClient.KnockOut` chama `Refill(player)` (vida cheia) **antes** de olhar a
fase e retorna sem nocautear quando a fase não é `PhaseLive` (preparo, já nocauteado, desafio PvE
em outra fase). O `KnockoutPatch` (`ArenaCombat.cs`) já cancelou a morte → o jogador fica imortal.
- [x] Só cancelar a morte quando o nocaute de fato acontecer; fora disso, a morte segue (ou
      nocaute direto, conforme a fase).
      PR: https://github.com/Deadheim-project/npcs/pull/2

### 1.2 Derrotado vira wisp enquanto a batalha continua
Vale quando o jogador cai e a partida ainda segue: PvP 2x2 ou maior, e PvE com 2 ou mais
jogadores. Substitui o "corpo na borda + câmera" do `ArenaSpectate`.
- [x] **Visual:** esconder o modelo do jogador e mostrar o visual do prefab `Wisp` do Valheim
      no lugar (só visual, sem IA nem drop), sincronizado para todos os clientes pela ZDO.
      PR: https://github.com/Deadheim-project/npcs/pull/3
- [x] **Intangível:** sem colisão com jogadores e monstros; não recebe dano de ninguém
      (`ArenaCombat.VictimPatch`); monstros não o escolhem como alvo (`ArenaBeastBrain` e IA
      vanilla ignoram quem está `PhaseOut`).
      PR: https://github.com/Deadheim-project/npcs/pull/3
- [x] **Não ataca:** bloquear ataque, bloqueio, habilidades, troca de arma e uso de itens;
      também não interage com nada (portas, baús, itens no chão).
      PR: https://github.com/Deadheim-project/npcs/pull/3
- [x] **Movimento:** voa livre como wisp, preso ao raio da arena; Backspace sai para casa.
      Sem o `GameCamera.ToggleFreeFly` (o AzuAntiCheat expulsa).
      PR: https://github.com/Deadheim-project/npcs/pull/3
- [x] **Volta ao normal, sempre:** modelo, colisão, dano, alvo e ataque restaurados quando a
      partida acaba, quando ele sai (Backspace/teleporte), ao deslogar/cair a conexão (ao logar
      de novo nunca fica wisp) e se a partida for cancelada. Restauração num ponto único
      (`ArenaClient.ClearMatch`) + checagem de segurança no spawn (`OnLocalSpawned`).
      PR: https://github.com/Deadheim-project/npcs/pull/3
- [x] 1x1 ou PvE solo: sem wisp, a partida acaba e o jogador volta direto.
      PR: https://github.com/Deadheim-project/npcs/pull/3

### 1.3 Jogador com PvE ligado não toma dano na arena
Decisão: jogador PvE **pode** jogar PvP na arena, toma e dá dano normalmente durante a partida,
e volta a ser PvE quando ela termina.
- [x] Achar onde o `PvpPve` anula o dano hoje e abrir exceção para quem está em partida de
      arena (os dois lados: tomar e dar dano).
      PR: https://github.com/Deadheim-project/Deadheim/pull/3
- [x] Suspender o PvE ao entrar na partida e restaurar o estado anterior no fim, no mesmo ponto
      único da 1.2 (`ArenaClient.ClearMatch`): fim de partida, saída, cancelamento e relog
      (ao logar fora de partida, o PvE do jogador volta como estava).
      PR: https://github.com/Deadheim-project/Deadheim/pull/3
Restrição: fora da partida (mesmo dentro da zona da arena), o PvE continua valendo.

### 1.4 Arena balanceada: "classe não carregou"
Restrição: manter `PrazoKit` = 60 (já é o máximo do clamp 10–60); corrigir a causa, não o prazo.
- [x] Ler os logs do servidor das partidas com erro
      (`powershell -ExecutionPolicy Bypass -File ../deadheim-deploy/bin/servidor.ps1 log "kit|classe|Balancead"`).
      PR: https://github.com/Deadheim-project/npcs/pull/4
- [x] Revisar `ArenaEngine` ~1540–1590 (`KitDeadline`/`KitOk`): o cliente confirma o kit só
      depois de equipar? Pode ser a confirmação se perdendo e não o tempo. Corrigir a causa.
      PR: https://github.com/Deadheim-project/npcs/pull/4

### 1.5 Aesir entra na arena pelo menu Arena do TAB
- [x] Ver o que `Integracoes.AbrirArena` abre hoje e o que barra o Aesir; liberar a entrada na
      fila para VIP pelo TAB (sem precisar do NPC).
      PR: https://github.com/Deadheim-project/npcs/pull/5

### 1.6 Travadas ao abrir o TAB / menu de pets que não responde (Deadheim)
- [ ] (manual) Medir no jogo com o `PerfProbe` (`Testing/PerfProbe`) o que roda ao abrir a
      Central (`MenuHub`) e o menu de pets.
- [x] Revisar no código os suspeitos e corrigir: varredura de objetos/recriação de UI a cada
      abertura, pedido de rede síncrono.
      PR: https://github.com/Deadheim-project/Deadheim/pull/4
- [x] Pets (repo **montarias**, `PetMenu`): nenhum canvas do Deadheim cobre o menu; causa provável
      é o `Redraw()` destruir e recriar a lista inteira a cada 1 s e perder cliques. Redesenhar só
      quando o estado mudar. PR no montarias.
      PR: https://github.com/Deadheim-project/Montarias/pull/1

**Checkpoint 1:** teste local — cair numa 2x2 PvP e num PvE em dupla: vira wisp, ninguém o
acerta, monstro o ignora, não consegue atacar, voa só dentro da arena; ao fim da partida, ao
sair e ao relogar volta normal e tangível. Numa 1x1 não há wisp, jogador PvE sem dano, 3 partidas balanceadas seguidas sem erro de
classe, Aesir entra pelo TAB, TAB sem travada. Jogador PvE entra numa partida PvP, toma e dá
dano, e ao fim (e ao relogar) está PvE de novo. Release npc-valheim + Deadheim.

---

## Fase 2 — Cooldowns como buffs (Deadheim)
<!-- run-plan: model=sonnet -->

- [x] Separar o cooldown único de hoje (`PvpModule.MarkRetreatUsed`) em três:
      **Hearthstone**, **Retreat 1**, **Retreat 2**, cada um com config própria (segundos),
      padrão **30 min (1800 s)** para os três.
      PR: https://github.com/Deadheim-project/Deadheim/pull/5
- [x] Criar um `StatusEffect` por cooldown (ícone + tempo restante), aplicado ao usar e
      reaplicado ao logar se ainda faltar tempo — mesmo visual dos buffs `Rested`/`Combat`.
      PR: https://github.com/Deadheim-project/Deadheim/pull/5
- [x] Persistir o fim de cada cooldown no perfil (customData) para sobreviver a relog.
      PR: https://github.com/Deadheim-project/Deadheim/pull/5
- [x] Recusa mostra qual cooldown e quanto falta.
      PR: https://github.com/Deadheim-project/Deadheim/pull/5

**Checkpoint 2:** usar os três em sequência; ver os 3 ícones no topo contando independente;
relogar e conferir que continuam.

---

## Fase 3 — Binds e cama (Deadheim)
<!-- run-plan: model=sonnet -->

- [x] Hover da cama (`Bed_GetHoverText_Patch`) com as três linhas explícitas:
      `[E] dormir · [tecla] definir Hearthstone · Retreat 1 · Retreat 2` (com o estado: definido/não).
      PR: https://github.com/Deadheim-project/Deadheim/pull/6
- [x] Botão de bind abre um menu pequeno: **Hearthstone / Retreat 1 / Retreat 2**
      (mostra o ponto atual de cada e confirma ao gravar).
      PR: https://github.com/Deadheim-project/Deadheim/pull/6
- [x] Mensagem clara ao gravar ("Retreat 2 definido nesta cama").
      PR: https://github.com/Deadheim-project/Deadheim/pull/6

**Checkpoint 3:** gravar os três pontos pela cama usando só o menu, sem consultar ajuda.

---

## Fase 4 — Pets: tecla de Spawn/Despawn (Deadheim)
<!-- run-plan: model=sonnet -->

- [x] No menu dos pets, campo para escolher a tecla (salva por jogador, como as AesirTeclas).
      Sem tecla padrão: fica desligado até o jogador escolher uma.
      PR: https://github.com/Deadheim-project/Montarias/pull/2
- [x] Tecla alterna invocar/guardar o pet ativo (respeitando combate/arena se necessário).
      PR: https://github.com/Deadheim-project/Montarias/pull/2

**Checkpoint 4:** trocar a tecla no menu, invocar e guardar o pet, relogar e a tecla continua.

---

## Fase 5 — Recompensas da arena balanceada (npc-valheim)
<!-- run-plan: model=sonnet -->

- [x] Hoje a vitória paga moedas (`ArenaEngine` ~2620). Criar uma tabela de recompensas
      própria da balanceada, separada da normal, **no mesmo formato da normal** (mesma regra de
      pagamento, só os valores são próprios).
      PR: https://github.com/Deadheim-project/npcs/pull/6
- [x] Cada recompensa configurável individualmente (item, quantidade), editável no
      F8 > Arena > Balanceada e sincronizada do servidor.
      PR: https://github.com/Deadheim-project/npcs/pull/6
- [x] Entrega pelo correio, como as do desafio.
      PR: https://github.com/Deadheim-project/npcs/pull/6

**Checkpoint 5:** vencer uma normal e uma balanceada e receber recompensas diferentes; mudar
uma recompensa no F8 e a próxima partida já usa o valor novo.

---

## Decisões (fechadas em 2026-11-04)
1. PvE na arena: pode jogar PvP; toma e dá dano na partida e volta a PvE ao terminar (1.3).
2. Cooldowns: Hearthstone, Retreat 1 e Retreat 2 com padrão de 30 min cada (Fase 2).
3. Tecla do pet: nenhuma até o jogador escolher (Fase 4).
4. Recompensas da balanceada: mesmo formato da normal, com tabela própria (Fase 5).
