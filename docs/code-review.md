# Code Review — Personal Finance

Executado pelo sub-agente **Reviewer** ao final de cada issue, antes do PR.

## Checklist de qualidade

### Estrutura e arquitetura (Clean Architecture)

- [ ] Fluxo correto: `Api → Infrastructure → Application → Domain`
- [ ] Um arquivo por classe — sem exceção
- [ ] Nenhuma lógica de negócio em Controllers — apenas orquestração
- [ ] Use cases em `Application` — um por classe
- [ ] Interfaces no `Domain` (exceto `IReportRepository` que fica em `Application`)
- [ ] Interfaces nunca reescritas — apenas `str_replace` para adicionar métodos
- [ ] PKs: `Guid.NewGuid()` no Domain, `ValueGeneratedNever()` no EF Core
- [ ] Enums: `.HasConversion<int>()` | FKs: `OnDelete(Restrict)`

### Código backend (.NET 8)

- [ ] `async` não usa `ref`/`out` — retorna tuple `(T value, bool flag)` se necessário
- [ ] Sem concatenação SQL — apenas parâmetros tipados
- [ ] Soft-delete respeitado: `DeletedAt` + `HasQueryFilter` global
- [ ] FluentValidation em use cases de escrita
- [ ] Sem lógica de negócio duplicada entre classes

### Código frontend (Angular 21)

- [ ] Standalone components — sem NgModules
- [ ] `DecimalPipe` importado explicitamente em cada componente que usa
- [ ] Estado local via Signals — sem `BehaviorSubject` para estado de componente
- [ ] HTTP apenas via `ApiService` — sem `HttpClient` direto em componentes
- [ ] Modal: `@if` + Angular Animations — sem CDK Portal/OverlayRef
- [ ] Sem `any` no TypeScript
- [ ] `authInterceptor` não duplicado — apenas injetado via `provideHttpClient`

### Testes

- [ ] Toda feature nova tem testes (xUnit backend + Jasmine frontend)
- [ ] Red → Green → Refactor respeitado
- [ ] Testes cobrem: caminho feliz, edge cases e cenários de falha
- [ ] `MarkAsPaid` com data futura → usar `DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))`
- [ ] `SeedLookupData()` chamado na factory quando necessário (não `HasData`)

### Segurança

- [ ] Nenhuma credencial em arquivo versionado
- [ ] `.env` não comitado
- [ ] CORS sem origens abertas
- [ ] Nenhuma concatenação de SQL — apenas parâmetros tipados
- [ ] Inputs validados antes de processar
- [ ] Endpoints sensíveis com `[Authorize]` — `AuthController` explicitamente `[AllowAnonymous]`
- [ ] Admin endpoints com `[Authorize(Roles="Admin")]`

## Severidade e ação

| Severidade | Critério | Ação |
|---|---|---|
| **Crítica** | Leak de credencial, SQL injection, endpoint sem auth | Bloquear PR — corrigir antes |
| **Alta** | Lógica em camada errada, `any` no TS, CORS aberto, CDK Portal | Corrigir na mesma sessão |
| **Média** | Código duplicado, falta cobertura de edge case | Corrigir se no escopo |
| **Baixa** | Nomes inconsistentes, comentário desatualizado | Tech debt — não bloqueia PR |

## Output esperado do Reviewer

```
## Code Review — #<issue-id>

### ✅ Aprovado
- <lista do que está correto>

### ⚠️ Issues encontradas
| Severidade | Arquivo | Descrição | Ação |
|---|---|---|---|

### Resultado: APROVADO | REQUER CORREÇÃO
```
