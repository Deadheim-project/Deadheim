# Localização PT/EN do Deadheim

- Classe `L` ([Localizacao/L.cs](../Localizacao/L.cs)). Textos em `Localizacao/Textos.*.cs`, um arquivo por módulo: `L.Add("modulo.chave", "pt", "en")`.
- Uso no código:
  - `L.T("modulo.chave", args...)`: texto local (string.Format com `{0}`).
  - `L.Tok("modulo.chave")`: devolve `$dh_modulo.chave`, para `m_name`/`m_description` (o jogo traduz sozinho).
  - `L.Net("modulo.chave", args...)`: mensagem do servidor para o cliente. O servidor manda a chave; o cliente traduz com `L.Resolve` (já aplicado em PvpClient/RankingRede/Pets). Logs/Discord do servidor saem em português (`L.Resolve(x, true)`).
- Config `General/Idioma`: `auto` (segue o idioma do jogo: "Portuguese*" = pt, o resto = en), `pt` ou `en`.
- Novo módulo: criar `Textos.X.cs` com `Registrar()`, incluir no `Deadheim.csproj` e chamar em `Plugin.Awake` (ao lado de `TextosLixeira.Registrar()`).
- Fora do escopo (continuam em português): descrições de config (`ConfigDescription`), logs (`Debug.Log`), comandos/descrições de console, ferramentas de admin (`FerramentasAdmin`, respostas do `AdminPainel`, `/pvpadmin`), nomes `GarantiaNome`/`DeadToken`/`Totem da Cidade` (chaves de item/peça), Testing/.
