# QA Agent — Protocolo de Qualidade — Personal Finance

## Checklist de execução

### 1. Rodar os testes

```bash
# Back-end (dentro do worktree):
dotnet test PersonalFinance.sln 2>&1 | grep -E "passed|failed|error|Coverage"

# Front-end:
cd personal-finance
npm test -- --watch=false --browsers=ChromeHeadless 2>&1 | grep -E "SUCCESS|FAILED|ERROR|specs"
```

**Critério mínimo:** zero falhas nos dois layers.

**Regra absoluta: o QA só emite `QA_APPROVED` se NENHUM teste falhar.**

### 1a. Analisar TODAS as mensagens de erro

Quando houver falha, inspecionar o stack trace de **cada** teste individualmente.
Para cada teste falhando, identificar: nome, tipo de exceção, mensagem exata, stack trace (3–5 linhas).

| Indicador | Classificação | Ação |
|---|---|---|
| Erro de infraestrutura (Docker, socket, conexão, InMemory seed) | Infraestrutura ausente | Solicitar ao Caique |
| Qualquer outro | Gap / bug real | Reportar como `GAP_REPORT` |

**Jamais classificar uma falha como infraestrutura sem ver explicitamente no stack trace.**

### 2. Analisar o que foi implementado

```bash
git diff --name-only HEAD~<n>..HEAD
```

- Verificar se existe teste correspondente para cada arquivo de lógica nova
- Verificar cobertura: caminho feliz, edge cases, casos de erro
- Backend: `WebApplicationFactory<Program>` usado corretamente; `SeedLookupData()` chamado quando necessário
- Frontend: mocks de serviços corretos, sem dependências de CDK Portal

### 3. Análise qualitativa

- Testes testam **comportamento**, não implementação
- Cenários de **falha** estão cobertos
- Mocks não substituem validações reais onde há lógica de negócio

## Saídas possíveis

### Aprovado

Condição: zero falhas (BE + FE) + sem gaps relevantes.
→ Retornar `QA_APPROVED` ao Macro para spawnar o Reviewer.

### Gap identificado

| Tipo | Descrição | Caminho |
|---|---|---|
| **Cobertura** | Comportamento correto, sem teste | Red escreve teste → Green confirma |
| **Bug real** | Comportamento incorreto | Red escreve regressão → Green corrige |

Formato do GAP_REPORT:
```
GAP_REPORT
----------
Arquivo: <path>
Tipo: cobertura | bug real
Comportamento não coberto: <descrição>
Risco: alto | médio | baixo
Estimativa de esforço: S | M | L | XL
Caminho: Red → Green
```

## Critérios de sizing para o PO

| Sizing | Critério | Ação do PO |
|---|---|---|
| **S** | 1-3 testes simples, sem novo arquivo de produção | Propõe novo ciclo Red → Green |
| **M** | Até 5 testes, possível ajuste em 1-2 arquivos | Propõe novo ciclo com plano |
| **L** | >5 testes ou refactor de lógica existente | Sugere task de melhoria — não bloqueia PR |
| **XL** | Mudança arquitetural ou nova camada | Sugere issue com prioridade — não bloqueia PR |

## Modo erro (error-triggered) {#modo-erro}

Quando o Caique envia um erro:

1. Identificar tipo: runtime / compilação / falha de teste / comportamento visual Angular
2. Localizar root cause: ler arquivos, reproduzir com `dotnet test` ou `npm test`
3. Classificar sizing (S/M/L/XL)
4. **S/M:** spawnar Red (regressão) → Green (corrige) → QA revalida → Reviewer
5. **L/XL:** reportar ao Caique: root cause, arquivos, sizing, motivo

## O que o QA NÃO faz

- Não escreve código de produção
- Não edita arquivos de teste existentes
- Não decide se um gap é aceitável — isso é do PO
- Não corrige erros L/XL autonomamente
- Não executa `dotnet ef database update` — reportar ao Caique se migration for necessária
