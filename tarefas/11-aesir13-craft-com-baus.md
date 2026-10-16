> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **Deadheim** (em D:\deadheim\). Release: **R5 Deadheim (depois do 10)**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Aesir 13: fabricar usando os baús próximos

**Abordagem.** O VIP pode fabricar/construir usando o conteúdo de baús ao redor da bancada, em vez de só o inventário. Já existe o padrão de `ChestSnap`/mods similares (conferir o manifest do launcher: se algum opcional já fizer isso de graça, **não vender**; é o primeiro passo deste capítulo).

**Ganchos.** `Player.HaveRequirements`, `Player.ConsumeResources`, `Inventory.CountItems`/`HaveItem` quando chamados a partir de `InventoryGui.DoCrafting` e `Player.PlacePiece`. Estratégia segura: postfix em `Player.HaveRequirements` e `Player.ConsumeResources` que, se o jogador é VIP e está perto de uma bancada, soma/consome nos `Container` do raio (mesma rotina do item 10). Já existe `CraftPatches.cs` (item 11); somar lá.

**Autoridade.** O consumo de itens de baús é uma mudança do mundo: o servidor precisa conhecer o resultado. Como o `Container` é um objeto com dono, o cliente que fabrica pede posse (`ZNetView.ClaimOwnership`) como já faz ao abrir um baú. Só baús onde `Container.CheckAccess(playerId)` é verdadeiro e `PrivateArea.CheckAccess` passa. **Não consumir se o baú está aberto por outro jogador** (`m_inUse`).

**PvP.** Em zona de raid, baú de base inimiga nunca conta (CheckAccess cobre). Raio pequeno (5 m da bancada) para não virar "craft de longe".

**Config** (`[AesirCraft]`): `Enabled`, `Radius=5`, `VipOnly=true`. **Riscos:** consumo duplicado; item tirado de baú sem sincronizar; compatibilidade com `CraftPatches` (lote 99). **Teste:** receita com material parte no inventário, parte no baú; baú de outro jogador (não conta); não-VIP no vanilla. **Esforço:** 1,5 dia (depois do 10).
