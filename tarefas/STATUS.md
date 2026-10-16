# Status das tarefas (16/10/2026)

Verificado lendo os repositórios e compilando. Nada abaixo foi testado no jogo. Nenhuma tarefa foi commitada (os briefings mandavam não commitar).

## No ar (7.6.0, NpcValheim 0.1.73, Velas 0.5.2)
Treinador, Mercadores de bioma em moedas e Deadcoins, passe só por Deadcoins, Garantia de Refino, Mestre das Montarias nas duas moedas, fichas, títulos [Aesir] e [Fundador], `/batizar`, Aesir fáceis, `VipOnlySails`.

## Feito pelos outros agentes, só no working tree (sem commit)

| Tarefa | Repositório | Situação | Evidência |
|---|---|---|---|
| 01 Barbeiro | npc-valheim | Código escrito, não validado | `Npc/Barber.cs`, `BarberNpc.cs`, `UI/BarberView.cs`; configs `BarberEnabled/TokenPrefab/...` em `Plugin.cs`; `NpcPrefabFactory.cs` alterado |
| 02 Aesir 22 anúncios | npc-valheim | Código escrito, não validado | `VipMaxListings` (100) em `Plugin.cs`; `MarketDatabase`, `MarketplaceNpc`, `MarketView` alterados |
| 03 Aesir 23 anexos | npc-valheim | Código escrito, não validado | `VipMaxAttachments` (3); `MaxAttachmentsNonVip = 1`; `MailDatabase`, `MailboxNpc`, `MailView` alterados |
| 04 Montarias 28 e 30 | montarias | Código escrito, não validado | `MountNameActive/VipOnly/MaxLength`, `SkinList`, `Integration/VipProvider.cs`; 9 arquivos alterados |
| 05 Pet 36 (fechamento) | Deadheim | Parcial | `Pets.cs` novo e corrigido (itens 1 a 10 da validação); falta Animator real, nome sobre o pet, ocultar em raid e battleground, Ficha de Pet na loja, teste com 2 clientes |

Os três repositórios compilam com 0 erros. As tarefas 01, 02 e 03 estão misturadas nos mesmos arquivos do npc-valheim (`Plugin.cs`, `ServiceNpcAuthority.cs`), então ficam num commit só.

## Não iniciado
06 chama 29, 07 abate 31, 08 lápide 33, 09 Halloween, 10 puxar do baú, 11 craft com baús, 12 vaga reservada, 13 comemoração na arena, 14 passe de temporada, 15 estábulo, 16 peças decorativas, 17 plantas salvas, 18 transmog, 19 marcadores (21). Halloween foi cortado por decisão do usuário (16/10).

## Pendências que bloqueiam release
1. **Loja Deadcoins no servidor vende fichas sem uso** (padrão do código): FichaBarbeiro (até o NPC entrar), FichaTemporada, FichaHalloween (janela 24/10 a 07/11). Tirar do `Items` do cfg e de `DefaultItems`.
2. **Pet:** Animator do catálogo usa `Speed`/`Sit`, que não existem (o jogo usa `forward_speed`, `sleeping`); `Raven` não existe (usar `Crow`).
3. **Revisar e testar** 01 a 04 antes de qualquer release: Barbeiro (persistência de cabelo e cor), limite de anúncios por quadro, anexos por carta, nome e skin de montaria.
4. **Documentos:** tirar Halloween do guia e do catálogo; marcar Barbeiro/anúncios/anexos/nome/skin como "Pronto, chega no próximo update" quando validados.

## Releases sugeridos
- R1 NpcValheim 0.1.74: 01, 02, 03 (tirar as fichas sem uso da loja junto).
- R2 Montarias 0.3.24: 04.
- R3 Deadheim 7.7.0: pet fechado (05).
Cada um vira um deploy só, com `MinimumRequiredVersion` e Whitelist trocadas.
