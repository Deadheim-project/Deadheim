## 1.6 Travadas ao abrir o TAB / menu de pets
- Deadheim: PR https://github.com/Deadheim-project/Deadheim/pull/4 (branch feat/fase1-travada-tab-pets): cache de tipo/métodos por reflexão na Central (TemGuilds varria assemblies a cada 0,5 s).
- montarias: `Pets/PetMenu.cs` — `Update` consulta o estado a cada 0,25 s; `Redraw(bool force)` só recria lista/detalhe se `ComputeSignature` (foco, pet ativo, estado de cada pet, BuffsEnabled) mudou. Clique de linha usa `Redraw(true)`; `OpenInternal` zera `_signature`.
- PR montarias: https://github.com/Deadheim-project/Montarias/pull/1 (branch feat/fase1-travada-tab-pets, base main).
- Repo GitHub do montarias é `Deadheim-project/Montarias` (M maiúsculo) para `gh --repo`.
- Armadilha: `Destroy` é adiado ao fim do frame; recriar a UI a cada tick faz o clique cair em botão já destruído.

## Fase 2 — Cooldowns como buffs
- Deadheim: PR https://github.com/Deadheim-project/Deadheim/pull/5 (branch feat/fase2-cooldowns-buffs, base feat/fase1-travada-tab-pets).
- `Pvp/PvpModule.cs`: enum `Cooldown {Hearthstone, Retreat1, Retreat2}`; `Remaining(kind)`, `TotalSeconds(kind)`, `Label(kind)`, `MarkRetreatUsed(player, kind=Retreat1)`, `RetreatRefusal(player, kind=Retreat1)` ("<Label> em recarga: faltam X"). Relógio monotônico `_readyAt[3]`.
- Persistência: customData `dh_cdLeft_hearthstone|retreat1|retreat2` (segundos que faltam); `LoadCooldowns` (OnSpawned) migra `dh_retreatLeft`/`dh_retreatAt` para o Retreat 1; `SaveCooldowns` no Player.Save e Logout.
- Config (`PvpConfig`, seção "PvP - Retreat"): `HearthstoneCooldownSeconds`, `Retreat1CooldownSeconds`, `Retreat2CooldownSeconds` (1800). `RetreatCooldownMinutes` foi removida (valor antigo do cfg é ignorado).
- `Pvp/PvpHud.cs`: buffs `DH_CdHearthstone/DH_CdRetreat1/DH_CdRetreat2` (SE_Stats registrados em `All`), ligados em `UpdateBuffs` a cada 0,25 s; reaparecem no login sozinhos. Ícones: Hearthstone / Ruby / Amber (fallback Hearthstone); não verificados no jogo.
- Usos: `Retreat.TentarRetreat` (ponto 2 -> Retreat2), `PvpPatches.HearthstoneCombatPatch` (Hearthstone), `MenuHub` (detalhe do botão), `AesirJanela` (texto de benefício usa Retreat1).
- Armadilha: `dotnet build` precisa de `Deadheim.csproj` (há 3 csproj na pasta). PvpModule.cs/PvpConfig.cs são LF, o resto CRLF.

## Fase 3 — Binds e cama
- Deadheim: PR https://github.com/Deadheim-project/Deadheim/pull/6 (branch feat/fase3-binds-cama, base feat/fase2-cooldowns-buffs).
- `BedBindMenu.cs` (novo, no csproj): janela IMGUI aberta por `Plugin.Update` quando a tecla `Retreat.TeclaDoPonto` é apertada olhando a própria cama; 3 linhas (Hearthstone/Retreat 1/Retreat 2) com estado e botão; grava via `Retreat.SetHearthStonePosition(ponto)` e avisa "<nome> definido nesta cama". Fecha com ESC ou a >8 m da cama.
- `Retreat.GetHearthStonePosition/SetHearthStonePosition(ponto)`: 0 = Hearthstone (customData `positionXh/Yh/Zh`, cai no ponto 1 se ausente), 1 = `positionX..`, 2 = `positionX2..`. `Retreat.Estado(ponto)` = "definido"/"não" para o hover.
- Hover: `Retreat.Bed_GetHoverText_Patch` (uma linha com os 3 estados + dicas das teclas de retorno). Hearthstone/ (mod separado) agora, com `Detalhes.Deadheim` carregado, não grava ao apertar a tecla nem escreve hover; `Hearthstone.GetHearthStonePosition` lê o ponto `h` primeiro.
- Shift+tecla (segundo ponto) foi removido; o menu substitui. Retreat 1/2 exigem VIP (Retreat 2 também `Aesir18DoisPontos`).
- Build: `dotnet build Deadheim.csproj` e `Hearthstone.csproj` ok. Não testado no jogo. Plugin.cs é LF, Retreat.cs/Hearthstone CRLF.

