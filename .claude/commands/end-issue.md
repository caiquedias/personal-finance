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

---

Economize tokens. Sem resumo extenso após concluir.
