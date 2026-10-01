#!/usr/bin/env bash
# Repara refs zerados (SHA 000...0) deixados por travamento/reinício forçado.
# Só toca em arquivos descartáveis: refs de rastreio remoto e ORIG_HEAD de worktrees.
# Refs locais (refs/heads, tags) e objetos corrompidos são apenas reportados.
# Uso: bash scripts/git-heal.sh   (a partir de qualquer checkout/worktree do repo)

set -u
GIT_COMMON="$(git rev-parse --git-common-dir)" || exit 1
ZERO="0000000000000000000000000000000000000000"
fixed=0

is_zero() { [ "$(tr -d '[:space:]' < "$1" 2>/dev/null)" = "$ZERO" ] || [ ! -s "$1" ]; }

# 1. Refs de rastreio remoto zerados/vazios: apagar e deixar o fetch recriar
while IFS= read -r f; do
  if is_zero "$f"; then
    echo "removendo ref de rastreio quebrado: ${f#"$GIT_COMMON"/}"
    rm -f "$f" && fixed=$((fixed + 1))
  fi
done < <(find "$GIT_COMMON/refs/remotes" -type f 2>/dev/null)

# 2. ORIG_HEAD zerado/vazio (global e por worktree): descartável
for f in "$GIT_COMMON/ORIG_HEAD" "$GIT_COMMON"/worktrees/*/ORIG_HEAD; do
  [ -f "$f" ] || continue
  if is_zero "$f"; then
    echo "removendo ORIG_HEAD quebrado: ${f#"$GIT_COMMON"/}"
    rm -f "$f" && fixed=$((fixed + 1))
  fi
done

# 3. Refs locais zerados: NÃO apagar — só avisar (recuperar via reflog ou origin)
while IFS= read -r f; do
  if is_zero "$f"; then
    echo "ATENÇÃO ref local quebrado (corrigir manualmente via 'git reflog' ou 'git branch -f <b> origin/<b>'): ${f#"$GIT_COMMON"/}"
  fi
done < <(find "$GIT_COMMON/refs/heads" "$GIT_COMMON/refs/tags" -type f 2>/dev/null)

# 4. Recriar rastreio remoto e verificar integridade
git fetch --prune origin || echo "AVISO: fetch falhou (sem rede?)"
echo "--- git fsck"
git fsck --no-dangling && echo "fsck OK ($fixed arquivo(s) reparado(s))"
