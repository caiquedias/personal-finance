# Testing — Personal Finance

Protocolo geral de TDD. Setup do `TestWebApplicationFactory` está em
[`docs/test-factory.md`](test-factory.md); padrões Angular em [`docs/patterns.md`](patterns.md).

## Ciclo TDD

```
Red   → escrever todos os testes (falham)
Green → implementar o mínimo para passar
Refactor → limpar, sem alterar comportamento
```

- Red primeiro — nunca escrever implementação antes dos testes
- Um arquivo de teste por classe
- Cobertura obrigatória: caminho feliz, edge cases, cenários de falha

## Idioma da saída do runner

`grep -E "passed|failed"` (backend) e `grep -E "SUCCESS|FAILED"` (frontend) assumem runner em
inglês. Se o `dotnet test` sair localizado (pt-BR: "Aprovado!"/"Com falha"), o grep devolve linha
nenhuma e o agente lê "sem falhas" num resultado que nem viu. **Grep vazio = erro de comando até
prova em contrário, nunca suíte verde.** Se acontecer, fixar `DOTNET_CLI_UI_LANGUAGE=en` no comando.

## Testes de dados derivados/recalculados

Sempre que a lógica testada regenera ou recalcula um valor persistido (parcelas, rateios, saldo,
qualquer campo derivado de fórmula), assertar o **valor** produzido — `Assert.Equal(n,
resultado.Count)` sozinho não pega regressão de arredondamento. Incluir sempre um caso de
arredondamento não-exato.

## Fallback em múltiplos níveis

Quando a lógica tem mais de um nível de fallback (flag decide entre caminho A e B; dentro de A, um
segundo fallback próprio), cobrir a matriz completa de combinações de falha — "nível 1 falha E o
fallback do nível 2 também falha" é o tipo de caso que passa despercebido sem desenhar a matriz 2×2.

## Mappers domínio ↔ persistência

O mapper entre entidade de domínio e modelo EF é unit-testável sem banco — essa é a principal razão
da separação existir. Testar os dois sentidos, incluindo round-trip e campo nullable. Se o teste do
mapper precisou de banco, há lógica de persistência vazando para dentro dele.

## Asserção sobre request capturado em mock — snapshot, não referência

Se o teste guarda a **referência** de um objeto passado a um mock e inspeciona depois do `await`,
pode ler o objeto já mutado por código que roda depois. Capturar um **snapshot** dos campos que
interessam dentro do callback, ou usar matcher avaliado em tempo de chamada
(`Verify(x => x.Op(It.Is<T>(r => ...)), Times.Once)`).

## Testes de middleware que dependem de host (HSTS, redirect)

