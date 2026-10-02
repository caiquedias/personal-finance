# Project Memory — Personal Finance (MonkeyBomb)

Estado atual do sistema. Atualizado ao final de cada issue via `/end-issue`.

---

## Índice por issue

| Issue | Título | Data | Detalhe |
|---|---|---|---|
| #329 | [BE] Expurgo: Infrastructure + API | 2026-06-26 | [329.md](329.md) |
| #330 | [FE] Expurgo: MOD-01 — Tela de Expurgo | 2026-06-26 | [330.md](330.md) |
| #331 | [FE] Expurgo: MOD-02 — Análise Offline de CSV | 2026-06-26 | [331.md](331.md) |
| #332 | [FE] Expurgo: MOD-03 — Registro de Expurgos | 2026-06-26 | [332.md](332.md) |
| #355 | fix: enum string quebra deserialização no POST /expenses/batch/create | 2026-06-26 | [355.md](355.md) |
| #356 | [FE] Expurgo: Layout e Design — tabela padrão + modal Sonic | 2026-06-27 | [356.md](356.md) |
| #369 | Expurgo - Análise Detalhe (bugfix CSV parser) | 2026-06-29 | [369.md](369.md) |
| #367 | Expurgo - Layout Modal | 2026-06-29 | [367.md](367.md) |
| #368 | Expurgo - Análise: botão de navegação para tela de CSV | 2026-06-29 | [368.md](368.md) |
| #377 | Expurgo - Bug Grid "Histórico de Expurgos" | 2026-06-29 | [377.md](377.md) |
| #378 | Expurgo - Análise Detalhe | 2026-06-29 | [378.md](378.md) |
| #376 | Expurgo - Botão "Análise" | 2026-06-29 | [376.md](376.md) |
| #387 | Tela de Login - Remover seção de criação de usuário | 2026-07-04 | [387.md](387.md) |
| #389 | [Security] Remover JWT SecretKey hardcoded e rotacionar | 2026-07-04 | [389.md](389.md) |
| #384 | Expurgo - Análise Detalhe — Filtros por aba (padrão tela de Despesas) | 2026-07-05 | [384.md](384.md) |
| #419 | [Import] Update de Income — backend e frontend | 2026-09-28 | [419.md](419.md) |
| #420 | [Import] Parser PDF do extrato C6 Bank | 2026-09-28 | [420.md](420.md) |
| #421 | [Import] Preview de importação — classificação e duplicatas | 2026-09-28 | [421.md](421.md) |
| #422 | [Import] Confirmação de importação — persistência multi-período | 2026-09-29 | [422.md](422.md) |
| #423 | [Import] Frontend — upload e tela de revisão | 2026-09-29 | [423.md](423.md) |
| #443 | Redesign Extrato PDF (1/5): casca visual — dropzone e estados vazio/erro | 2026-10-01 | [443.md](443.md) |
| #444 | Redesign Extrato PDF (2/5): stepper + estado unificado + card de processamento | 2026-10-01 | [444.md](444.md) |
| #445 | Redesign Extrato PDF (3/5): cards de resumo + tabela de revisão | 2026-10-01 | [445.md](445.md) |
| #446 | Redesign Extrato PDF (4/5): barra de ação fixa + tela de sucesso | 2026-10-01 | [446.md](446.md) |
| #447 | Redesign Extrato PDF (5/5): polimento responsivo (<768px) | 2026-10-01 | [447.md](447.md) |
| #464 | Redesign grid de revisão — Import Extrato PDF (padrão despesas) | 2026-10-01 | [464.md](464.md) |
| #390 | [Security] Adicionar HTTPS redirection e HSTS | 2026-10-01 | [390.md](390.md) |
| #391 | [Security] Rate limiting e lockout de conta no login | 2026-10-02 | [391.md](391.md) |

---

## Índice por módulo/componente

| Módulo | Issues relacionadas | Última atualização |
|---|---|---|
| Expurgo (Purge) | #329, #330, #331, #332, #356, #369, #367, #368, #377, #376, #378, #384 | 2026-07-05 |
| Batch Expenses / Serialização | #355 | 2026-06-26 |
| Login / Auth UI | #387 | 2026-07-04 |
| Segurança / JWT | #389, #391 | 2026-10-02 |
| Import (Income) | #419 | 2026-09-28 |
| Import (Extrato C6 PDF) | #420, #421, #422, #423, #443, #444, #445, #446, #447, #464 | 2026-10-01 |

