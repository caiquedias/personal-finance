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

### Passo a passo

1. Listar issues: `gh issue list --state open --limit 50 --json number,title,body,labels --repo caiquedias/personal-finance`
2. Explorar codebase — identificar o que existe e o que falta implementar
3. Definir por issue: Estimativa (h), Prioridade, Size (XS/S/M/L/XL), arquivos afetados
4. **Issues L/XL ou com >15 arquivos afetados → propor divisão em sub-issues antes de iniciar**
5. Postar comentário de planejamento:
   ```
   ## 📋 Sprint Planning
   **Estimativa:** Xh | **Prioridade:** N | **Size:** XS/S/M/L/XL | **Risco:** Baixo/Médio/Alto
   ---
   **Backend** — o que criar/alterar
   **Frontend** — o que criar/alterar
   ### Arquivos afetados
   - lista de arquivos
   ```
6. Atualizar campos no board (Status → Ready, Size, Priority, Estimate):
   ```bash
   gh project item-edit --project-id <PROJECT_ID> --id <ITEM_ID> --field-id <FIELD_ID> --single-select-option-id <OPT_ID>
   gh project item-edit --project-id <PROJECT_ID> --id <ITEM_ID> --field-id <ESTIMATE_FIELD_ID> --number <HORAS>
   ```

### IDs fixos (Project #2)

| Campo | Field ID | Opções |
|-------|----------|--------|
| Status | `PVTSSF_lAHOAOhFlc4BUMJ_zhBWAHQ` | Backlog `f75ad846` · Ready `61e4505c` · In progress `47fc9ee4` · In review `df73e18b` · Done `98236657` |
| Priority | `PVTSSF_lAHOAOhFlc4BUMJ_zhBWAPM` | P0 `79628723` · P1 `0a877460` · P2 `da944a9c` |
| Size | `PVTSSF_lAHOAOhFlc4BUMJ_zhBWAPQ` | XS `6c6483d2` · S `f784b110` · M `7515a9f1` · L `817d0097` · XL `db339eb2` |
| Estimate | `PVTF_lAHOAOhFlc4BUMJ_zhBWAPU` | número (horas) |

**Project ID:** `PVT_kwHOAOhFlc4BUMJ_`

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
→ Fluxo completo: [`docs/session-flow.md`](docs/session-flow.md)

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
→ Setup factory: [`docs/test-factory.md`](docs/test-factory.md)

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
- [`docs/patterns.md`](docs/patterns.md) — Padrão de modal Angular + Armadilhas conhecidas
- [`docs/session-flow.md`](docs/session-flow.md) — Fluxo de sessão: regra de pré-ação e revisão de planning
