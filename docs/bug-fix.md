# Bug Fix Explícito — Personal Finance

Acionado quando o Caique cola um erro e solicita planejamento fora de uma sessão ativa.

## Fluxo

```
Macro Agent
 ├── Analisa root cause + identifica branch alvo
 │   ├── PR aberto em feat/ → fix vai no mesmo branch (novo worktree se necessário)
 │   └── PR mergeado → novo branch fix/<id>-bugfix-<slug> a partir de develop
 ├── Apresenta plano: causa + arquivos + sizing
 ├── Aguarda confirmação do Caique
 ├── Spawna Implementador Red (teste de regressão)
 │   └── Escreve teste falhando → commita
 ├── Spawna Implementador Green (corrige o bug)
 │   └── Fix → Green → commita → push
 ├── Spawna QA → Reviewer
 └── PR existente se atualiza ou novo PR criado
```

## Tipos de bug por layer

| Layer | Ferramenta de diagnóstico | Comando |
|---|---|---|
| Backend runtime | dotnet test | `dotnet test PersonalFinance.sln 2>&1` |
| Backend compilação | dotnet build | `dotnet build PersonalFinance.sln 2>&1` |
| Frontend runtime | npm test | `cd personal-finance && npm test -- --watch=false --browsers=ChromeHeadless 2>&1` |
| Frontend build | npm build | `cd personal-finance && npm run build 2>&1` |
| Migration | EF Core | Reportar ao Caique — não executar `dotnet ef database update` autonomamente |

## Antes de classificar como bug

Nem toda falha é bug de código. Cada linha abaixo economiza um ciclo Red→Green inteiro:

| Sintoma | Causa provável antes de ser bug |
|---|---|
| `grep` de teste voltou vazio / "sem falhas" suspeito | Comando de teste falhou por outro motivo — `grep` vazio nunca é suíte verde |
| Erro de recurso inexistente em teste de integração | `SeedLookupData()` não foi chamado na factory (InMemory não popula `HasData`) |
| Guardrail de Bash bloqueando comando legítimo | `pre-bash.sh` é fail-closed por design — ver `docs/agents.md` |
| Falha de autenticação em serviço externo | Variável de ambiente/segredo ausente no ambiente local |
| Campo obrigatório vindo nulo da origem externa | Pendência aberta de especificação, não bug — escalar ao Caique |

Um "bug" que na verdade é pendência de especificação **não** vira ciclo Red→Green: vira pergunta ao
Caique.

## Regra de escopo

O fix autônomo (S/M) toca apenas arquivos relacionados ao erro.
Qualquer mudança além disso → parar e consultar o Caique.

## Branches para bug fix

- Bug em feature não mergeada: criar worktree a partir da `feat/` existente
- Bug em produção (master): `hotfix/<id>-<slug>` a partir de `master`, PR para `master` + `develop`
- Bug em develop: `fix/<id>-<slug>` a partir de `develop`, mesmo fluxo de feature
