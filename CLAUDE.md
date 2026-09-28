# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Identidade do projeto

- **Nome:** Personal Finance System | **Empresa:** MonkeyBomb
- **Autor:** Caique Dias — Desenvolvedor Sênior, autodidata, 10 anos de experiência
- **Stack:** .NET 8 backend + Angular 21 frontend

---

## Comandos

### Backend (.NET)

```bash
dotnet build PersonalFinance.sln
dotnet test PersonalFinance.sln
dotnet test PersonalFinance.sln --filter "FullyQualifiedName~TestMethodName"
dotnet run --project src/PersonalFinance.Api/PersonalFinance.Api.csproj
dotnet ef migrations add <Nome> --project src/PersonalFinance.Infrastructure --startup-project src/PersonalFinance.Api
dotnet ef database update --project src/PersonalFinance.Infrastructure --startup-project src/PersonalFinance.Api
```

### Frontend (dentro de `personal-finance/`)

```bash
npm start        # ng serve em http://localhost:4200
npm run build    # build de produção
npm run lint
npm test -- --watch=false --browsers=ChromeHeadless 2>&1 | grep -E "SUCCESS|FAILED|ERROR|specs"
```

---

## Sprint Planning

### Pré-requisito
```bash
gh auth refresh -h github.com -s project  # só se token não tiver escopo project
```

Passo a passo completo, IDs de campos do board, critério de sizing e fórmula de estimativa:
→ [`docs/sprint-planning.md`](docs/sprint-planning.md)

### Ciclo de vida da issue

| Evento | Ação |
|--------|------|
| Sprint Planning concluído | → **Ready** |
| Início da implementação | → **In Progress** |
| PR worktree → feat criado | → **In Review** |
| PR feat aprovado | Vincular `feat/` à issue; PR feat → release; PR feat → master |
| Merge em master | → **Done** |

### Rastreabilidade

- Toda PR criada no contexto de uma issue deve ser vinculada à issue (via `Closes #N` no body)
- `feat/` vinculada à issue via PR com `Closes #N` após merge worktree → feat

---

## Versionamento

### Branches permanentes

| Branch | Propósito |
|--------|-----------|
| `master` | Produção |
| `release` | Homologação / QA |
| `develop` | Base para novos desenvolvimentos |

### Regras

- Push direto proibido em `master`, `release`, `develop` — apenas via PR
- Prefixos: `feat/` · `fix/` · `hotfix/` | Claude usa `claude/` (automático)
- `hotfix/` parte de `master`, não de `develop`

### Fluxo Feature/Fix

1. Criar `feat/xxx` ou `fix/xxx` a partir de `develop`
2. Claude trabalha em `claude/worktree` — push **somente** para `claude/`, nunca para `feat/`
3. PR: `claude/` → `feat/` (sem aprovadores)
4. PR: `feat/` → `release` (QA + integração)
5. PR: `feat/` → `master` (aprovação do grupo)
6. Merge em `master` → sync automático: `develop` ← `master` e `release` ← `master`

### Push correto no worktree

```bash
git push origin HEAD:claude/<nome-worktree>
gh pr create --head claude/<nome-worktree> --base feat/xxx ...
```

Push acidental para `feat/`: reverter com `git push origin <commit-anterior>:refs/heads/feat/xxx --force`.

### Setup dos git hooks

```bash
git config core.hooksPath .githooks
```

---

## Arquitetura

### Backend — Clean Architecture

`Api → Infrastructure → Application → Domain`

- **Domain** — Entidades, value objects, enums, interfaces de repositório. Factory `static Create()`. Sem dependências externas.
- **Application** — Use cases (um por classe), DTOs, FluentValidation. `IReportRepository` aqui (evita circular com Domain).
- **Infrastructure** — `AppDbContext`, repositórios, `Argon2PasswordHasher`, `JwtTokenService`, `ExcelParserService`, `DatabaseInitializer`.
- **Api** — Controllers `/api/v1/`, `ExceptionMiddleware`, DI em `InfrastructureExtensions`/`ApplicationExtensions`.

### Modelo de dados

`User · Category · Period (Year+Month, um ativo por usuário) · Expense (DueDate/PaymentDate/Status/Fortnight) · Income (SourceType)`

Lookup tables seeded: `Role`, `PaymentStatus`, `SourceType`, `FortnightType`. Soft-delete universal (`DeletedAt` + `HasQueryFilter` global). PKs: `Guid.NewGuid()` no Domain, `ValueGeneratedNever()` no EF.

`vw_PeriodSummary` — view SQL criada pelo `DatabaseInitializer` no startup (idempotente). Subconsultas separadas para Income e Expense — evita produto cartesiano.

### Autenticação

JWT Bearer. `AuthController` é `[AllowAnonymous]`. Admin: `[Authorize(Roles="Admin")]`. `authInterceptor` Angular injeta token automaticamente.

### Frontend — Angular 21

Standalone components (sem NgModules), lazy-loaded em `app.routes.ts`. Signals para estado local, RxJS para HTTP. `ApiService` wrapper HTTP. `ThemeService` dark/light.

