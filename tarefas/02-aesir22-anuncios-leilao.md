> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **npc-valheim** (em D:\deadheim\). Release: **R1 NpcValheim**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Aesir 22: mais anúncios no leilão

**Abordagem.** O limite 50 vira `NaoVip=50` e `Vip=100` (config). A checagem está em `MarketDatabase.cs:211` e não sabe quem é VIP. Mudar `Add(...)` para receber o limite já resolvido; quem resolve é o handler do RPC em `MarketplaceNpc` (tem o `sender`), usando `VipShopShortcut.SenderIsVip(sender)`.

**Ganchos.** Nenhum Harmony. **Autoridade:** servidor, já é onde o anúncio é criado. **PvP:** nenhum efeito. **Config** (`[Market]`): `MaxListingsPerPlayer=50`, `VipMaxListings=100`. **Arquivos:** `Persistence/MarketDatabase.cs`, `Npc/MarketplaceNpc.cs`, UI do leilão (mostrar "x/100").

**Riscos.** O teto 500 por quadro (`MaxListingsPerBoard`) continua; se muitos VIPs lotarem o quadro, não-VIP não consegue anunciar. Mitigar: o teto de VIP só vale enquanto o quadro tiver menos de 400. Quem perde o VIP mantém os anúncios, só não cria novos acima de 50.

**Teste.** Não-VIP trava no 51º; VIP chega ao 100º; VIP expirado não cria mais. **Esforço:** 0,5 dia.
