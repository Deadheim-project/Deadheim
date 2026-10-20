> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **npc-valheim** (em D:\deadheim\). Release: **R1 NpcValheim**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Barbeiro (NPC novo, gasta FichaBarbeiro)

**Abordagem.** Um NPC de serviço no NpcValheim, no molde do Treinador (`Npc/TrainerNpc.cs`, `ServiceNpcAuthority`, `NpcRequestGuard`). O jogador fala com o NPC, abre uma tela com cabelo, barba, cor do cabelo e (opcional) cor da pele, vê a prévia no próprio personagem e confirma. A confirmação gasta 1 `FichaBarbeiro` por visita (todas as mudanças de uma vez).

**Ganchos do jogo.** Nada de Harmony. A aparência do jogador é `VisEquipment`: `SetHairItem(string)`, `SetBeardItem(string)`, `SetHairColor(Vector3)`, `SetSkinColor(Vector3)`, acessíveis por `Player.m_localPlayer.m_visEquipment`. A lista de cabelos e barbas vem do `ObjectDB` (itens com nome `Hair*` e `Beard*`; confirmar a lista exata no assembly antes, porque nomes e quantidade variam). Prévia = chamar o setter e voltar ao valor antigo se cancelar.

**Persistência (spike obrigatório de 1 hora).** O ponto de risco é saber se o cabelo trocado fora da criação de personagem é salvo. A aparência é gravada junto do personagem em `PlayerProfile` (via `Player.Save`). Teste: trocar o cabelo, deslogar, logar. Se não persistir, aplicar a escolha guardada no `m_customData` do jogador a cada spawn (mesmo padrão que o pet usa com `dh_pet`).

**Onde fica a autoridade.** A ficha é um item do inventário do cliente, então o consumo é do cliente (mesmo padrão da `FichaFundador`/`FichaPet`). O efeito é só visual e de baixo valor, então uma fraude (cliente modificado que muda o cabelo sem ficha) custa apenas cosmético. Para reduzir: o cliente só aplica depois de o servidor responder "OK" a um RPC `Barber_Request` (valida distância ao NPC e `NpcRequestGuard` contra repetição), e o servidor registra a transação no log. Sem checagem de VIP: é venda avulsa.

**PvP.** Cabelo e cor de pele podem ser usados para imitar outro jogador (disfarce). Regra: cor de pele limitada a uma paleta fixa de tons naturais (config), nada de verde/azul fosforescente, e o nome sobre a cabeça continua o real. Sem mudar nome, sem mudar porte.

**Config** (`[Barber]`): `Enabled`, `TokenPrefab=FichaBarbeiro`, `SkinColors` (paleta), `HairColors` (paleta ou livre), `AllowSkin=false` por padrão.

**Arquivos.** NpcValheim: `Npc/BarberNpc.cs` (novo), `UI/BarberWindow.cs` (novo), registrar o NPC em `NpcSpawnerStub`/modelo igual ao do Treinador; `Content` para o modelo e o diálogo. Deadheim: nada, a ficha já existe em `ClonedItems.cs`.

**Riscos.** Persistência (spike). Cabelo de gênero oposto/modelo do personagem (`m_modelIndex`) fica fora. O NPC não sobrescreve NPCs já postos no servidor (lembrete do admin).

**Teste.** (1) Trocar com ficha: aplica, ficha some. (2) Cancelar: volta ao antigo, ficha fica. (3) Sem ficha: bloqueado. (4) Relogar: persiste. (5) Segundo cliente vê o novo cabelo. (6) Spam de clique: cobra uma vez.

**Esforço:** 2 dias (a UI toma metade). **Antes de pronto, tirar a FichaBarbeiro da loja** (já está em `DefaultItems`).

---

## Resultado (2026-10-19, sessão blissful-mendel, sem commit)

Compila (0 erros). Arquivos: `NpcValheim/Npc/Barber.cs`, `Npc/BarberNpc.cs`, `UI/BarberView.cs`, `UI/NpcViewBase.cs` (`Destroy` virtual), `Assets/Icons/barber.png` (cópia provisória do trainer.png), `README.md` (seção "Barbeiro", com como ligar).
- Corrigido: (1) `Barber.Register` agora chama `BarberNet.Register` (o RPC de resposta existe nos dois lados; só aceita remetente autoritativo). (2) Ficha cobrada uma vez: o servidor emite autorização de uso único (20 s, uma por jogador) e o cliente dono remove 1 ficha só ao receber o "ok" que esperava; botão travado enquanto espera. O servidor dedicado não vê o inventário, então não confere a ficha. (3) Cor do cabelo/pele validada contra `HairColors`/`SkinColors` (paleta `r,g,b|...`, vazia = livre 0..1; a UI encaixa na cor da paleta). (4) Servidor não aplica nada; o cliente grava. (5) `NpcRequestGuard.AllowNearby` (6 m, limite de frequência). (6) Spike: lendo o assembly, o visual fica em `Humanoid.m_hairItem/m_beardItem` e `Player.m_hairColor/m_skinColor`, gravados por `Player.Save` e recopiados ao `VisEquipment` por `SetupVisEquipment`; a versão antiga mexia só no `VisEquipment` (desfeito no quadro seguinte, não salvo). Agora usa `Player.SetHair/SetBeard/SetHairColor/SetSkinColor`.
- NPC criável: já estava registrado em `NpcPrefabFactory` e em `AllowedTargets`; faltava o ícone.
- Continua DESLIGADO (`[Barber] Enabled=false`). `FichaBarbeiro` continua FORA da loja.
- Sem teste no jogo: tudo (aplicar, cancelar, sem ficha, relogar, 2º cliente, spam, distância, paleta). Em especial a persistência após relogar e a posição dos sliders/dropdown da UI (não foi vista em tela). Bug de cultura (vírgula decimal) tratado com InvariantCulture.
