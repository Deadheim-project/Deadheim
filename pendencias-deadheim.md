# Deadheim: o que falta (16/10/2026)

No ar desde 15/10: Deadheim 7.6.0, NpcValheim 0.1.73, Velas 0.5.2.
Já entregue: catálogo da Loja Deadcoins, Mercadores de bioma em moedas ou Deadcoins, passe de chefe só por Deadcoins, Garantia de Refino, Treinador, Mestre das Montarias nas duas moedas, fichas, títulos [Aesir] e [Fundador], `/batizar`, Aesir fáceis (5, 6, 7, 8, 9, 11, 12, 14, 15, 16, 17, 18, 26), `VipOnlySails`, guia do jogador e catálogo.

Código pronto sem commit (aguardando teste e release): Barbeiro (A5), Aesir 22, Aesir 23, Montarias 28 e 30.

## 1. Código ainda não construído

| # | Item | Repositório | Esforço | Observação |
|---|---|---|---|---|
| A5 | Barbeiro (NPC que troca cabelo e barba, gasta a FichaBarbeiro) | npc-valheim | médio | **Código pronto, sem commit** — falta teste no jogo |
| 22 | Mais anúncios no leilão para VIP (50→100) | npc-valheim | fácil | **Código pronto, sem commit** — falta teste no jogo |
| 23 | Mais anexos no correio para VIP | npc-valheim | fácil | **Código pronto, sem commit** — falta teste no jogo |
| 34 | Comemoração de vitória na arena (VIP) | npc-valheim | médio | |
| 10 | Animais e fogueiras puxam do baú | Deadheim | médio | |
| 13 | Fabricar usando os baús próximos | Deadheim | médio | Só baús que o jogador pode abrir |
| 19 | Vaga reservada com o servidor cheio | Deadheim | médio | |
| 31 | Efeito de abate no PvP | Deadheim | médio | Entra no pacote de Halloween |
| 32 | Peças decorativas exclusivas | Deadheim | médio | Precisa de arte |
| 33 | Lápide personalizada | Deadheim | médio | Entra no pacote de Halloween |
| 20 | Estábulo | montarias | médio | Começou: `Integration/VipProvider.cs` e outros arquivos sem commit |
| 28 | Nome visível na montaria | montarias | fácil | **Código pronto, sem commit** — falta teste no jogo |
| 30 | Skins de montaria | montarias | médio | **Código pronto, sem commit** — falta teste no jogo |
| 29 | Chama colorida na tocha e na lanterna | Deadheim | médio | Depende de um sistema de chamas que não existe; entra no Halloween |
| H1 | Pacote de Halloween: título Assombrado, vela Abóbora, chama roxa, lápide de caveira, efeito de abate almas | vários | médio | Depende de 29, 31, 33 e da vela por cosmético |
| H2 | Passe de temporada: NPC Arauto da Temporada, 10 níveis, 3 grátis, 500 DC retroativo, só cosmético | npc-valheim | grande | Onda 3 |
| 24 | Plantas salvas | Deadheim | grande | Cada peça passa por `Player.PlacePiece` para herdar as travas de ward, raid e combate |
| 35 | Transmog | Deadheim | grande | |
| 36 | Pet cosmético | Deadheim | grande | |

## 2. Decisões que dependem de você

- **Data da abertura.** Adiada em 14/10, sem data nova.
- **Item 21 (marcadores extras no mapa).** Você não soube dizer o que seria. Foi tirado do catálogo. Defina ou descarte.
- **Ficha de Halloween.** A venda abre em 24/10, mas o pacote ainda não existe. Escolha entre adiar a janela de venda e entregar o pacote até lá.
- **Ficha do Barbeiro, da Temporada e do Brasão.** Já estão à venda no código, mas o efeito não existe. Escolha entre tirar da loja por enquanto e entregar o efeito.
- **Preços da economia.** Os números do Mercador de bioma e do Mestre das Montarias são proposta implementada. Confirme ou ajuste.

## 3. Do administrador (sem código)

- **Mercadores de bioma já colocados no servidor.** Os modelos novos não sobrescrevem NPCs existentes. Edite na aba Admin: preço em Deadcoins, Garantia de Refino (1.000 moedas ou 30 DC, 10 por semana), minério (2 lotes por semana), Portal Token (3.000 moedas). O Norte Profundo (Fader) ainda não tem modelo.
- **Valores de teste ainda no servidor.** Remover antes de abrir:
  `TesteSkills100`, `LiberarTodasGratis`, `ImmunityMinutes 0.3`, `CombatTagSeconds 1`, `KillerMaxDistance 100`, arena de teste, Coal no Intendente, `DamagePercent 0`, `FuelItem GreydwarfEye`, `WardLimit 3/5`.
  Com `TesteSkills100` ligado, o Treinador não vende aula.
- **Vela e brasão de guilda sob encomenda.** O mecanismo existe. Falta a arte, feita à mão.
- **Itens 37 a 40.** Bônus mensal de Deadcoins, item sazonal, Conselho Aesir, cargo e canal no Discord.
- **Cadastro.** VIP, Deadcoins e fichas continuam creditados à mão.
- **Fundador.** Defina as datas da Ficha do Fundador (hoje sem janela).

## 4. Testes que faltam no jogo

Os itens abaixo foram escritos sem abrir o Valheim:
- Aesir 6 (colher e replantar), 7 (abastecer em área), 8 (ferramentas sem desgaste), 9 (tempo restante), 12 (reparar tudo), 14 (`/depositar`), 15 (`/ordenar`) e 16 (`/etiqueta`).
- Treinador: Estudo, Proteção e Aula (precisa tirar o `TesteSkills100`).
- Mercadores de bioma: compra em Deadcoins e limite semanal.
- Mestre dos Passes: preço em Deadcoins só depois da primeira morte do chefe.
- Mestre das Montarias: compra em moedas e em Deadcoins.
- Garantia de Refino na Forja.
- Pick em massa e craft em lote: confirmar que não-VIP fica no vanilla.

## 5. Para o próximo release

- Montarias tem código sem commit de outra sessão. Não foi incluído no 7.6.0.
- Ao fechar cada parte, trocar "Em breve" por "No ar" no catálogo (`Deadheim/catalogo-loja.html`) e reexportar os PNGs de `catalogo-rascunho/`.
- Todo deploy troca a Whitelist (regra do servidor) e o release do Deadheim exige subir o `MinimumRequiredVersion`.

## Ordem sugerida

1. ~~Barbeiro e Aesir 22 e 23 (npc-valheim).~~ **Código pronto, aguardando teste e release R1.**
2. ~~Montarias 28 e 30.~~ **Código pronto, aguardando teste e release R2.**
3. Aesir 34 (arena, npc-valheim) — próxima da fila do npc-valheim.
4. Pet 36 fechamento (Deadheim + npc-valheim).
5. Aesir médios 10, 13, 19 e 32 (Deadheim).
6. Montarias 20 (estábulo) — depende de 28/30 fechados.
7. Halloween: chama 29, abate 31, lápide 33, vela.
8. Passe de temporada.
9. Grandes: 24, 35.
