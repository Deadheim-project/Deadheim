> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **Deadheim** (em D:\deadheim\). Release: **R4 Deadheim (Halloween)**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Aesir 33: lápide personalizada

**Abordagem.** A lápide (`Tombstone`) que o jogador deixa ao morrer ganha um visual escolhido (cruz, caveira de Halloween) e uma frase curta opcional ("Fulano, morto por ..."). **A lápide continua funcionando igual** (mesmo inventário, mesma recuperação, mesmo prazo).

**Ganchos.** `Player.CreateTombStone` (cria o prefab `Player_tombstone` e o preenche) e `TombStone.Setup(name, ownerUID)`. Postfix em `TombStone.Setup`/`Awake` no cliente que enxerga: ler o cosmético do **dono** (UID) e trocar o visual (material/modelo de filho) ou instanciar um filho decorativo. Para o jogador que morreu, `dh_lapide` vai no `ZDO` da própria lápide, escrito pelo cliente dono no momento da criação (o servidor valida `CosmeticosApi.Has(playerId, "lapide:<id>")` num RPC antes, ou revalida no ato de ler).

**Verificação.** Cliente lê `dh_lapide` do ZDO da lápide; para impedir cliente modificado de se dar "caveira" sem ter, o servidor confere ao receber o ZDO novo (`ZDOMan`/ao criar) e apaga a chave se o dono não tem o cosmético. Mais simples: aceitar o risco (puramente visual), mas o `Pets` fecha melhor com tabela do servidor; usar o mesmo padrão da seção 36-D.

**PvP.** A frase não pode mostrar posição, nem insultar: sanear como `SanitizeName` (sem `<`, `>`, controle, 40 caracteres). Não altera colisão nem loot: não pode servir de armadilha ou de esconderijo.

**Config** (`[Lapide]`): `Enabled`, `MaxTextLength=40`. **Escolha:** `/lapide <id>|off|texto <txt>`. **Arquivos:** novo `Lapide.cs`; chaves `lapide:cruz`, `lapide:caveira`. **Riscos:** variar o visual sem quebrar `TombStone.m_floater`/colisor; cliente que não tem o mod (todos têm, é o mod principal). **Teste:** morrer com e sem cosmético; recuperar itens normal; outro cliente vê o visual. **Esforço:** 1,5 dia.
