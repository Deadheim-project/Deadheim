> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **montarias** (em D:\deadheim\). Release: **depois de 04**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Montarias 20: estábulo

**Abordagem.** Uma construção "estábulo" (peça) onde o jogador guarda e chama suas montarias (hoje existe `MountHub`/apito, `WhistleItem`). Estábulo VIP: guarda mais montarias e permite trocar a ativa de qualquer lugar da base, **sem teleportar a montaria**.

**Ganchos.** `Prefabs/MountHub.cs` e `MountRoster.cs` já organizam as montarias do jogador. O estábulo seria uma `Piece` com um `Interactable` que abre o menu `MountMenu`. Não precisa de Harmony novo: registrar a peça no martelo e ligar ao `MountMenu`.

**Posse.** Servidor: quantidade máxima de montarias guardadas por jogador (`MountRoster`), VIP maior (config). **PvP.** O estábulo não protege nem esconde montaria; chamar a montaria respeita `CombatLock.cs` (já existe: trava em combate). Sem voo a partir do estábulo.

**Dependência.** O que o estábulo faz além de "menu" precisa ser definido: o apito e o menu já listam as montarias. **Pergunta ao usuário:** o que o estábulo adiciona que o apito não dá? Sem isso, o item é uma peça decorativa. Proposta: guarda de montarias extras (slots VIP) + visual da montaria no estábulo. **Esforço:** 2 dias, **depois** de 28/30 fechados. Arte para a peça (pode reusar uma casa de madeira vanilla recolorida).
