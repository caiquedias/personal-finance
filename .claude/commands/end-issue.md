Finalize a sessão de desenvolvimento para a issue abaixo.

**Argumento:** `$ARGUMENTS`
_(formato esperado: `<issue-number>` ou `<issue-url>`)_

---

## O que fazer

### 1. Coletar dados da sessão

```bash
gh issue view <number> --repo caiquedias/personal-finance --json number,title,body
git log origin/develop..HEAD --oneline
git diff origin/develop..HEAD --stat
```

### 2. Gerar arquivo de detalhe da issue

Crie `docs/memory/<ISSUE-NUMBER>.md`:

```markdown
# #<number> — <Título da issue>
Issue: #<number> | Data: <data de hoje> | Branch: feat/<id>-<slug>

## Arquivos criados
- `caminho/arquivo` — descrição de 1 linha

## Arquivos modificados
- `caminho/arquivo` — o que mudou e por quê

## Decisões técnicas
- decisões que desviam ou complementam o spec

## Desvios do spec
- qualquer diferença em relação às tasks da issue

## Estado do sistema após esta issue

### Back-end
- **Domain:** o que existe agora
- **Application:** use cases adicionados/alterados
- **Infrastructure:** repositórios/migrações
- **Api:** endpoints novos/alterados (formato: MÉTODO /api/v1/rota)

### Front-end
- **Rotas:** rotas adicionadas em app.routes.ts
- **Componentes:** componentes criados/alterados
- **Serviços:** serviços Angular novos/alterados

### Banco de dados
- **Migrations aplicadas:** lista de migrations
- **Entidades/Tabelas:** entidades novas/alteradas
```

### 3. Atualizar docs/memory/project-memory.md

Fazer `str_replace` cirúrgico:
- Índice por issue: adicionar 1 linha
- Estado atual por layer: atualizar os layers tocados

### 4. Mover issue → Done no board

```bash
gh project item-edit --project-id PVT_kwHOAOhFlc4BUMJ_ --id <ITEM_ID> --field-id PVTSSF_lAHOAOhFlc4BUMJ_zhBWAHQ --single-select-option-id 98236657
```

**Exibir a ação acima e aguardar OK do Caique antes de executar.**

### 5. Analisar eficiência da sessão

Baseie-se **somente** no comportamento observado nesta sessão — nunca em conselhos genéricos.

- Busque a estimativa original no comentário de Sprint Planning da issue (`Estimativa: Xh`)
- Calcule **duração de codificação**: primeiro commit do Red ao último commit do último Green
  (`git log --reverse --format=%ai origin/develop..HEAD`, isolando `test(...)`/`feat(...)` anteriores
  a qualquer `fix(...)` pós-Reviewer)
- Calcule **duração de sessão completa**: primeiro ao último commit da branch, incluindo os
  `fix(...)` de ciclo de correção. Se QA/Reviewer aprovaram de primeira sem deixar commit, estime
  pelo horário da notificação de conclusão — não invente
- Conte os ciclos de retrabalho (QA → GAP → PO → Red/Green → QA revalida; Reviewer → REQUER
  CORREÇÃO → Green → QA → Reviewer) e a causa raiz **observada** de cada um

Adicione ao `docs/memory/<ISSUE-NUMBER>.md`:

```markdown
## Análise de eficiência da sessão
- **Estimativa:** Xh
- **Duração de codificação:** Xh Xmin (Red → último Green, antes de qualquer fix pós-Reviewer)
- **Duração de sessão completa:** Xh Xmin (primeiro ao último commit, incl. ciclos de correção) | **Desvio vs. Estimativa:** +X% / dentro do estimado
- **Ciclos de retrabalho:** N (QA: N | Reviewer: N)
- **Causas identificadas:** <uma linha por causa, só as observadas nesta sessão>
- **Melhorias propostas:** <uma linha por proposta, se houver>
```

### 6. Diagnóstico de tokens da sessão

Para cada spawn de agente (Red, Green, QA, Reviewer, PO), extraia os tokens/tool calls reportados
no bloco de uso do harness. Etapa não reportada → "não medido", nunca estimar como se fosse medido.

Adicione ao `docs/memory/<ISSUE-NUMBER>.md`:

```markdown
## Diagnóstico de tokens da sessão
| Etapa | tokens | tool calls |
|---|---|---|
| <nome> | <N> | <N> |

**Total medido:** N tokens | **Não medido:** <quais etapas, se houver>
```

### Padrão de apresentação no chat (passos 5 e 6)

Os blocos vão para `docs/memory/<ISSUE-NUMBER>.md` em markdown puro, mas a apresentação **no chat**
segue este formato fixo — desvio de prazo precisa saltar aos olhos:

1. **Estimativa vs. duração, em bloco `diff`**: atraso = linhas `-` (vermelho); entregue antes =
   linhas `+` (verde). Duração e desvio percentual sempre no mesmo sinal.
2. **Melhoria(s) proposta(s), logo abaixo, em bloco `diff` com prefixo `+`** (verde), mesmo sem atraso.
3. **Tabela de tokens por agente/etapa, em ordem decrescente de tokens** (maior gasto primeiro).

Exemplo (issue com atraso):

````markdown
```diff
- Duração de codificação: 51min (estimativa: 34min)
- Desvio vs. Estimativa: +51,6% (atrasado)
```

```diff
+ Melhoria proposta: <uma linha por proposta>
```

| Agente/Etapa | Tokens | Tool calls |
|---|---:|---:|
| Green (correção pós-Reviewer) | 112.558 | 30 |
| QA (1ª rodada) | 111.242 | 23 |
| Red (inicial) | 106.809 | 14 |

**Total medido:** N tokens | **Não medido:** <etapas, se houver>
````

### 7. Checar se a calibração de estimativas está vencida

```bash
node scripts/calibrate-estimates.js --check
```

- **`OK: ...`** → nada a fazer, não mencione no resumo
- **`RECALIBRAR: ...`** (exit 1) → rode sem flag, atualize `docs/sprint-planning.md` →
  "Fórmula de Estimativa" com os números que mudaram **e** a linha
  `> **Última calibração:** <data> · n=<N> issues com codificação medida`

---

Economize tokens. Sem resumo extenso após concluir.
