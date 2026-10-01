#!/bin/bash
# Linter automático pós-escrita de arquivo — PostToolUse (Write | Edit)
#
# Mesmo parser do pre-bash.sh: node + fallback em sed. Aqui a falha de parse é benigna
# (sai 0 sem lintar), diferente do pre-bash.sh, que bloqueia.

INPUT=$(cat)

FILE=$(printf '%s' "$INPUT" | node -e "
let d='';
process.stdin.on('data', c => d += c).on('end', () => {
  try { process.stdout.write(((JSON.parse(d).tool_input) || {}).file_path || ''); } catch (e) {}
});
" 2>/dev/null)

if [ -z "$FILE" ]; then
  FILE=$(printf '%s' "$INPUT" | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
fi

[ -z "$FILE" ] && exit 0

EXT="${FILE##*.}"

# Raiz do repositório resolvida em runtime — o agente roda de dentro do worktree na maior
# parte da sessão, então caminho relativo fixo não resolve.
REPO_ROOT=$(git rev-parse --show-toplevel 2>/dev/null)
SOLUTION="${REPO_ROOT:-.}/PersonalFinance.sln"

case "$EXT" in
  cs)
    [ -f "$SOLUTION" ] || exit 0
    dotnet format "$SOLUTION" --include "$FILE" --no-restore 2>&1
    ;;
  ts|html)
    npx eslint --fix "$FILE" 2>&1
    ;;
esac

exit 0