---

## Estado atual por layer

### Domain
- **Entidades:** User (#391 — `FailedLoginCount`, `LockedUntil`, `RowVersion`), Category, Period, Expense, Income, PurgeRecord, LoginThrottle (#391 — par conta+IP; fora do EntityBase, exclusão física)
- **Value objects / enums:** PaymentStatus, SourceType, FortnightType, Role
- **Regras notáveis:** soft-delete universal (DeletedAt; exceção deliberada: `LoginThrottle`, #391), PKs via Guid.NewGuid(); lockout com `now` injetado (`User.RegisterFailedLogin`, `IsLockedOut`; `LoginThrottle.RegisterFailure`)
- **Interfaces:** IPurgeRepository, ICsvExportService (Application layer), ILoginThrottleRepository (#391)
- **Exceções:** ConcurrencyConflictException (#391 — conflito de rowversion/índice único; vira 409 no ExceptionMiddleware)

### Application
- **Use cases:** ExportPeriodUseCase, PurgePeriodUseCase, GetPurgeRecordsUseCase, DeletePurgeRecordUseCase, GetEligiblePeriodsUseCase, UpdateIncomeUseCase (#419 — ownership 400 via DomainException, mesmo padrão do UpdateExpenseUseCase), ConfirmStatementImportUseCase (#422 — persiste extrato revisado; valida tudo antes, um CommitAsync, reativa Period soft-deleted), PreviewStatementImportUseCase (#421 — preview de extrato sem persistência; filtro `fromDate` por PostingDate, Receita/Despesa pelo sinal, categoria sugerida, transferência interna e duplicata por userId), StatementEntryClassifier (#421)
- **Interfaces:** IStatementParserService (#420 — parser de extrato PDF com senha; `ParseAsync(Stream, password, ct)`)
- **DTOs:** ConfirmStatementItemDto, ConfirmStatementImportRequestDto, ConfirmStatementImportResultDto (#422), StatementPreviewItemDto, StatementPreviewResultDto (#421 — Items + DiscardedByDateCount), ParsedStatementEntryDto (#420 — record: EventDate, PostingDate, RawType, Description, Amount com sinal), EligiblePeriodDto, PurgeRecordDto, UpdateIncomeDto (#419 — sem PeriodId, sem SourceType)
- **Use cases alterados:** GetPurgeRecordsUseCase — retorna `IEnumerable<PurgeRecordDto>` (antes `IEnumerable<PurgeRecord>`), mapeamento interno com `ItemCount = ExpenseCount + IncomeCount`
- **Use cases alterados:** LoginWithRolesUseCase (#391) — `ExecuteAsync(dto, ipAddress, ct)`; lockout por par (conta, IP) + teto global no User; Verify contra hash dummy em todos os caminhos de falha (inclusive inativo); mensagem sempre "Credenciais inválidas."; retry de 3 tentativas em `ConcurrencyConflictException`
- **Options:** LoginLockoutOptions (#391 — `Auth:LoginLockout`: MaxFailedAttempts 5, LockoutMinutes 15, GlobalMaxFailedAttempts 50, ThrottleMaxRows 2000, ThrottleCleanupBatchSize 500; validadas no startup)
- **Validações (FluentValidation):** —

### Infrastructure
- **Repositórios:** PurgeRepository, LoginThrottleRepository (#391 — `TryAddAsync`: limpeza de expiradas em lote, teto duro de 2.000 linhas, fail-open)
- **UnitOfWork (#391):** traduz `DbUpdateConcurrencyException` e violação de índice único (2601/2627) em `ConcurrencyConflictException` e limpa o `ChangeTracker` antes de lançar
- **Serviços:** Argon2PasswordHasher, JwtTokenService, ExcelParserService, C6StatementPdfParserService (#420 — PdfPig por coordenadas x/y, DomainException para senha/PDF/linha inválida; sem consumidor ainda), DatabaseInitializer, CsvExportService
- **Migrations aplicadas:** AddPurgeModule (2026-06-26); #391 (a aplicar em release/produção): AddUserLockoutFields, AddUserRowVersion, AddLoginThrottle
- **Views:** vw_PeriodSummary (criada pelo DatabaseInitializer no startup)

### Api
- **Endpoints ativos:**
  - GET /api/v1/purge/eligible-periods — retorna periodId, year, month, totalIncome, totalExpense, itemCount
  - GET /api/v1/purge/{periodId}/export — (era POST /purge/export/{periodId})
  - POST /api/v1/purge/{periodId} — requer { csvFileName } no body
  - GET /api/v1/purge/records — retorna `year`, `month`, `itemCount` (DTO, não entidade direta)
  - DELETE /api/v1/purge/records/{id}
  - POST /api/v1/import/statement/preview — multipart (file .pdf ≤10 MB, password?, fromDate?); devolve preview sem persistir; 400 para arquivo ausente/vazio, extensão inválida, senha errada ou PDF inválido (#421)
  - POST /api/v1/import/statement/confirm — body `{ items: [{ date, description, amount, kind, categoryId?, sourceType? }] }`; cria/reaproveita Period por Ano+Mês, grava Expense (Paid) e Income; 200 com contagens; 400 para lista vazia, data futura, categoria ausente/inacessível, Description > 200 (#422)
  - PUT /api/v1/incomes/{id} — update de receita; 204; 400 para amount inválido/ownership/not-found/soft-deleted; PeriodId imutável (#419)
  - POST /api/v1/auth/login — `[EnableRateLimiting("login")]`: 10/min por IP (`RateLimiting:Login`), 429 com `Retry-After` + JSON `{status,error,message,traceId}`; lockout genérico ("Credenciais inválidas.") por par (conta, IP) após 5 falhas/15 min e por conta após 50 (#391)
  - Qualquer endpoint: `ConcurrencyConflictException` → 409 "O registro foi alterado por outra operação. Tente novamente." (#391)
  - Pipeline (#390, #391): `UseForwardedHeaders` (XFF+XFP, só redes privadas 10/8, 172.16/12, 192.168/16 + loopback, `ForwardLimit=1`) → ExceptionMiddleware → `UseHsts` (só não-Development, sem Preload/IncludeSubDomains) → `UseHttpsRedirection` → CORS → `UseRateLimiter` (#391) → Auth
- **Auth:** JWT Bearer; AuthController [AllowAnonymous]; Admin [Authorize(Roles="Admin")]; `JwtSettings:SecretKey` não é mais hardcoded em `appsettings.json` — configurado via User Secrets (dev) / env var `JwtSettings__SecretKey` no Render (homolog/prod) (#389)
- **Converters:** `FlexibleEnumConverterFactory` registrada globalmente via `AddJsonOptions` — deserializa enums de int, string numérica ou nome; serializa como int

### Frontend (Angular 21)
- **Rotas (app.routes.ts):** `/purge` (lazy, authGuard); `purge/analysis` (PurgeAnalysisComponent, providers: [CsvReaderService]); `purge/analysis/detail` (PurgeDetailComponent)
- **Componentes standalone:** PurgeComponent redesenhado — cards grid, modal Sonic pixel-art, tabela histórico, modal delete, botão "Upload CSV" no header via ng-content (classe `btn-primary`, #368/#376) (`features/purge/components/purge/`); PurgeAnalysisComponent, PurgeDetailComponent, PurgeWarningBannerComponent (`features/purge/`) — `PurgeWarningBannerComponent` removido da tela principal em #367; permanece apenas em `purge-detail.component.ts`
- **Assets:** `public/sonic-tile.svg` (tile pixel-art do frame Sonic)
- **Serviços:** ApiService (wrapper HTTP) com métodos purge (`getEligiblePeriods`, `exportPurgeCsv`, `executePurge(periodId, csvFileName)`, `getPurgeRecords`, `deletePurgeRecord`), `updateIncome(id, data)` (#419); ThemeService (dark/light); CsvReaderService (parse CSV offline, sem `providedIn: 'root'`) — corrigido em #369 para 12 colunas, RFC 4180, enums como string
- **Componentes:** `PurgeDetailComponent` (#378, #384) — header, 3 abas (expenses/incomes/indicators), grid padronizado (.table/.table-wrap, badges, CurrencyBrlPipe, ícones de sort), KPIs (kpiTotalIncome, kpiTotalExpense, kpiTotalPaid, kpiTotalOwed, kpiBalance, kpiPaymentProgress). Filtros independentes por aba (#384): Despesas (expFilterDesc, expFortnight, expStatus, expSourceType, expFilterOpen, expFilterFields 4 campos, expActiveFilterCount) e Receitas (incFilterDesc, incFilterOpen, incFilterFields 1 campo, incActiveFilterCount); Indicadores sem filtro próprio
- **Componentes:** `IncomesComponent` — modo edit do modal faz update real via `updateIncome` (antes fazia delete+create); select de período desabilitado em modo edit, com hint visual (#419)
- **Modelos:** `PurgeRecordResponse` adicionado em models.ts; `UpdateIncomeRequest` adicionado (#419)
- **Sidebar:** item "Expurgo" com ícone `archive` e rota `/purge`
- **Auth:** authInterceptor injeta token automaticamente
-464:** grid de revisão no padrão de Despesas — `.table-wrap.card` > `.table` (regras copiadas para o CSS do componente; só `.card` é global), `col-hide-mobile` em Data e Avisos no <768px (substitui o `min-width: 680px` com scroll); débito: possível scroll interno em 375px (inputs `min-width: 90px`)$Login:** `LoginComponent` sem seção de cadastro — link `/register` e `RouterLink` removidos (#387)
- **Import (Extrato PDF, #423):** `/import` com abas "Legado | Extrato PDF" (`ImportComponent.activeTab`/`setTab`); `StatementImportComponent` (`features/import/components/statement-import/`) — upload .pdf ≤10 MB + senha + data de início nativa, tabela editável, badges de transferência interna/duplicata (só sinalizam), `save()` limpa `items` após sucesso. **#443:** casca visual dos estados vazio/erro — dropzone `.pdf-dropzone` com arraste (`isDragging`, `onDragOver`/`onDragLeave`/`onDrop`, validação comum em `acceptFile`), card `.pdf-file-card`, estado `.has-error` derivado (`errorMessage() && items().length === 0 && !result()`). **#444:** computed `state` (`empty|proc|preview|done|err`, precedência proc>done>err>preview>empty) que alimenta stepper `.stepper` e card `.proc-card`; `simulatedProgress` é ticker cosmético (teto 90%, 100% ao fim da request, limpo no destroy). **#445:** cards `.summary-cards` (Lançamentos/Despesas/Receitas/Precisam de atenção) via computeds `expenseItems/incomeItems/expenseSum/incomeSum/pendingCount/warnCount/attentionCount` (união) e `discardedSinceLabel` (regex, sem `Date`); tabela com pills `.pill-*`, `.select-pending` (âmbar) e `.cell-input` (edição inline preservada). `ApiService.previewStatementImport`/`confirmStatementImport`; models `StatementPreviewItem`, `StatementPreviewResult`, `ConfirmStatementItemRequest`, `ConfirmStatementImportRequest`, `ConfirmStatementImportResult`. **#446:** barra sticky `.action-bar` (só em `state()==='preview'`; `targetPeriodLabel` informativo, botão "Importar N lançamentos" = `save()` inalterado) e tela `.success-screen` (cards Despesas/Receitas/Períodos, ações `/periods`, `/expenses`, `importAnother()` com `resetDone`). **#447:** bloco `@media (max-width: 767px)` no CSS do componente (tabela `min-width: 680px` com scroll no `.table-wrapper`, `.action-bar` em coluna, cards em 2 colunas, stepper/`.pdf-file-card` compactos); débito: `.shell-main` `overflow-x: hidden` provavelmente impede o sticky da `.action-bar` **#464:** grid de revisão no padrão de Despesas — `.table-wrap.card` > `.table` (regras copiadas para o CSS do componente; só `.card` é global), `col-hide-mobile` em Data e Avisos no <768px (substitui o `min-width: 680px` com scroll); débito: possível scroll interno em 375px (inputs `min-width: 90px`). **#464 (ciclo 2):** `ImportComponent` `.page-content` sem `max-width: 760px` (agora `flex: 1`), largura total nas duas abas; `.pdf-intro` segue com `max-width: 700px`

### Banco de dados
- **Lookup tables seeded:** Role, PaymentStatus, SourceType, FortnightType
- **Tabelas principais:** Users (#391 — +FailedLoginCount default 0, LockedUntil, RowVersion), Categories, Periods, Expenses, Incomes, PurgeRecords, LoginThrottles (#391 — índice único (UserId, IpAddress), FK Restrict, sem soft-delete)
