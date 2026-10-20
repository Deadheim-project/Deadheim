# Status das tarefas (19/10/2026)

Nada abaixo foi testado no jogo, salvo onde indicado.

## No ar (Deadheim 7.7.3, NpcValheim 0.1.74, Montarias 0.3.24, Velas 0.5.2)
Treinador, Mercadores de bioma, passe por Deadcoins, Garantia de Refino, fichas, títulos, `/batizar`, Aesir fáceis, `VipOnlySails`, anúncios 100 (22), anexos por carta (23), nome e skin de montaria (28, 30), pet cosmético (36), animais e fogueiras puxam do baú (10).

## Validação do código (19/10)
| Item | Veredito |
|---|---|
| 22 anúncios | Limite checado no servidor (`RPC_Sell` usa `SenderIsVip`); teto do quadro 400 protege não-VIP. OK |
| 23 anexos | Limite checado no servidor. **Risco herdado:** o cliente remove o item e o servidor confia (mesmo padrão do mercado); cliente modificado cria item. Anexo para casa só pela UI bloqueada; no servidor duplicaria por membro |
| 28/30 montaria | VIP checado no servidor via `VipListApi.IsVip(conta)`, nome sanitizado. OK |
| 36 pet | Corrigido em 7.7.1 a 7.7.3 (`/pet` como ConsoleCommand, remoção de `CharacterDrop`, escala, seguir, zona arena). Falta confirmar no jogo que os 6 aparecem |
| 10 puxar do baú | Compila; acessos privados trocados por reflexão. Sem teste |
| 01 Barbeiro | **Desligado** (`Barber.Enabled=false`). Bloqueios abertos: BarberNet não registrado no cliente, ficha cobrada duas vezes, cor do cabelo sem validar, aplicação feita no servidor, distância, persistência |

## Falta fazer
1. **Teste no jogo** (nada foi aberto ainda): pet (6 modelos, troca, resgate de Ficha, nome), baú+fogueira, baú+animal, anúncios acima de 50, carta com 2 e 3 stacks, nome e skin de montaria, não-VIP no vanilla.
2. **Barbeiro:** corrigir os 6 bloqueios, colocar o NPC no servidor, voltar a Ficha à loja.
3. **Pet restante:** nome sobre o pet (conferir), ocultar em partida de arena e raid (conferir), mensagem de erro do resgate, teste com 2 clientes; `arena` fora de `PetHiddenZones` só enquanto a arena de teste existir.
4. **Documentos:** tirar Halloween do guia e do catálogo; marcar como "No ar" os itens 10, 22, 23, 28, 30 e 36; reexportar PNGs.
5. **Valores de teste no servidor** (remover antes da abertura): `TesteSkills100`, `LiberarTodasGratis`, `ImmunityMinutes 0.3`, `CombatTagSeconds 1`, `KillerMaxDistance 100`, arena de teste, Coal no Intendente, `DamagePercent 0`, `FuelItem GreydwarfEye`, `WardLimit 3/5`.
6. **NPCs de bioma já colocados:** editar no jogo (templates não sobrescrevem).
7. **Aesir 23:** mover o débito do anexo para o servidor (fecha o risco de item criado).

## Não iniciado
06 chama (29), 07 abate (31), 08 lápide (33), 11 craft com baús (13), 12 vaga reservada (19), 13 comemoração na arena (34), 14 passe de temporada, 15 estábulo (20), 16 peças decorativas (32), 17 plantas salvas (24), 18 transmog (35), 19 marcadores (21, a definir). Halloween cortado.

## Sem código (administrador)
Itens 37 a 40, arte da vela e do brasão de guilda, datas da Ficha do Fundador, cadastro manual de VIP/Deadcoins, abertura (sem data).
