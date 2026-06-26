# Project Memory — Personal Finance (MonkeyBomb)

Estado atual do sistema. Atualizado ao final de cada issue via `/end-issue`.

---

## Índice por issue

| Issue | Título | Data | Detalhe |
|---|---|---|---|
| #329 | [BE] Expurgo: Infrastructure + API | 2026-06-26 | [329.md](329.md) |
| #330 | [FE] Expurgo: MOD-01 — Tela de Expurgo | 2026-06-26 | [330.md](330.md) |

---

## Índice por módulo/componente

| Módulo | Issues relacionadas | Última atualização |
|---|---|---|
| Expurgo (Purge) | #329, #330 | 2026-06-26 |

---

## Estado atual por layer

### Domain
- **Entidades:** User, Category, Period, Expense, Income, PurgeRecord
- **Value objects / enums:** PaymentStatus, SourceType, FortnightType, Role
- **Regras notáveis:** soft-delete universal (DeletedAt), PKs via Guid.NewGuid()
- **Interfaces:** IPurgeRepository, ICsvExportService (Application layer)

### Application
- **Use cases:** ExportPeriodUseCase, PurgePeriodUseCase, GetPurgeRecordsUseCase, DeletePurgeRecordUseCase
- **DTOs:** —
- **Validações (FluentValidation):** —

### Infrastructure
- **Repositórios:** PurgeRepository
- **Serviços:** Argon2PasswordHasher, JwtTokenService, ExcelParserService, DatabaseInitializer, CsvExportService
- **Migrations aplicadas:** AddPurgeModule (2026-06-26)
- **Views:** vw_PeriodSummary (criada pelo DatabaseInitializer no startup)

### Api
- **Endpoints ativos:**
  - GET /api/v1/purge/eligible-periods
  - POST /api/v1/purge/export/{periodId}
  - POST /api/v1/purge/{periodId}
  - GET /api/v1/purge/records
  - DELETE /api/v1/purge/records/{id}
- **Auth:** JWT Bearer; AuthController [AllowAnonymous]; Admin [Authorize(Roles="Admin")]

### Frontend (Angular 21)
- **Rotas (app.routes.ts):** `/purge` (lazy, authGuard)
- **Componentes standalone:** PurgeComponent (`features/purge/components/purge/`)
- **Serviços:** ApiService (wrapper HTTP) com métodos purge (`getEligiblePeriods`, `exportPurgeCsv`, `executePurge`); ThemeService (dark/light)
- **Sidebar:** item "Expurgo" com ícone `archive` e rota `/purge`
- **Auth:** authInterceptor injeta token automaticamente

### Banco de dados
- **Lookup tables seeded:** Role, PaymentStatus, SourceType, FortnightType
- **Tabelas principais:** Users, Categories, Periods, Expenses, Incomes, PurgeRecords
