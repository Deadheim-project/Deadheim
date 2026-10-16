> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **Deadheim** (em D:\deadheim\). Release: **depende de arte**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Aesir 32: peças decorativas exclusivas

**Abordagem.** Peças de construção só cosméticas (estátuas, bandeira do Deadheim, vaso, tapete), liberadas por VIP/ficha. **Sem função**: sem estrutura que bloqueie raid, sem proteção, sem abrigo (`Piece.m_comfort` zero, sem `WearNTear` com força alta), sem baú. Isso evita que "decoração" vire parede.

**Ganchos.** Registrar peças novas no `PieceTable` do Hammer pelo mesmo caminho que `ClonedItems.cs` usa para itens (Jotunn). Limitar a construção por `Player.PlacePiece`/`Player.CheckCanRemovePiece`: postfix em `Player.TryPlacePiece` (servidor via RPC não existe; o cliente decide), então a **trava de VIP é no cliente e no servidor**: no servidor, `ZNetScene.CreateObject`/`ZDOMan` aceita qualquer prefab. Para impedir cliente modificado de instanciar, adicionar checagem no servidor em `WearNTear.Awake`/`Piece.Awake` da peça cosmética: se `Piece.m_creator` não é VIP/dono, destruir (`ZNetScene.Destroy`) e logar.

**PvP.** Peça com `m_noClipping`/colisor leve; não pode ser colocada dentro de wards alheias (o jogo já bloqueia por ward). Não conta como parede para o raid (sem `WearNTear` ou com vida 1). Teto de quantidade por jogador (config).

**Arte.** Esta é a dependência: modelos e texturas feitos à mão (assetbundle). Primeira versão com peças existentes do jogo recoloridas (mesh vanilla duplicado), ou adiar até ter arte.

**Config:** `[Decor] Enabled`, `MaxPerPlayer=50`, `Prefabs`. **Arquivos:** novo `DecorPieces.cs`, assetbundle em `Assets`. **Riscos:** arte; peça cosmética quebrando regra de raid; save com prefab desconhecido se o mod sair. **Teste:** VIP constrói; não-VIP não vê no martelo; cliente adulterado: peça some; construir sobre ward alheia bloqueia. **Esforço:** 2 dias + arte. Fica atrás dos outros.
