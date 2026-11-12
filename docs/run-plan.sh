#!/usr/bin/env bash
# run-plan.sh - executa o plano SECAO a secao: uma sessao nova do Claude por "### x.y"
# (ou pela fase inteira, quando a fase nao tem secoes), com um PR por secao.
# Mesmo comportamento do run-plan.ps1 (prefira o .ps1 no Windows). Precisa de node no PATH.
#
# Uso (Git Bash, a partir de D:\deadheim\Deadheim):
#   bash docs/run-plan.sh                      # usa docs/plano-arena-cooldowns-binds.md
#   bash docs/run-plan.sh outro-plano.md
#   DRY_RUN=1 bash docs/run-plan.sh            # mostra a proxima secao, sem rodar
#
# Regras lidas do plano:
#   - "- [ ] (manual) ..."            item do usuario (teste/medicao no jogo): o script pula.
#   - "<!-- run-plan: model=X -->"    abaixo do "## Fase", escolhe o modelo da fase (MODEL ganha).
#   - "<!-- run-plan: junto -->"      logo abaixo de um "### x.y": roda junto com a secao anterior.
# Roda UMA fase por vez: para quando nao sobra item automatico na fase (checkpoint no jogo).
#
# Variaveis opcionais: REPOS="../npc-valheim ../montarias"  MAX_TURNS=60  BUDGET_USD=5
#                      MODEL=opus  CLAUDE_BIN=...  DRY_RUN=1  TODAS_FASES=1 (nao para entre fases)

set -uo pipefail

PLAN="${1:-docs/plano-arena-cooldowns-binds.md}"
REPOS="${REPOS:-../npc-valheim ../montarias}"
MAX_TURNS="${MAX_TURNS:-60}"
BUDGET_USD="${BUDGET_USD:-}"

find_claude() {
  if [ -n "${CLAUDE_BIN:-}" ] && [ -x "$CLAUDE_BIN" ]; then echo "$CLAUDE_BIN"; return; fi
  local c
  for c in claude claude.exe claude.cmd; do
    command -v "$c" 2>/dev/null && return
  done
  for c in "$HOME/.local/bin/claude.exe" "$HOME/.local/bin/claude" \
           "$HOME/.claude/local/claude" "$HOME/.claude/local/claude.exe" \
           "${APPDATA:-}/npm/claude.cmd" "${LOCALAPPDATA:-}/Programs/claude/claude.exe"; do
    [ -x "$c" ] && { echo "$c"; return; }
  done
  local base="${LOCALAPPDATA:-$HOME/AppData/Local}/Claude-Data"
  c="$(ls -t "$base"/*/claude-code/*/*/claude.exe 2>/dev/null | head -1)"
  [ -n "$c" ] && echo "$c"
}
CLAUDE="$(find_claude)"
if [ -z "$CLAUDE" ]; then
  echo "!!! Nao achei o Claude Code CLI. Use CLAUDE_BIN=\"/c/caminho/do/claude.exe\" bash docs/run-plan.sh"
  exit 1
fi
echo "Usando: $CLAUDE"

[ -f "$PLAN" ] || { echo "Plano nao encontrado: $PLAN"; exit 1; }
LOGDIR="tarefas/run-plan-logs"
NOTES="tarefas/run-plan-notas.md"
COSTS="tarefas/run-plan-custos.csv"
mkdir -p "$LOGDIR"
[ -f "$COSTS" ] || echo 'data;secao;modelo;status;turnos;custo_usd;tokens_entrada;tokens_cache_lidos;tokens_cache_criados;tokens_saida' > "$COSTS"

