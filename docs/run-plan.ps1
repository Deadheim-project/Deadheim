# run-plan.ps1 - executa o plano SECAO a secao: uma sessao nova do Claude por "### x.y"
# (ou pela fase inteira, quando a fase nao tem secoes), com um PR por secao.
#
# Uso (PowerShell, a partir de D:\deadheim\Deadheim):
#   powershell -ExecutionPolicy Bypass -File docs\run-plan.ps1
#   powershell -ExecutionPolicy Bypass -File docs\run-plan.ps1 -Model opus      # forca o modelo
#   powershell -ExecutionPolicy Bypass -File docs\run-plan.ps1 -BudgetUsd 5     # teto de gasto por secao
#   powershell -ExecutionPolicy Bypass -File docs\run-plan.ps1 -TodasFases      # nao para entre fases
#   powershell -ExecutionPolicy Bypass -File docs\run-plan.ps1 -Login           # so faz o login do CLI
#   powershell -ExecutionPolicy Bypass -File docs\run-plan.ps1 -DryRun          # mostra a proxima secao, sem rodar
#
# Regras lidas do plano:
#   - "- [ ] (manual) ..."            item do usuario (teste/medicao no jogo): o script pula.
#   - "<!-- run-plan: model=X -->"    abaixo do "## Fase", escolhe o modelo da fase (-Model ganha).
#   - "<!-- run-plan: junto -->"      abaixo de um "### x.y": roda na mesma sessao da secao anterior
#                                     (use quando as duas mexem no mesmo codigo).
# Roda UMA fase por vez: para quando nao sobra item automatico na fase (checkpoint no jogo).
#
# Economia de tokens: sem MCPs (--strict-mcp-config), sem lista de skills
# (--disable-slash-commands), so as ferramentas de codigo (--tools), build silencioso e uma nota
# de passagem (tarefas/run-plan-notas.md) que a proxima secao le em vez de reexplorar o codigo.
# Custo e tokens de cada secao vao para tarefas/run-plan-custos.csv.