### Testes

Integration: `WebApplicationFactory<Program>` + banco InMemory. Unit: xUnit + Moq + FluentAssertions. Infra: `ExcelParserService`.
→ Setup detalhado: [`docs/test-factory.md`](docs/test-factory.md)

---

## Regras absolutas de execução

- **Nunca alterar fora do escopo** — apresentar como novo plano, Caique aprova caso a caso
- **Verificar PR antes de push** — se fechado/mergeado, abrir novo PR para os ajustes
- **Pré-ação obrigatória** — antes de post em issue, board update ou push: exibir ação e aguardar OK do Caique
- **Sub-agentes: sempre `subagent_type` nomeado** — nunca colar o corpo de `.claude/agents/*.md` no prompt
→ Fluxo completo: [`docs/session-flow.md`](docs/session-flow.md) · Spawn de agentes: [`docs/agents.md`](docs/agents.md)

---

## Regras absolutas de código

### Geral
- **Um arquivo por classe** — sem exceção
- Idioma: inglês no código, comentários em PT-BR
- **Interfaces: nunca reescrever** — sempre `str_replace` cirúrgico para adicionar métodos
- **Correções pontuais**: sempre `str_replace` — nunca reescrever arquivo inteiro por 1 linha

### Backend (.NET 8)
- PKs: `Guid.NewGuid()` no Domain, `ValueGeneratedNever()` no EF Core
- `async` nunca usa `ref`/`out` — usar tuple `(T value, bool flag)`
- Enums: `.HasConversion<int>()` | FKs: `OnDelete(Restrict)`

### Frontend (Angular 21)
- Standalone, sem NgModule | `DecimalPipe` importado explicitamente em cada componente
- Modal: `@if` + Angular Animations — **nunca CDK Portal/OverlayRef**
→ Padrão validado: [`docs/patterns.md`](docs/patterns.md)

---

## Regras de testes

> Todas as diretrizes abaixo se aplicam a **Backend e Frontend**.

- **Toda nova feature exige testes de cobertura** — Backend (xUnit) e Frontend (Jasmine/Karma)
- **Cobrir obrigatoriamente**: regras de negócio e casos de uso envolvidos na implementação
- Testes apenas onde há **lógica de negócio nova** — não replicar padrões já cobertos
- Um arquivo de teste por classe | xUnit + Moq + FluentAssertions
- `MarkAsPaid` rejeita data futura → usar `DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))`
- `HasData` não popula InMemory → `SeedLookupData()` manual na factory
→ Setup factory: [`docs/test-factory.md`](docs/test-factory.md) · Protocolo TDD/guardrails: [`docs/testing.md`](docs/testing.md)

---

## Estilo de comunicação

- Direto e técnico — sem rodeios, elogios ou introduções
- Nunca suposições silenciosas — alinhar antes de implementar
- **Caique decide** a arquitetura — Claude propõe opções, Caique confirma

---

## Autonomia por contexto

| Contexto | Nível de autonomia |
|----------|--------------------|
| **Worktree** (`claude/`) | **Livre** — ler arquivos, implementar, rodar testes, ler GitHub/board, alterar status da issue sem pedir permissão |
| **Planning** | **Livre** — ler arquivos e board para levantamento; confirmar ordem/escopo antes de executar |
| **Branch `feat/`** | **Restrito** — confirmar antes de push, criação de PR e alterações fora do escopo |
| **Deploy / ambientes** | **Restrito** — sempre confirmar antes de qualquer ação |

Regra geral: **worktree é espaço de implementação autônomo**. Volta ao modo restrito quando a ação afeta branches do Caique, PRs públicas ou ambientes.

---

## Otimização de tokens

- Respostas curtas — sem repetir código ou contexto anterior
- `str_replace` cirúrgico — nunca reescrever arquivo completo
- Output de testes: filtrar com `grep -E "SUCCESS|FAILED|ERROR"`
- Issues L/XL (>15 arquivos): propor divisão antes de implementar

---

## Referências

- [`docs/claude-md-guide.md`](docs/claude-md-guide.md) — **consultar antes de qualquer alteração neste arquivo**
- [`docs/test-factory.md`](docs/test-factory.md) — TestWebApplicationFactory setup
- [`docs/testing.md`](docs/testing.md) — Protocolo TDD e guardrails de teste
- [`docs/patterns.md`](docs/patterns.md) — Padrão de modal Angular + Armadilhas conhecidas
- [`docs/session-flow.md`](docs/session-flow.md) — Fluxo de sessão: regra de pré-ação e revisão de planning
- [`docs/agents.md`](docs/agents.md) — Spawn de sub-agentes por `subagent_type`, contexto compartilhado
- [`docs/qa-agent.md`](docs/qa-agent.md) — Protocolo QA completo
- [`docs/code-review.md`](docs/code-review.md) — Checklist do Reviewer
- [`docs/sprint-planning.md`](docs/sprint-planning.md) — Planning, sizing, fórmula de estimativa
- [`docs/bug-fix.md`](docs/bug-fix.md) — Protocolo de bug fix explícito
