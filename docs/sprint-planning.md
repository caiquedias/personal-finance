# Sprint Planning — Protocolo

## Visão geral

O planning acontece **antes** da implementação e é responsabilidade do Macro Agent com suporte do PO.
Nenhuma linha de código é alterada durante o planning.

---

## Pré-requisito

```bash
gh auth refresh -h github.com -s project  # só se token não tiver escopo project
```

---

## Fluxo do planning (`/start-issue`)

### 1. Leitura da issue
```bash
gh issue view <number> --repo caiquedias/personal-finance --json number,title,body,labels
```

### 2. Análise de Impacto (PO — Modo 1)

O Macro Agent spawna o PO **antes** de apresentar qualquer plano ao Caique.
O PO responde com `ANÁLISE CONCLUÍDA` contendo:

| Dimensão | O que avaliar |
|---|---|
| **Dependências afetadas** | Componentes, serviços, rotas, controllers, use cases tocados |
| **Pacotes/versões** | Dependências npm/NuGet que precisam atualizar (se aplicável) |
| **Risco de regressão** | Nível (Baixo/Médio/Alto), o que pode quebrar, testes existentes na área |

O resultado é incorporado ao plano apresentado ao Caique como item fixo.

### 3. Apresentação do plano ao Caique

O plano apresentado inclui **6 itens fixos**:
1. Setup de branches e worktree
2. Análise de Impacto — resumo do resultado do PO
3. Red — testes falhando
4. Green — implementação task a task
5. QA → UX Validator (se frontend) → Reviewer
6. Push + PR + board

### 4. Aguardar confirmação do Caique antes de qualquer ação externa

---

## Planejamento de issue no board (sprint planning clássico)

Para issues ainda no `Backlog` que precisam ser planejadas para uma sprint:

1. Listar issues: `gh issue list --state open --limit 50 --json number,title,body,labels --repo caiquedias/personal-finance`
2. Explorar codebase — identificar o que existe e o que falta implementar
3. Para cada issue, definir: Estimativa (h), Prioridade, Size (XS/S/M/L/XL), arquivos afetados
4. **Issues L/XL ou com >15 arquivos afetados → propor divisão em sub-issues antes de iniciar**
5. Postar comentário de planejamento na issue:
   ```
   ## 📋 Sprint Planning
   **Estimativa:** Xh | **Prioridade:** N | **Size:** XS/S/M/L/XL | **Risco:** Baixo/Médio/Alto
   ---
   **Backend** — o que criar/alterar
   **Frontend** — o que criar/alterar
   ### Arquivos afetados
   - lista de arquivos
   ```
6. Atualizar campos no board (Status → Ready, Size, Priority, Estimate)

### Issues válidas para planning
Planejar apenas issues com **Status = Backlog** no board — ignorar as demais.

### Critério de sizing

| Size | Arquivos criados/alterados |
|---|---|
| XS | 1–2 |
| S | 3–5 |
| M | 6–10 |
| L | 11–25 |
| XL | 25+ |

---

## IDs fixos (Project #2)

| Campo | Field ID | Opções |
|-------|----------|--------|
| Status | `PVTSSF_lAHOAOhFlc4BUMJ_zhBWAHQ` | Backlog `f75ad846` · Ready `61e4505c` · In progress `47fc9ee4` · In review `df73e18b` · Done `98236657` |
| Priority | `PVTSSF_lAHOAOhFlc4BUMJ_zhBWAPM` | P0 `79628723` · P1 `0a877460` · P2 `da944a9c` |
| Size | `PVTSSF_lAHOAOhFlc4BUMJ_zhBWAPQ` | XS `6c6483d2` · S `f784b110` · M `7515a9f1` · L `817d0097` · XL `db339eb2` |
| Estimate | `PVTF_lAHOAOhFlc4BUMJ_zhBWAPU` | número (horas) |

**Project ID:** `PVT_kwHOAOhFlc4BUMJ_`
