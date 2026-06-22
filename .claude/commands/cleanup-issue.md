Execute o cleanup de branches após o merge da issue.

**Argumento:** `$ARGUMENTS`
_(formato esperado: `<issue-number>` ou `<issue-url>`)_

---

## Pré-condição obrigatória

Verifique via GitHub se o PR de `feat/*` está com status **Merged**:
```bash
gh pr list --repo caiquedias/personal-finance --state merged --json number,title,headRefName | grep <issue-id>
```
Se não estiver: informe o status atual, interrompa e instrua o Caique a aguardar o merge.

---

## Passos de execução (nesta ordem)

### 1. Identificar branches da issue
```bash
git branch --list "feat/<issue-id>-*"
git worktree list
```

### 2. Checkout para develop e atualizar
```bash
git checkout develop
git pull origin develop
```

### 3. Remover o worktree
```bash
git worktree remove .claude/worktrees/<nome> --force
git push origin --delete claude/<nome>
```

### 4. Deletar a branch da feature
```bash
git branch -d feat/<id>-<slug>
git push origin --delete feat/<id>-<slug>
```

### 5. Confirmar estado final
```bash
git worktree list
git branch -a | grep <issue-id>
```

---

## Tratamento de erros

- `git branch -d` falhar: usar `-D` somente se PR estiver Merged — senão, abortar
- Branch remota já deletada: ignorar erro e continuar
- Worktree não encontrado: registrar como já limpo e continuar

---

**Exibir cada ação destrutiva (remoção de worktree, delete de branch) e aguardar OK do Caique antes de executar.**

Economize tokens. Sem resumo extenso após concluir.
