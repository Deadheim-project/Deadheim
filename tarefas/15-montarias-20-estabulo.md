> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **montarias** (em D:\deadheim\). Release: **depois de 04**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Montarias 20: estábulo

**Abordagem.** Uma construção "estábulo" (peça) onde o jogador guarda e chama suas montarias (hoje existe `MountHub`/apito, `WhistleItem`). Estábulo VIP: guarda mais montarias e permite trocar a ativa de qualquer lugar da base, **sem teleportar a montaria**.

**Ganchos.** `Prefabs/MountHub.cs` e `MountRoster.cs` já organizam as montarias do jogador. O estábulo seria uma `Piece` com um `Interactable` que abre o menu `MountMenu`. Não precisa de Harmony novo: registrar a peça no martelo e ligar ao `MountMenu`.

**Posse.** Servidor: quantidade máxima de montarias guardadas por jogador (`MountRoster`), VIP maior (config). **PvP.** O estábulo não protege nem esconde montaria; chamar a montaria respeita `CombatLock.cs` (já existe: trava em combate). Sem voo a partir do estábulo.

**Dependência.** O que o estábulo faz além de "menu" precisa ser definido: o apito e o menu já listam as montarias. **Pergunta ao usuário:** o que o estábulo adiciona que o apito não dá? Sem isso, o item é uma peça decorativa. Proposta: guarda de montarias extras (slots VIP) + visual da montaria no estábulo. **Esforço:** 2 dias, **depois** de 28/30 fechados. Arte para a peça (pode reusar uma casa de madeira vanilla recolorida).

---
## Resultado (2026-10-19, agente Montarias 20)
**Status: ADIADO por decisão do usuário. Nenhum código alterado; sem build; repositório montarias limpo.**
- Motivo: a pergunta do briefing ficou sem resposta de design. O menu (tecla U, `MountMenu`) já permite selecionar, montar/desmontar e "Pegar o item" (reemitir o apito) de qualquer lugar; a posse é um registro do servidor (`RiderServer.Record.Mounts`) e não existe limite de montarias carregadas. Um estábulo que só abre o menu seria peça decorativa.
- Restrição achada: qualquer limite de "slots" para não-VIP reduziria o que o jogador já tem hoje, contra a regra "não-VIP nunca abaixo do vanilla". Um desenho de slots precisa de um conceito novo (ex.: montarias "guardadas" vs "levadas") definido antes.
- O mod não tem nenhuma `Piece` nem Jotunn hoje; registrar peça no martelo (clone de peça vanilla + `PieceTable` do Hammer + componente `Interactable`) é infraestrutura nova e sem teste no jogo.
- Para retomar: o usuário define a função do estábulo (opções: slots VIP de guarda + peça; ou só peça que abre o menu). Pontos de encaixe: `RiderServer.Record` (novo campo), `RiderNet.SendState`/`RiderClient.Apply` (estado), `CombatLock.Block`, `VipProvider.IsVip(sender)`.