# units MODO [TITULO]
#   first  -> da primeira unidade com item automatico: fase, titulo, modelo, n auto, texto
#   count  -> n de itens automaticos da unidade TITULO
#   manual -> titulos das unidades da fase TITULO com itens (manual)
units() {
  tr -d '\r' < "$PLAN" | awk -v mode="$1" -v arg="${2:-}" '
    function flush() {
      if (title == "") return
      if (mode == "first" && auto > 0 && !done) {
        print phase; print title; print model; print auto; printf "%s", text; done = 1
      }
      if (mode == "count" && title == arg) print auto
      if (mode == "manual" && phase == arg && manual > 0) print title
    }
    function body(l) {
      if (l ~ /^[[:space:]]*- \[ \][[:space:]]*\(manual\)/) manual++
      else if (l ~ /^[[:space:]]*- \[ \]/) auto++
      text = text l "\n"
    }
    # "### " fica em espera: se a linha seguinte for <!-- run-plan: junto -->, junta com a anterior
    hold && /<!--[[:space:]]*run-plan:[[:space:]]*junto[[:space:]]*-->/ {
      h = held; sub(/^###[[:space:]]*/, "", h)
      if (title != phase) { title = title " + " h; text = text held "\n"; hold = 0; next }
    }
    hold { hold = 0; flush(); title = held; model = pmodel; auto = 0; manual = 0; text = held "\n" }
    /^## /   { flush(); phase = $0; pmodel = ""; title = $0; model = ""; auto = 0; manual = 0; text = $0 "\n"; next }
    /<!--[[:space:]]*run-plan:[[:space:]]*model=/ {
      m = $0; sub(/.*model=/, "", m); sub(/[[:space:]]*-->.*/, "", m); pmodel = m; model = m; next }
    /<!--[[:space:]]*run-plan:/ { next }
    /^### /  { held = $0; hold = 1; next }
    /^---[[:space:]]*$/ { next }
    { body($0) }
    END { if (hold) { flush(); title = held; model = pmodel; auto = 0; manual = 0; text = held "\n" } flush() }'
}

read_first() {
  local out; out="$(units first)"
  U_PHASE="$(sed -n 1p <<<"$out")"
  U_TITLE="$(sed -n 2p <<<"$out")"
  U_MODEL="$(sed -n 3p <<<"$out")"
  U_AUTO="$(sed -n 4p <<<"$out")"
  U_TEXT="$(sed -n '5,$p' <<<"$out")"
}

# Le o JSON do claude -p e imprime: resposta, depois uma linha "status;turnos;custo;in;cache_r;cache_w;out"
parse_json() {
  node -e '
    let s = require("fs").readFileSync(0, "utf8"); let j;
    try { j = JSON.parse(s); } catch (e) { console.log(s); console.log("nao_json;0;0;0;0;0;0"); process.exit(0); }
    const u = j.usage || {};
    console.log(j.result || "");
    console.log([j.subtype, j.num_turns, j.total_cost_usd, u.input_tokens, u.cache_read_input_tokens,
                 u.cache_creation_input_tokens, u.output_tokens].join(";"));'
}

read_first
if [ -z "$U_PHASE" ]; then echo "Nenhum item automatico pendente em $PLAN."; exit 0; fi
START_PHASE="$U_PHASE"
echo "=== Iniciando: $START_PHASE ==="

REPO_LIST=""
ADD_DIRS=()
for r in $REPOS; do
  REPO_LIST="${REPO_LIST:+$REPO_LIST, }$(basename "$r") ($r)"
  ADD_DIRS+=(--add-dir "$r")
done
TOTAL=0

N=0
while true; do
  read_first
  if [ -n "${TODAS_FASES:-}" ] && [ -n "$U_PHASE" ] && [ "$U_PHASE" != "$START_PHASE" ]; then
    units manual "$START_PHASE" | sed 's/^/--- (manual) fica com voce: /'
    echo "=== $START_PHASE concluida (sem checkpoint, TODAS_FASES). Seguindo: $U_PHASE ==="
    START_PHASE="$U_PHASE"
  fi
  if [ -z "$U_PHASE" ] || [ "$U_PHASE" != "$START_PHASE" ]; then
    MAN="$(units manual "$START_PHASE")"
    [ -n "$MAN" ] && { echo "--- Itens (manual) que ficam com voce nesta fase:"; sed 's/^/    /' <<<"$MAN"; }
    echo "--- Gasto nesta execucao: US\$ $TOTAL (detalhe em $COSTS)"
    if [ -z "$U_PHASE" ]; then echo "=== Plano inteiro concluido (fora os itens manuais). ==="; break; fi
    echo "=== $START_PHASE concluida. Faca o checkpoint de teste no jogo, mergeie os PRs"
    echo "    e rode o script de novo para comecar: $U_PHASE ==="
    break
  fi

  N=$((N + 1))
  M="${MODEL:-$U_MODEL}"
  EXTRA=("${ADD_DIRS[@]}")
  [ -n "$M" ] && EXTRA+=(--model "$M")
  [ -n "$BUDGET_USD" ] && EXTRA+=(--max-budget-usd "$BUDGET_USD")
  STAMP="$(date +%Y%m%d-%H%M%S)"
  LOG="$LOGDIR/$STAMP-secao$N.json"
  ERRLOG="$LOGDIR/$STAMP-secao$N.stderr.log"
  echo
  echo ">>> Secao $N: $U_TITLE  ($U_AUTO item(ns) automatico(s), modelo: ${M:-padrao})"
  echo "    (log: $LOG)"

  PROMPT="Tarefa: implementar TODOS os checkboxes pendentes da secao abaixo do plano $PLAN
(fase '$START_PHASE'), sem comecar nenhuma outra secao. O trecho do plano e este:

$U_TEXT

Itens '- [ ] (manual)' sao do usuario (teste no jogo): nao faca. Linhas 'Restricao:' valem para a secao.
So abra o resto do plano se precisar de contexto de outra secao.
Antes de explorar o codigo, leia $NOTES (se existir): sao notas das secoes anteriores com os
arquivos e metodos-chave. Confie nelas e so reexplore o que faltar.

Repos: Deadheim (diretorio atual) e $REPO_LIST. Mexa no repo que a secao indicar.
Ler o servidor (somente leitura): powershell -ExecutionPolicy Bypass -File ../deadheim-deploy/bin/servidor.ps1 log \"<regex>\".
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
4. So entao marque como [x] em $PLAN cada checkbox que o PR cobre, com a linha
   'PR: <link>' logo abaixo de cada um (mesmo formato da Fase 0). Nao commite o plano.
5. Acrescente ao fim de $NOTES (crie se nao existir; nao commite) uma secao '## <titulo da secao>'
   com 5-10 linhas: arquivos/metodos-chave e o que fazem, pontos unicos, armadilhas, PRs/branches.
   Nada de narrar o trabalho; so o que poupa exploracao a proxima sessao.
6. Item que nao der para concluir (nao compila, falta informacao, decisao do usuario): deixe
   [ ], conclua os outros se forem independentes, e explique em 1-3 linhas no fim da resposta.
Seja objetivo; nada de refatoracao fora do escopo da secao."

  if [ -n "${DRY_RUN:-}" ]; then echo "--- DryRun: prompt que seria enviado ---"; echo "$PROMPT"; break; fi

  "$CLAUDE" -p "$PROMPT" "${EXTRA[@]}" \
    --output-format json \
    --strict-mcp-config \
    --disable-slash-commands \
    --tools "Read,Edit,Write,Glob,Grep,Bash" \
    --permission-mode acceptEdits \
    --allowedTools "Read,Edit,Write,Glob,Grep,Bash(git:*),Bash(gh:*),Bash(dotnet:*),Bash(powershell -ExecutionPolicy Bypass -File ../deadheim-deploy/bin/servidor.ps1:*)" \
    --max-turns "$MAX_TURNS" > "$LOG" 2> "$ERRLOG"

  OUT="$(parse_json < "$LOG")"
  STATS="$(tail -n1 <<<"$OUT")"
  sed '$d' <<<"$OUT"
  IFS=';' read -r ST TURNS COST IN CR CW OUTT <<<"$STATS"
  echo "--- status: $ST | turnos: $TURNS | US\$ $COST"
  echo "$(date +%Y-%m-%dT%H:%M:%S);${U_TITLE//;/,};${M:-padrao};$ST;$TURNS;$COST;$IN;$CR;$CW;$OUTT" >> "$COSTS"
  TOTAL="$(node -e "console.log((+process.argv[1] + +process.argv[2]).toFixed(2))" "$TOTAL" "${COST:-0}")"
  if [ "$ST" != "success" ]; then
    echo "!!! A sessao terminou com '$ST' (max-turns/orcamento/erro). Revise: $LOG e $ERRLOG"; exit 1
  fi

  AFTER="$(units count "$U_TITLE" | head -1)"
  AFTER="${AFTER:-0}"
  if [ "$AFTER" -ge "$U_AUTO" ]; then
    echo "!!! Nenhum item da secao foi marcado. Parando para voce revisar: $LOG"; exit 1
  fi
  if [ "$AFTER" -gt 0 ]; then
    echo "!!! A secao ficou com $AFTER item(ns) pendente(s). Parando para voce revisar: $LOG"; exit 1
  fi
done
