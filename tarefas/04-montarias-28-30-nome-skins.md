> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **montarias** (em D:\deadheim\). Release: **R2 Montarias**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Montarias 28 (nome) e 30 (skins) — estado do código existente

**O que já existe sem commit** (leia antes de mexer; não sobrescrever): `MountSettings.cs` (+74 linhas: `SkinList`, `SkinOptions()`, `TryGetSkinColor`, configs `NomeMontariaAtivo/SoVip/Tamanho`, `SkinMontariaAtivo/SoVip`), `Integration/VipProvider.cs` (VIP por reflexão no `VipList`, servidor valida cada pedido: `IsVip(sender)`), `Riding/RiderServer.cs`/`RiderNet.cs` (`HandleSetCosmetic(sender, argument, isSkin)`, `Apply(rank, mounts, names, skins)`), `Prefabs/MountPrefab.cs` e `MountVisuals.cs` (aplicam tinta). `MountClipPlayer.cs` tem alterações de outra sessão (não tocar).

**Falta (a confirmar lendo o diff completo, que não li inteiro):** (a) menu para escolher nome/skin (`MountMenu.cs` não aparece modificado); (b) persistência por jogador (onde o nome/skin escolhido fica salvo no servidor); (c) mostrar o nome em cima da montaria (`Patches/MountHudPatch.cs`); (d) comando de chat opcional; (e) ver se o nome já passa pelo sanitizador (sem `<`/`>`).

**Checagem de VIP.** Servidor, a cada pedido (já projetado assim). **PvP.** Nome com limite de 20 caracteres e sanitizado; a skin só muda a cor (mesmos atributos); skin não pode tornar a montaria invisível ou confundi-la com um mob hostil (limitar a paleta de tintas e nunca alfa < 1).

**Teste.** VIP muda nome (aparece em outro cliente), skin (idem), não-VIP é recusado; relogar mantém; trocar de montaria preserva por montaria. **Esforço:** 0,5 dia cada, para revisar, compilar e fechar. Publicar como Montarias 0.3.x. **Atenção:** não commitar o trabalho da outra sessão sem acordo; falar com ela antes (`local_257e8c66` no plano).
