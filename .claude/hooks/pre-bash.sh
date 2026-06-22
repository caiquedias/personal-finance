#!/bin/bash
# Guardrail de segurança — PreToolUse (Bash)
# exit 2 bloqueia execução e retorna erro ao agente

INPUT=$(cat)
CMD=$(echo "$INPUT" | python3 -c "
import sys, json
d = json.load(sys.stdin)
print(d.get('tool_input', {}).get('command', ''))
" 2>/dev/null)

# Branches protegidas — commits e pushes diretos proibidos
PROTECTED_BRANCH_PATTERN="^(master|release|develop|feat/.+|fix/.+|hotfix/.+)$"

# 1. Bloqueia push direto para branches protegidas
if echo "$CMD" | grep -qE "git push[^|&;]*origin[^|&;]*(master|release|develop)([ \"']|$)"; then
  BRANCH=$(echo "$CMD" | grep -oE "(master|release|develop)")
  echo "BLOQUEADO: push direto para '$BRANCH' não permitido." >&2
  echo "Use: git push origin HEAD:claude/<worktree> e abra PR via gh pr create" >&2
  exit 2
fi

# 2. Bloqueia force push
if echo "$CMD" | grep -qE "git push[^|&;]*(--force|-f)( |$)"; then
  echo "BLOQUEADO: force push não permitido." >&2
  echo "Se necessário, solicite aprovação explícita do Caique." >&2
  exit 2
fi

# 3. Bloqueia commit direto em branches protegidas
if echo "$CMD" | grep -qE "git commit"; then
  WORKTREE_PATH=$(echo "$CMD" | grep -oP '(?<=git -C )[^ ]+' | head -1)
  if [ -n "$WORKTREE_PATH" ]; then
    CURRENT_BRANCH=$(git -C "$WORKTREE_PATH" rev-parse --abbrev-ref HEAD 2>/dev/null)
  else
    CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
  fi
  if echo "$CURRENT_BRANCH" | grep -qE "$PROTECTED_BRANCH_PATTERN"; then
    echo "BLOQUEADO: commit direto na branch '$CURRENT_BRANCH' não permitido." >&2
    echo "Agentes devem trabalhar em worktrees (claude/*). Crie o worktree antes de commitar." >&2
    exit 2
  fi
fi

# 4. Bloqueia remoção de arquivos
if echo "$CMD" | grep -qE "^rm( |$|-[a-zA-Z])"; then
  echo "BLOQUEADO: comando rm não permitido em execução autônoma." >&2
  echo "Remoção de arquivos requer aprovação explícita do Caique." >&2
  exit 2
fi

# 5. Bloqueia git clean
if echo "$CMD" | grep -qE "git clean( |$|-)"; then
  echo "BLOQUEADO: git clean não permitido — apagaria arquivos não rastreados." >&2
  exit 2
fi

# 6. Bloqueia git reset --hard
if echo "$CMD" | grep -qE "git reset[^|&;]*--hard"; then
  echo "BLOQUEADO: git reset --hard não permitido — destrói mudanças não commitadas." >&2
  echo "Use git stash ou git restore para descartar mudanças pontuais." >&2
  exit 2
fi

# 7. Bloqueia git rm
if echo "$CMD" | grep -qE "git rm( |$|-)"; then
  echo "BLOQUEADO: git rm não permitido em execução autônoma." >&2
  exit 2
fi

# 8. Bloqueia dotnet ef database update
if echo "$CMD" | grep -qE "dotnet ef database update"; then
  echo "BLOQUEADO: dotnet ef database update não permitido em execução autônoma." >&2
  echo "Execute manualmente: dotnet ef database update --project src/PersonalFinance.Infrastructure --startup-project src/PersonalFinance.Api" >&2
  exit 2
fi

# 9. Bloqueia dotnet ef migrations remove
if echo "$CMD" | grep -qE "dotnet ef migrations remove"; then
  echo "BLOQUEADO: dotnet ef migrations remove não permitido em execução autônoma." >&2
  exit 2
fi

exit 0