## Fase 4 — Pets: tecla de Spawn/Despawn
- O menu de pets é do repo montarias (não do Deadheim). PR https://github.com/Deadheim-project/Montarias/pull/2 (branch feat/fase4-tecla-pet, base feat/fase1-travada-tab-pets / PR #1).
- A tecla já existia: `PetSettings.QuickKey` (cfg cliente `[Pets] AtalhoPet`, vazio por padrão), disparada em `Plugin.Update` -> `PetBridge.ToggleQuick` -> `Deadheim.Pets.ToggleQuick` (Deadheim/Pets.cs: guarda se ativo, senão invoca o último/primeiro desbloqueado).
- `Pets/PetMenu.cs`: botão "Tecla: <nome>" no canto inferior esquerdo do detalhe; `_capturing` + `CaptureKey()` (Esc cancela, Backspace/Delete desliga, Shift/Ctrl/Alt viram modificadores); `PetMenu.Capturing` bloqueia o atalho em Plugin.Update. `PetSettings.Save()` grava o cfg na hora. `KeyName()` entra no `ComputeSignature`.
- Combate/arena: nada extra; o pet já fica oculto em arena/raid no Deadheim.Pets e não há trava de combate para invocar.
- Armadilhas: PetMenu.cs/PetSettings.cs são LF, Plugin.cs CRLF. Build: `dotnet build ValheimMontarias.csproj`. Não testado no jogo.

## Fase 5 — Recompensas da arena balanceada
- npc-valheim (remote `Deadheim-project/npcs`): PR https://github.com/Deadheim-project/npcs/pull/6 (branch feat/fase5-recompensas-balanceada, base feat/fase1-aesir-fila-tab / PR #5).
- A "vitória paga moedas" da normal é `ArenaEngine.PayMatchPoints` (Pontos de Arena, cfg `[Arena - Pontos] PorPartida`, teto diário). Balanceada agora desvia para `PayBalancedRewards` (mesma regra: só quem terminou dentro, empate/W.O. pagam derrota; sem teto diário) e NÃO paga Pontos de Arena.
- Tabela: cfg `[Arena - Balanceada] Recompensas` = `1v1=Coins:150/Coins:30|2v2=...|ffa=...` (vitória/derrota, cada lado item:qtd). `ArenaSettingsParser.ParseBalancedRewards/FormatBalancedRewards/TryReward`, struct `ArenaReward`, `ArenaTuning.BalancedRewards`, `ArenaConfig.BalancedRewards` (validação de item via `ItemSpawner.MaxDeliverableAmount` em `Build` com checkWorld).
- Entrega: `_host.MailItem(playerId, assunto, item, qtd)` (igual ao desafio). Edição: ação `ArenaWire.ActAdminBalancedRewardsSet` (`ArenaServer.HandleCfgEdit`, valida item/qtd) gravando o ConfigEntry; sincroniza pelo cfg.
- UI: `UI/ArenaSetupView.DrawBalanced` (bloco "Recompensas por partida"), campos `_rewards[4,4]`, `RewardsMatch/SaveRewards`, sincronização no refresh junto com `_seenPoints`.
- Armadilha: ArenaTuning.cs/ArenaWire.cs são CRLF, o resto LF. `dotnet build` em NpcValheim/ . Não testado no jogo.
