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
| #376 | Expurgo - Botão "Análise" | 2026-06-29 | [376.md](376.md) |

---

## Índice por módulo/componente

| Módulo | Issues relacionadas | Última atualização |
|---|---|---|
| Expurgo (Purge) | #329, #330, #331, #332, #356, #369, #367, #368, #377, #376 | 2026-06-29 |
| Batch Expenses / Serialização | #355 | 2026-06-26 |

---

## Estado atual por layer

### Domain
- **Entidades:** User, Category, Period, Expense, Income, PurgeRecord
- **Value objects / enums:** PaymentStatus, SourceType, FortnightType, Role
- **Regras notáveis:** soft-delete universal (DeletedAt), PKs via Guid.NewGuid()
- **Interfaces:** IPurgeRepository, ICsvExportService (Application layer)

### Application
- **Use cases:** ExportPeriodUseCase, PurgePeriodUseCase, GetPurgeRecordsUseCase, DeletePurgeRecordUseCase, GetEligiblePeriodsUseCase
- **DTOs:** EligiblePeriodDto, PurgeRecordDto
- **Use cases alterados:** GetPurgeRecordsUseCase — retorna `IEnumerable<PurgeRecordDto>` (antes `IEnumerable<PurgeRecord>`), mapeamento interno com `ItemCount = ExpenseCount + IncomeCount`
- **Validações (FluentValidation):** —

### Infrastructure
- **Repositórios:** PurgeRepository
- **Serviços:** Argon2PasswordHasher, JwtTokenService, ExcelParserService, DatabaseInitializer, CsvExportService
- **Migrations aplicadas:** AddPurgeModule (2026-06-26)
- **Views:** vw_PeriodSummary (criada pelo DatabaseInitializer no startup)

### Api
- **Endpoints ativos:**
  - GET /api/v1/purge/eligible-periods — retorna periodId, year, month, totalIncome, totalExpense, itemCount
  - GET /api/v1/purge/{periodId}/export — (era POST /purge/export/{periodId})
  - POST /api/v1/purge/{periodId} — requer { csvFileName } no body
  - GET /api/v1/purge/records — retorna `year`, `month`, `itemCount` (DTO, não entidade direta)
  - DELETE /api/v1/purge/records/{id}
- **Auth:** JWT Bearer; AuthController [AllowAnonymous]; Admin [Authorize(Roles="Admin")]
- **Converters:** `FlexibleEnumConverterFactory` registrada globalmente via `AddJsonOptions` — deserializa enums de int, string numérica ou nome; serializa como int

### Frontend (Angular 21)
- **Rotas (app.routes.ts):** `/purge` (lazy, authGuard); `purge/analysis` (PurgeAnalysisComponent, providers: [CsvReaderService]); `purge/analysis/detail` (PurgeDetailComponent)
- **Componentes standalone:** PurgeComponent redesenhado — cards grid, modal Sonic pixel-art, tabela histórico, modal delete, botão "Upload CSV" no header via ng-content (classe `btn-primary`, #368/#376) (`features/purge/components/purge/`); PurgeAnalysisComponent, PurgeDetailComponent, PurgeWarningBannerComponent (`features/purge/`) — `PurgeWarningBannerComponent` removido da tela principal em #367; permanece apenas em `purge-detail.component.ts`
- **Assets:** `public/sonic-tile.svg` (tile pixel-art do frame Sonic)
- **Serviços:** ApiService (wrapper HTTP) com métodos purge (`getEligiblePeriods`, `exportPurgeCsv`, `executePurge(periodId, csvFileName)`, `getPurgeRecords`, `deletePurgeRecord`); ThemeService (dark/light); CsvReaderService (parse CSV offline, sem `providedIn: 'root'`) — corrigido em #369 para 12 colunas, RFC 4180, enums como string
- **Componentes:** `PurgeDetailComponent` (#369) — tabela Income com 4 colunas (Descrição, Valor, Período, Notas); coluna Quinzena removida
- **Modelos:** `PurgeRecordResponse` adicionado em models.ts
- **Sidebar:** item "Expurgo" com ícone `archive` e rota `/purge`
- **Auth:** authInterceptor injeta token automaticamente

### Banco de dados
- **Lookup tables seeded:** Role, PaymentStatus, SourceType, FortnightType
- **Tabelas principais:** Users, Categories, Periods, Expenses, Incomes, PurgeRecords
