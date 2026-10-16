> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **Deadheim** (em D:\deadheim\). Release: **R4 Deadheim (Halloween)**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Chama colorida 29

**Abordagem.** Tocha, lanterna e fogueira pessoais do VIP queimam de outra cor (roxa no Halloween, azul, verde). Pura cor da luz e das partículas. Hoje "não existe sistema de chamas"; o projeto é construí-lo pequeno.

**Ganchos.** Tocha na mão: `VisEquipment.AttachItem` instancia o prefab do item na mão; o objeto filho tem um `Light` e um `ParticleSystem` (`flame`). Postfix em `VisEquipment.AttachItem` (ou `Humanoid.EquipItem`) que, se o jogador tem `chama:<id>` ativo, ajusta `Light.color` e as cores/`startColor` do `ParticleSystem` do filho. Lanterna idem (`Lantern`). Para a tocha de parede e fogueira (`Fireplace`), fora da v1: só o que o jogador **veste/segura** (menos arriscado, sem mexer em peças do mundo).

**Sincronização.** O visual do item na mão de **outro** jogador é criado em outro cliente. Cada cliente precisa saber a chama do outro: escolher `dh_chama` no ZDO do jogador (como `dh_pet`) e o cliente que renderiza lê. Verificação de posse pelo servidor como no 36-D (tabela de cosméticos transmitida pelo servidor), senão cliente modificado acende qualquer cor (só cosmético, mas fechar de graça aqui é fácil).

**PvP.** Cor não pode aumentar ou diminuir alcance de luz: **não mexer em `Light.range`/`intensity`**, só `color`. Cor também não pode ficar branca/ofuscante. Em PvP noturno uma luz diferente pode denunciar, mas igual à tocha normal.

**Config** (`[Flames]`): `Enabled`, `Palette` (`id=#RRGGBB`). **Escolha:** `/chama <id>|off`. **Arquivos:** novo `Chamas.cs`. **Riscos:** o prefab tem mais de um `ParticleSystem`/`Light`; `MaterialPropertyBlock` vs cópia de material (usar bloco para não vazar para outros jogadores). **Teste:** 2 clientes, um escolhe roxo, o outro vê roxo; tirar e pôr a tocha mantém; sem cosmético: vanilla. **Esforço:** 2 dias.
