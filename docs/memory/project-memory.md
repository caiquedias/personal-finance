# Project Memory — Personal Finance (MonkeyBomb)

Estado atual do sistema. Atualizado ao final de cada issue via `/end-issue`.

---

## Índice por issue

| Issue | Título | Data | Detalhe |
|---|---|---|---|
| — | — | — | — |

---

## Índice por módulo/componente

| Módulo | Issues relacionadas | Última atualização |
|---|---|---|
| — | — | — |

---

## Estado atual por layer

### Domain
- **Entidades:** User, Category, Period, Expense, Income
- **Value objects / enums:** PaymentStatus, SourceType, FortnightType, Role
- **Regras notáveis:** soft-delete universal (DeletedAt), PKs via Guid.NewGuid()

### Application
- **Use cases:** —
- **DTOs:** —
- **Validações (FluentValidation):** —

### Infrastructure
- **Repositórios:** —
- **Serviços:** Argon2PasswordHasher, JwtTokenService, ExcelParserService, DatabaseInitializer
- **Migrations aplicadas:** —
- **Views:** vw_PeriodSummary (criada pelo DatabaseInitializer no startup)

### Api
- **Endpoints ativos:** —
- **Auth:** JWT Bearer; AuthController [AllowAnonymous]; Admin [Authorize(Roles="Admin")]

### Frontend (Angular 21)
- **Rotas (app.routes.ts):** —
- **Componentes standalone:** —
- **Serviços:** ApiService (wrapper HTTP), ThemeService (dark/light)
- **Auth:** authInterceptor injeta token automaticamente

### Banco de dados
- **Lookup tables seeded:** Role, PaymentStatus, SourceType, FortnightType
- **Tabelas principais:** Users, Categories, Periods, Expenses, Incomes
