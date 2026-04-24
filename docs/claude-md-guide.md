# Guia de manutenção do CLAUDE.md

Consultar este arquivo **antes de qualquer alteração** no CLAUDE.md.

---

## Orçamento de linhas

| Arquivo | Limite |
|---------|--------|
| `CLAUDE.md` | ≤ 250 linhas |
| Cada arquivo em `docs/` | sem limite |

Se uma adição ao CLAUDE.md ultrapassar o limite → mover o conteúdo mais longo para `docs/` e adicionar apenas uma linha de referência no CLAUDE.md.

---

## O que pertence a cada lugar

### CLAUDE.md — apenas o que é consultado em toda sessão

- Comandos de build/test/run
- Regras críticas de execução e código (1–2 linhas cada)
- IDs fixos do projeto (board, campos)
- Ciclo de vida da issue e fluxo de versionamento (resumido)
- Referências para `docs/`

### docs/ — conteúdo extenso ou consultado pontualmente

| Arquivo | Conteúdo |
|---------|----------|
| `docs/patterns.md` | Padrão de modal Angular; armadilhas conhecidas |
| `docs/test-factory.md` | Setup do `TestWebApplicationFactory` |
| `docs/claude-md-guide.md` | Este arquivo |

---

## Quando e onde adicionar novos itens

| Tipo de conteúdo | Destino |
|------------------|---------|
| Nova regra crítica que se aplica a **toda** sessão | CLAUDE.md — 1 linha |
| Nova armadilha descoberta (erro + causa + fix) | `docs/patterns.md` — linha na tabela |
| Novo padrão de código com snippet | `docs/patterns.md` — nova seção |
| Setup técnico com bloco de código longo | `docs/test-factory.md` ou novo `docs/xxx.md` |
| IDs de projeto/board | CLAUDE.md — tabela existente |

---

## Regras de escrita

- Sem introduções, conclusões ou texto explicativo — direto ao ponto
- Cada regra: 1 linha ou 1 entrada de tabela
- Snippets de código: apenas em `docs/`, nunca inline no CLAUDE.md
- Nunca duplicar informação entre CLAUDE.md e `docs/`

---

## Processo de atualização

1. Identificar o tipo de conteúdo (tabela acima)
2. Verificar se já existe entrada relacionada — atualizar em vez de duplicar
3. Se for para CLAUDE.md: checar contagem de linhas após a edição (`wc -l CLAUDE.md`)
4. Se ultrapassar 250 linhas: mover seção menos crítica para `docs/`
