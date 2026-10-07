#!/usr/bin/env bash
# Roda um teste específico numa base (default origin/develop) em worktree descartável,
# para confirmar se uma falha é pré-existente. Limpeza garantida (trap), inclusive
# quando o dotnet deixa processos de build segurando arquivos.
# Uso: bash scripts/run-test-on-base.sh <projeto-de-teste> <filtro> [ref-base]
# Ex.:  bash scripts/run-test-on-base.sh tests/PersonalFinance.Infrastructure.Tests \
#         "FullyQualifiedName~Parse_PrimaryIncome_ShouldExtractFromB4Formula" origin/develop

set -u
PROJECT="${1:-}"
FILTER="${2:-}"
BASE="${3:-origin/develop}"

if [ -z "$PROJECT" ] || [ -z "$FILTER" ]; then
  echo "Uso: bash scripts/run-test-on-base.sh <projeto-de-teste> <filtro> [ref-base]" >&2
  exit 2
fi

ORIGIN_DIR="$(pwd)"
REPO_TOP="$(git rev-parse --show-toplevel)" || exit 2
TMP_DIR="$(mktemp -d)" || exit 2
WT="$TMP_DIR/base"

cleanup() {
  cd "$ORIGIN_DIR" 2>/dev/null || cd "$REPO_TOP" || true  # sai do worktree antes de remover
  dotnet build-server shutdown >/dev/null 2>&1 || true     # solta arquivos em uso
  git -C "$REPO_TOP" worktree remove --force "$WT" >/dev/null 2>&1 || true
  rm -rf "$TMP_DIR" 2>/dev/null || true
  git -C "$REPO_TOP" worktree prune >/dev/null 2>&1 || true
  if [ -e "$TMP_DIR" ]; then
    echo "AVISO: não foi possível remover $TMP_DIR — apague manualmente." >&2
  fi
}
trap cleanup EXIT

git -C "$REPO_TOP" fetch origin -q || echo "AVISO: fetch falhou (sem rede?) — usando ref local" >&2
git -C "$REPO_TOP" worktree add --detach -q "$WT" "$BASE" || { echo "ERRO: worktree em $BASE falhou" >&2; exit 2; }

echo "Base: $BASE ($(git -C "$WT" rev-parse --short HEAD))"
cd "$WT" || exit 2
dotnet test "$PROJECT" --filter "$FILTER" 2>&1 | tail -15
exit "${PIPESTATUS[0]}"