param(
  [string]$Plan = 'docs/plano-arena-cooldowns-binds.md',
  [string[]]$Repos = @('../npc-valheim', '../montarias'),
  [int]$MaxTurns = 60,
  [double]$BudgetUsd = 0,
  [string]$Model = '',
  [string]$ClaudeBin = '',
  [switch]$TodasFases,
  [switch]$Login,
  [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8

function Find-Claude {
  if ($ClaudeBin -and (Test-Path $ClaudeBin)) { return $ClaudeBin }
  $cmd = Get-Command claude -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }
  foreach ($c in @("$env:USERPROFILE\.local\bin\claude.exe", "$env:APPDATA\npm\claude.cmd")) {
    if (Test-Path $c) { return $c }
  }
  # CLI embutido no app desktop: pega o mais novo
  $emb = Get-ChildItem "$env:LOCALAPPDATA\Claude-Data\*\claude-code\*\*\claude.exe" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($emb) { return $emb.FullName }
  return $null
}

$claude = Find-Claude
if (-not $claude) {
  Write-Host '!!! Nao achei o Claude Code CLI. Instale com:  irm https://claude.ai/install.ps1 | iex'
  exit 1
}
Write-Host "Usando: $claude"

if ($Login) {
  Write-Host 'Abrindo o Claude interativo: digite /login, siga os passos e depois /exit.'
  & $claude
  exit 0
}

if (-not (Test-Path $Plan)) { Write-Host "Plano nao encontrado: $Plan"; exit 1 }

$rxPending = '^\s*- \[ \]'
$rxManual  = '^\s*- \[ \]\s*\(manual\)'

# Quebra o plano em unidades: cada "### " e uma unidade; uma fase sem "### " e uma unidade so.
# "### " com <!-- run-plan: junto --> e anexada a unidade anterior.
function Get-Units {
  $lines = Get-Content -LiteralPath $Plan -Encoding UTF8
  $units = New-Object System.Collections.ArrayList
  $phase = ''; $phaseModel = ''; $cur = $null
  foreach ($l in $lines) {
    if ($l -match '^## ') {
      $phase = $l.Trim(); $phaseModel = ''
      $cur = @{ Phase = $phase; Title = $phase; Model = ''; Lines = New-Object System.Collections.ArrayList }
      [void]$units.Add($cur); [void]$cur.Lines.Add($l)
      continue
    }
    if ($l -match '<!--\s*run-plan:\s*model=(\S+)\s*-->' -and $cur) {
      $phaseModel = $Matches[1]; $cur.Model = $phaseModel; continue
    }
    if ($l -match '<!--\s*run-plan:\s*junto\s*-->' -and $cur -and $units.Count -ge 2) {
      $prev = $units[$units.Count - 2]
      if ($prev.Phase -eq $cur.Phase -and $prev.Title -ne $prev.Phase) {
        foreach ($x in $cur.Lines) { [void]$prev.Lines.Add($x) }
        $prev.Title = "$($prev.Title) + $($cur.Title -replace '^###\s*', '')"
        $units.RemoveAt($units.Count - 1); $cur = $prev
      }
      continue
    }
    if ($l -match '^### ') {
      $cur = @{ Phase = $phase; Title = $l.Trim(); Model = $phaseModel; Lines = New-Object System.Collections.ArrayList }
      [void]$units.Add($cur); [void]$cur.Lines.Add($l)
      continue
    }
    if ($l -match '^---\s*$') { continue }
    if ($cur) { [void]$cur.Lines.Add($l) }
  }
  foreach ($u in $units) {
    $u.Auto   = @($u.Lines | Where-Object { $_ -match $rxPending -and $_ -notmatch $rxManual }).Count
    $u.Manual = @($u.Lines | Where-Object { $_ -match $rxManual }).Count
    $u.Text   = ($u.Lines -join "`n").Trim()
  }
  return $units
}

function First-Unit { Get-Units | Where-Object { $_.Auto -gt 0 } | Select-Object -First 1 }

$logDir = 'tarefas/run-plan-logs'
$notes  = 'tarefas/run-plan-notas.md'
$costs  = 'tarefas/run-plan-custos.csv'
New-Item -ItemType Directory -Force $logDir | Out-Null
if (-not (Test-Path $costs)) {
  'data;secao;modelo;status;turnos;custo_usd;tokens_entrada;tokens_cache_lidos;tokens_cache_criados;tokens_saida' |
    Out-File -FilePath $costs -Encoding utf8
}

$first = First-Unit
if (-not $first) { Write-Host "Nenhum item automatico pendente em $Plan."; exit 0 }
$startPhase = $first.Phase
Write-Host "=== Iniciando: $startPhase ==="

$repoList = ($Repos | ForEach-Object { "$(Split-Path $_ -Leaf) ($_)" }) -join ', '
$tools = 'Read,Edit,Write,Glob,Grep,Bash'
$allowed = 'Read,Edit,Write,Glob,Grep,Bash(git:*),Bash(gh:*),Bash(dotnet:*),Bash(powershell -ExecutionPolicy Bypass -File ../deadheim-deploy/bin/servidor.ps1:*)'
$total = 0.0

$n = 0
while ($true) {
  $u = First-Unit
  if ($TodasFases -and $u -and $u.Phase -ne $startPhase) {
    $manual = @(Get-Units | Where-Object { $_.Phase -eq $startPhase -and $_.Manual -gt 0 })
    foreach ($m in $manual) { Write-Host "--- (manual) fica com voce: $($m.Title)" }
    Write-Host "=== $startPhase concluida (sem checkpoint, -TodasFases). Seguindo: $($u.Phase) ==="
    $startPhase = $u.Phase
  }
  if (-not $u -or $u.Phase -ne $startPhase) {
    $manual = @(Get-Units | Where-Object { $_.Phase -eq $startPhase -and $_.Manual -gt 0 })
    if ($manual.Count) {
      Write-Host "--- Itens (manual) que ficam com voce nesta fase:"
      foreach ($m in $manual) { Write-Host "    $($m.Title)" }
    }
    Write-Host ("--- Gasto nesta execucao: US$ {0:N2} (detalhe em {1})" -f $total, $costs)
    if (-not $u) { Write-Host '=== Plano inteiro concluido (fora os itens manuais). ==='; break }
    Write-Host "=== $startPhase concluida. Faca o checkpoint de teste no jogo, mergeie os PRs"
    Write-Host "    e rode o script de novo para comecar: $($u.Phase) ==="
    break
  }

  $n++
  $title = $u.Title
  $model = if ($Model) { $Model } else { $u.Model }
  $extra = @()
  if ($model) { $extra += @('--model', $model) }
  if ($BudgetUsd -gt 0) { $extra += @('--max-budget-usd', "$BudgetUsd") }
  foreach ($r in $Repos) { $extra += @('--add-dir', $r) }
  $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
  $log = Join-Path $logDir ("{0}-secao{1}.json" -f $stamp, $n)
  $errLog = Join-Path $logDir ("{0}-secao{1}.stderr.log" -f $stamp, $n)
  Write-Host ''
  Write-Host ">>> Secao ${n}: $title  ($($u.Auto) item(ns) automatico(s), modelo: $(if ($model) { $model } else { 'padrao' }))"
  Write-Host "    (log: $log)"

  $prompt = @"
Tarefa: implementar TODOS os checkboxes pendentes da secao abaixo do plano $Plan
(fase '$startPhase'), sem comecar nenhuma outra secao. O trecho do plano e este:

$($u.Text)

Itens '- [ ] (manual)' sao do usuario (teste no jogo): nao faca. Linhas 'Restricao:' valem para a secao.
So abra o resto do plano se precisar de contexto de outra secao.
Antes de explorar o codigo, leia $notes (se existir): sao notas das secoes anteriores com os
arquivos e metodos-chave. Confie nelas e so reexplore o que faltar.

Repos: Deadheim (diretorio atual) e $repoList. Mexa no repo que a secao indicar.
Ler o servidor (somente leitura): powershell -ExecutionPolicy Bypass -File ../deadheim-deploy/bin/servidor.ps1 log "<regex>".
Compile sempre em modo silencioso: dotnet build -v q -clp:ErrorsOnly -nologo.

Fluxo (um PR por repo tocado, cobrindo a secao inteira):
0. Rode git status no repo da secao. Alteracoes nao commitadas la sao de uma execucao
   interrompida desta secao: revise e aproveite-as.
1. Branch: parta do HEAD atual do repo (ja contem as secoes anteriores desta fase, ainda nao
   mergeadas). Nome: feat/fase<N>-<slug-curto-da-secao>. Se o HEAD ja for uma branch desta
   secao com PR aberto, continue nela e no mesmo PR.
2. Implemente os itens e compile ate passar. Nao precisa testar no jogo.
3. Commit, push e PR com gh pr create. Base: a branch atual se ela tiver PR aberto
   (empilhado), senao main. No corpo do PR, liste os itens da secao cobertos.
4. So entao marque como [x] em $Plan cada checkbox que o PR cobre, com a linha
   'PR: <link>' logo abaixo de cada um (mesmo formato da Fase 0). Nao commite o plano.
5. Acrescente ao fim de $notes (crie se nao existir; nao commite) uma secao '## <titulo da secao>'
   com 5-10 linhas: arquivos/metodos-chave e o que fazem, pontos unicos, armadilhas, PRs/branches.
   Nada de narrar o trabalho; so o que poupa exploracao a proxima sessao.
6. Item que nao der para concluir (nao compila, falta informacao, decisao do usuario): deixe
   [ ], conclua os outros se forem independentes, e explique em 1-3 linhas no fim da resposta.
Seja objetivo; nada de refatoracao fora do escopo da secao.
"@

  if ($DryRun) { Write-Host '--- DryRun: prompt que seria enviado ---'; Write-Host $prompt; break }

  # Prompt via stdin (evita problemas de aspas do PowerShell 5.1 com executaveis nativos).
  # Saida em JSON (resposta final + custo + tokens); stderr vai para arquivo separado.
  $ErrorActionPreference = 'Continue'
  $raw = $prompt | & $claude -p @extra `
    --output-format json `
    --strict-mcp-config `
    --disable-slash-commands `
    --tools $tools `
    --permission-mode acceptEdits `
    --allowedTools $allowed `
    --max-turns $MaxTurns 2> $errLog
  $ErrorActionPreference = 'Stop'
  $rawText = ($raw | ForEach-Object { "$_" }) -join "`n"
  [IO.File]::WriteAllText((Join-Path $PWD $log), $rawText, $utf8)

  $res = $null
  try { $res = $rawText | ConvertFrom-Json } catch { }
  if ($res) {
    Write-Host $res.result
    $cost = [double]$res.total_cost_usd; $total += $cost
    $us = $res.usage
    Write-Host ("--- status: {0} | turnos: {1} | US$ {2:N2}" -f $res.subtype, $res.num_turns, $cost)
    ('{0};{1};{2};{3};{4};{5};{6};{7};{8};{9}' -f (Get-Date -Format s), ($title -replace ';', ','),
      $(if ($model) { $model } else { 'padrao' }), $res.subtype, $res.num_turns,
      ([string]$cost).Replace(',', '.'), $us.input_tokens, $us.cache_read_input_tokens,
      $us.cache_creation_input_tokens, $us.output_tokens) | Out-File -FilePath $costs -Append -Encoding utf8
    if ($res.subtype -ne 'success') {
      Write-Host "!!! A sessao terminou com '$($res.subtype)' (max-turns/orcamento/erro). Revise: $log"
      exit 1
    }
  } else {
    Write-Host $rawText
    Write-Host "!!! Resposta nao veio em JSON. Veja $log e $errLog"
  }

  $after = Get-Units | Where-Object { $_.Title -eq $title -and $_.Phase -eq $startPhase } | Select-Object -First 1
  if ($after -and $after.Auto -ge $u.Auto) {
    Write-Host "!!! Nenhum item da secao foi marcado. Parando para voce revisar: $log"
    exit 1
  }
  if ($after -and $after.Auto -gt 0) {
    Write-Host "!!! A secao ficou com $($after.Auto) item(ns) pendente(s). Parando para voce revisar: $log"
    exit 1
  }
}
