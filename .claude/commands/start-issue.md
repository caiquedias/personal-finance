Você é o Macro Agent do projeto Personal Finance (MonkeyBomb).
Leia o CLAUDE.md antes de qualquer ação.

**Argumento:** `$ARGUMENTS`
_(formato esperado: `<issue-number>` ou `<issue-url>`)_

---

## Passo 1 — Ler e apresentar o plano

1. Leia a issue via `gh issue view <number> --repo caiquedias/personal-finance --json number,title,body,labels`
2. Derive o nome da branch: `feat/<id>-<slug>` (slug em kebab-case do título)
3. Apresente ao Caique um passo a passo resumido (máximo 5 itens, 1 linha cada)
4. **Aguarde confirmação do Caique antes de avançar**

---

## Passo 2 — Preparar branch e worktree

Após confirmação, execute **nesta ordem exata**:

```bash
git fetch origin
git worktree add .claude/worktrees/<id>-<slug> -b claude/<id>-<slug> origin/develop
git push -u origin claude/<id>-<slug>
git checkout develop
git checkout -b feat/<id>-<slug> origin/develop
git push -u origin feat/<id>-<slug>
git checkout develop
```

**Verificação obrigatória antes de spawnar o Implementador:**
```bash
cd .claude/worktrees/<id>-<slug> && git branch --show-current
```
O output **deve ser exatamente** `claude/<id>-<slug>`.

Mova a issue para **In Progress** via board:
```bash
gh project item-edit --project-id PVT_kwHOAOhFlc4BUMJ_ --id <ITEM_ID> --field-id PVTSSF_lAHOAOhFlc4BUMJ_zhBWAHQ --single-select-option-id 47fc9ee4
```
_(obter `<ITEM_ID>` via `gh api graphql` — ver `docs/session-flow.md`)_

---

## Passo 3 — Spawnar o Implementador Red

Use a ferramenta **Agent** com o conteúdo de `.claude/agents/implementer-red.md` como prompt base.
Passe no contexto:
- Issue number, título e URL
- Acceptance criteria e edge cases das tasks descritas na issue
- Interfaces/contratos de domínio relevantes (somente assinaturas)
- Path do worktree: `.claude/worktrees/<id>-<slug>`

Aguarde RED CONCLUÍDO antes de avançar.

---

## Passo 4 — Para cada task: Spawnar o Implementador Green

Use a ferramenta **Agent** com o conteúdo de `.claude/agents/implementer-green.md` como prompt base.
Passe no contexto:
- Issue number, título
- Task atual (descrição detalhada)
- Testes falhando (commit do Red)
- Path do worktree

Aguarde GREEN CONCLUÍDO → passe para próxima task.

---

## Passo 5 — Spawnar o QA (OBRIGATÓRIO — não pular)

Use a ferramenta **Agent** com o conteúdo de `.claude/agents/qa.md` como prompt base.
Passe no contexto:
- Issue number e worktree path
- Lista de arquivos criados/modificados

Se resultado for `GAP_REPORT`: spawne o PO (modelo Opus) para avaliar sizing.
Só avance para o Passo 6 após `QA_APPROVED`.

---

## Passo 6 — Spawnar o UX Validator (somente se a issue incluir frontend)

Use a ferramenta **Agent** com o conteúdo de `.claude/agents/ux-validator.md`.
Só avance para o Passo 7 após `UX_APPROVED`.

---

## Passo 7 — Spawnar o Reviewer (OBRIGATÓRIO — não pular)

Use a ferramenta **Agent** com o conteúdo de `.claude/agents/reviewer.md`.
Se resultado for REQUER CORREÇÃO: Implementador → QA → Reviewer (ciclo até APROVADO).

---

## Passo 8 — Push e PR

Após APROVADO pelo Reviewer:

```bash
cd .claude/worktrees/<id>-<slug>
git push origin HEAD:claude/<id>-<slug>
```

Crie o PR usando o template de `docs/versioning.md`:
```bash
gh pr create \
  --head claude/<id>-<slug> \
  --base feat/<id>-<slug> \
  --title "<título da issue>" \
  --body "$(cat <<'EOF'
## Motivação
> <contexto em 1-2 linhas>

## O que foi implementado
<resumo técnico em 2-3 linhas>

## Arquivos

### Adicionados
| Arquivo | Descrição |
|---|---|

### Modificados
| Arquivo | O que mudou |
|---|---|

## Casos de teste

### Caminho feliz
- [ ] <cenário>

### Caminho triste
- [ ] <cenário>

## Critérios de aceite
- [ ] <critério>

Closes #<issue-number>
EOF
)"
```

Mova a issue para **In Review**:
```bash
gh project item-edit --project-id PVT_kwHOAOhFlc4BUMJ_ --id <ITEM_ID> --field-id PVTSSF_lAHOAOhFlc4BUMJ_zhBWAHQ --single-select-option-id df73e18b
```

---

## Passo 9 — Finalizar

- Confirme ao Caique com o link do PR criado
- Informe os próximos passos: `/end-issue <number>` (após merge) e `/cleanup-issue <number>` (após merge)
- **Não mova a issue para Done** — isso é responsabilidade do `/end-issue`

---

**Restrições:**
- Nunca execute o que deve ser delegado ao Implementador, QA ou Reviewer via Agent
- Nunca pule o Passo 5 (QA) — output do Implementador não substitui a análise do QA
- Nunca pule o Passo 7 (Reviewer) — obrigatório independentemente do tamanho
- Após correção do Reviewer: QA → Reviewer novamente
- Toda ação externa (push, PR, board) exige exibir a ação e aguardar OK do Caique
