> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **Deadheim** (em D:\deadheim\). Release: **release proprio**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Transmog (35)

**Abordagem.** O VIP escolhe a **aparência** de um item (capa, capuz, armadura, arma) mantendo o status real do item de verdade. Só visual: o item equipado continua sendo o que dá defesa/dano.

**Ganchos.** `VisEquipment.SetChestItem/SetLegItem/SetHelmetItem/SetShoulderItem/SetUtilityItem/SetRightItem/SetLeftItem` recebem o **nome do prefab** a mostrar. A troca é um prefixo em `VisEquipment.SetChestItem` etc. (e `Humanoid.SetupVisEquipment`) que substitui o nome do item **apenas para efeito de visual**, a partir de um mapa `item real → aparência` do jogador. O mapa (`dh_transmog`) vai no ZDO do jogador para os outros clientes aplicarem o mesmo prefixo ao desenhar esse jogador.

**Autoridade.** Posse da aparência: o jogador precisa ter **o item de aparência** ou tê-lo desbloqueado (decisão de design: desbloqueia ao ter possuído o item uma vez? ou ao gastar uma ficha?). **Recomendo:** a aparência só vale entre itens da mesma categoria (peito→peito) e só de itens que o jogador já **carregou alguma vez** (registro no servidor, `transmog-desbloqueados`), para não virar "mostrar armadura de chefe que nem matou". Servidor valida no RPC de escolha.

**PvP (o ponto mais delicado).** Transmog **não pode esconder informação de combate**: não dá para trocar o visual de uma armadura pesada por uma roupa de tecido para o inimigo julgar errado a defesa? É informação visual, não mecânica, mas **a regra do projeto proíbe vantagem de combate**; disfarçar-se pode ser vantagem (ex.: parecer um NPC/guarda, ou parecer sem armadura). Limites: (1) só categorias de armadura equivalente de peso (não finge armadura leve de pesada? decisão sua); (2) nunca aparência de itens de NPC/chefe/guarda; (3) nome sobre a cabeça e marcador de guilda/PvP continuam; (4) em arena e raid o transmog é desligado (volta ao visual real). **Decisão do usuário:** nível de liberdade (só mesma classe de peso ou livre).

**Config** (`[Transmog]`): `Enabled`, `Whitelist`, `DisabledZones=arena,raid`. **Arquivos:** novo `Transmog.cs`, UI. **Riscos:** `VisEquipment` aceita o nome do prefab e carrega os filhos de `ObjectDB`; itens com `m_attachOverride`; arma em uso muda o *ataque animado*, então **só aparência de arma da mesma classe** (espada→espada). Sincronização com 2 clientes é o teste principal. **Esforço:** 5 dias. Por último.

---
## Resultado (sessao 2026-10-19, worktree dazzling-roentgen-81fd3b)
**Decisoes do usuario:** liberdade por opcao (`SameWeightClass`, padrao true); v1 so armadura + capa (armas ficam para depois); zona de raid desligavel por config (`DisabledInRaidZone`, padrao true, via `Wards.WardBridge.Governed`).
**Implementado (compila, 0 erros):** `Transmog.cs` novo (prefix em `VisEquipment.Set{Helmet,Chest,Leg,Shoulder}Equipped` troca so o hash de desenho pelo mapa `dh_transmog` da ZDO; servidor guarda desbloqueios/escolhas em `config/Deadheim/transmog-<mundo>.txt`, desbloqueia lendo a ZDO do jogador a cada 5 s, revalida VIP/slot/peso/lista negra em cada `set`; janela IMGUI + `/transmog`, `/transmog <real> <aparencia>`, `/transmog off <real|*>`). Config `[Transmog]`: Enabled, SameWeightClass, DisabledInArena, DisabledInRaidZone, Blacklist, MaxChoices.
**Arquivos mudados:** `Transmog.cs` (novo), `Deadheim.csproj` (Compile + referencia UnityEngine.IMGUIModule), `Plugin.cs` (1 linha: `Transmog.BindConfigs(Config);` apos `Pets.BindConfigs`).
**Sem teste no jogo:** tudo. Principais: 2 clientes vendo a aparencia; cursor/janela IMGUI (patch em `GameCamera.UpdateMouseCapture` e `Player.TakeInput`); capa com variantes; cabelo com capuz trocado; arena/raid voltando ao visual real; VIP expirado; desbloqueio ao equipar no servidor dedicado (Mono/Linux); `ObjectDB` no servidor dedicado.
**Riscos conhecidos:** cliente adulterado pode escrever qualquer `dh_transmog` na propria ZDO (so visual); nome do Blacklist e manual (padrao vazio); armas, utilidade e trinket fora da v1.
