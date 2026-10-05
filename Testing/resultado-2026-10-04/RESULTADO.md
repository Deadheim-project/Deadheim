# Resultado: teste no jogo do Deadheim 7.5.0 (Dead Token)

Rodado em 2026-10-04 e 05 no PC de desenvolvimento recem-formatado: Valheim 1.0.16, servidor dedicado
1.0.16 em `D:\ValheimDedicatedServer` (SteamCMD), um cliente (`run-pvp-test.ps1 -Solo`), tela 960x540.
Branch `claude/teste-driver`, sobre o Dead Token (`6d157ce`).

## Resumo

| Teste | Resultado |
| --- | --- |
| PvpSemJogo | 71/0 |
| AjustesSemJogo | 47/0 |
| Solo completo, 7.4.0, driver antigo (primeira rodada no jogo desde o 7.3.1) | 280/14, todas do driver |
| Solo completo, 7.5.0, driver corrigido | 303/1 (corrida do proprio teste, corrigida abaixo) |
| Passo tokens depois das correcoes | 32/0 |
| Admin (`-Admin`): bounty, bounty-pagar, bounty-expira, ajustes, 7.5.0 | 68/0 |
| Dois clientes (A/B, sem `-Solo`) | nao roda com uma conta Steam so |

Nenhum bug do mod. Todas as falhas eram do roteiro de teste.

## Falhas da primeira rodada (7.4.0) e o que era

- **territorio, castelo-invasor, rank (6):** o `MoveTo` usava `GetSolidHeight`, que devolve o topo do
  primeiro solido. No mundo gerado neste PC o ponto do ward caiu em cima de algo alto e o personagem
  morreu de queda (`Morri: causa=Pve ... ultimoGolpe=Fall`); dai o dano 0 (estava morto), a "morte no
  castelo" que o servidor recebeu foi a queda e o rank contou uma morte a menos. Num mundo novo os dois
  passos davam 26/0. Corrigido: `SafeSpot` procura chao livre ate 6 m e, ate assentar, o teleporte nao
  conta como queda.
- **agressor (6):** o teste e anterior a M3. O Dummy bate no passo anterior e o golpe seguinte do jogador
  vira legitima defesa (`PvpState._hitMeAt`), sem marca de agressor. Isolado o passo dava 19/0.
  Corrigido: o passo limpa `_hitMeAt`, e o check novo `agressor/revidar-e-defesa` testa a propria M3.
- **ward-territorio/token-nao-cai-como-carga:** conferia a string `PvpCargoKeep`, mas a M6 e por codigo
  (`ClonedItems.IsNativeItem`). Corrigido na sessao do Dead Token.
- **tokens/moeda-deitada:** a moeda cai deitada, as vezes de face para baixo (aleatorio). Corrigido na
  sessao do Dead Token (vale os dois lados).
- **bounty/pausada-no-proprio-ward** (so com `-Steps` sem territorio): a bounty usava o ward do passo
  territorio. Corrigido: cria o proprio quando ele nao existe.

## Falha da rodada 7.5.0

- **tokens/antigo-do-chao-vira-dead-token** com `dead=6` (a conta certa): o token antigo nascia a 1 m do
  personagem e a coleta automatica as vezes o pegava antes do `Pickup` do teste. Corrigido com
  `m_autoPickup = false`, como as moedas da foto.
- Rodando o passo tokens sozinho, `moedas-desenhadas` falhou porque a camera recem-nascida fica na
  horizontal e o piso das moedas saia cortado (na rodada completa a borda do piso ainda entrava no
  quadro). Corrigido: o personagem olha 30 graus para baixo antes da foto.

## Outras coisas que apareceram

- Numa rodada o cliente fechou no inicio do passo montaria com `Game - OnApplicationQuit` e nenhum erro
  antes: pedido de fechamento de fora (outras sessoes tambem rodavam o Valheim no PC). Na rodada
  seguinte o passo passou.
- **Dois clientes:** o servidor recusa o segundo com `Peer ... has invalid session ticket` (o cliente
  mostra `ErrorBanned`): a Steam nao aceita dois tickets da mesma conta no mesmo servidor. O teste A/B
  da arena (outra sessao) bateu no mesmo. Precisa de uma segunda conta Steam.
- `run-pvp-test.ps1` agora acha o cliente e o servidor nas bibliotecas da Steam (e em
  `D:\ValheimDedicatedServer`) e para se as versoes forem diferentes. Antes, com o servidor 0.221.12 e o
  cliente 1.0.16, o cliente ficava no menu ate o tempo limite.

## Fotos

- `tokens-chao.png`: Dead Token, os tres tokens antigos e a Garantia de Refino num piso de madeira.
- `tokens-inventario.png`: os tokens antigos ja convertidos em pilhas de Dead Token no inventario.

## Arquivos

- `solo-LogOutput.log`: BepInEx do cliente na rodada completa da 7.5.0 (303/1).
- `solo-server-unity.log`: log do servidor dedicado na mesma rodada.