O `HttpClient` da `WebApplicationFactory` usa Host `localhost`, e `HstsOptions.ExcludedHosts` exclui
`localhost`/`127.0.0.1`/`[::1]` por padrão — o header HSTS nunca sai. Em testes que esperam
`Strict-Transport-Security`, enviar `Host: api.example.com` na request; não limpar `ExcludedHosts`
(enfraqueceria o teste e a config). (#390)

O rate limiter do login (`RateLimiting:Login:PermitLimit`/`WindowSeconds`, default 10/60s por IP) usaria
o mesmo "IP" nulo (`unknown`) para toda a suíte — `GetAdminAuthenticatedClientAsync()` loga dezenas de
vezes. Por isso o construtor estático da `TestWebApplicationFactory` seta
`RateLimiting__Login__PermitLimit=100000` via variável de ambiente. Para testar o 429, usar
`factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Login:PermitLimit", "2"))` — o limiter lê a
config de forma lazy (ao criar a partição), então o override vale. Não baixar o limite global da factory. (#391)

## Isolamento de falha por item

Se a regra é "falha em 1 registro não aborta o lote", o teste deve provar que, com 1 registro
falhando no meio, os demais seguiram processados e o registro falho recebeu o status de erro
correspondente.

## Qual escopo rodar por tipo de mudança (QA)

| Mudança em... | Escopo obrigatório | Por quê |
|---|---|---|
| Repositório / EF Core / migration | **Integration** (`WebApplicationFactory` + InMemory) | único escopo que valida contra o `DbContext` real |
| Use case, Validator, lógica de negócio pura | **Unit** já basta | mais rápido, sem custo de factory |
| `package.json` / `package-lock.json` (npm) | `npm ci` limpo em container Linux (`docker run --rm -v <dir com os 2 arquivos>:/app -w /app node:20 npm ci`) além de build e testes | lockfile gerado no Windows omite deps opcionais de outras plataformas e `npm audit fix` pode desalinhar `@angular/*`; `npm ci --dry-run` local não reproduz (#406) |
| Stack de CSS/build (Tailwind, PostCSS, builder, tema) | Inspecionar o **CSS compilado** (sem diretivas literais, utilitários gerados, tokens sem colisão) | build e testes passam mesmo com o CSS quebrado (#406) |

Rodar Integration sem nenhum arquivo de repositório/migration no diff não adiciona cobertura.

## Isolamento de contexto no Red

**O que o Red pode receber:** acceptance criteria e edge cases do planning; interfaces/contratos —
somente assinaturas; casos de erro esperados; caminho de arquivo de contexto compartilhado (nunca o
texto colado).

**O que o Red NÃO pode receber:** implementações existentes similares; arquivos de teste existentes
como modelo; qualquer detalhe de como a implementação será feita.

## InMemory não suporta rowversion

O provider InMemory do EF Core não gera nem valida `rowversion`: conflitos de concorrência otimista
por token (`User.RowVersion`, #391) não são reproduzíveis em testes de integração. Cobrir o retry
com unit test do use case (mock de `IUnitOfWork` lançando `ConcurrencyConflictException`) e a
tradução `DbUpdateConcurrencyException` → `ConcurrencyConflictException` no `UnitOfWorkTests`
(conflito simulado por remoção da linha). O comportamento real do rowversion só é validável em SQL Server.

## InMemory não valida índice único

O InMemory também ignora índices únicos: a violação do índice (UserId, IpAddress) de `LoginThrottle`
em inserts concorrentes do mesmo par não é reproduzível em testes de integração. O `UnitOfWork`
converte a violação de índice único (SQL Server 2601/2627) em `ConcurrencyConflictException`, que
entra no retry do `LoginWithRolesUseCase`; essa conversão só é validável em SQL Server. O retry em si
é coberto por unit test do use case (mock do `IUnitOfWork`).

## Falha suspeita de ser pré-existente

Antes de classificar uma falha como pré-existente, confirmar na base com worktree descartável (limpeza
garantida, sem resíduo em `.claude/worktrees/`):

```bash
bash scripts/run-test-on-base.sh tests/<Projeto>.Tests "FullyQualifiedName~<NomeDoTeste>" [origin/develop]
```

Exit code = o do `dotnet test` na base (≠ 0 → a falha existe na base).

## Testes que montam fórmulas/strings numéricas — cultura invariante

Interpolar `decimal`/`double` em string de fórmula ou payload (`$"=SUM({valor})"`) usa a cultura da máquina:
em pt-BR vira `SUM(9739,80)` (dois argumentos, resultado 9819) e o teste falha só fora do CI (#401,
`ExcelParserServiceTests.AddSheet`). Formatar sempre com `ToString(CultureInfo.InvariantCulture)` ou gravar o
número direto na célula (`Cell.Value = (double)valor`).

## Testes dependentes de relógio (TOTP e janelas de tempo)

Nunca recalcular um valor derivado de `DateTime.UtcNow` (código TOTP, expiração) depois de um setup
lento: o fluxo de integração (Argon2, register, login) leva ~20s e cruza a fronteira de 30s do time
step, fazendo o teste passar ou falhar conforme o instante da execução (#393: replay do enable).
Guardar o valor usado na etapa anterior (`MfaUserContext.EnableCode`) e reaproveitá-lo; para "step
seguinte" usar offset +30s, sempre dentro da janela ±1 do servidor. Preferir relógio injetado no unit test.

## Fixtures devem refletir o comportamento real

Antes de asserir sobre dados criados por um helper de fixture (roles, claims, defaults), conferir no
código de produção o que o caminho usado realmente grava — ex.: `RegisterUserUseCase` não atribui role
(só `CreateUserByAdminUseCase` atribui "User"), então um JWT de usuário registrado não tem claim de role (#393).
