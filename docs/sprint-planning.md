# Sprint Planning — Protocolo

## Visão geral

O planning acontece **antes** da implementação e é responsabilidade do Macro Agent com suporte do PO.
Nenhuma linha de código é alterada durante o planning.

---

## Pré-requisito

```bash
gh auth refresh -h github.com -s project  # só se token não tiver escopo project
```

---

## Fluxo do planning (`/start-issue`)

### 1. Leitura da issue
```bash
gh issue view <number> --repo caiquedias/personal-finance --json number,title,body,labels
```

### 2. Análise de Impacto (PO — Modo 1)

O Macro Agent spawna o PO **antes** de apresentar qualquer plano ao Caique.
O PO responde com `ANÁLISE CONCLUÍDA` contendo:

| Dimensão | O que avaliar |
|---|---|
| **Dependências afetadas** | Componentes, serviços, rotas, controllers, use cases tocados |
| **Pacotes/versões** | Dependências npm/NuGet que precisam atualizar (se aplicável) |
| **Risco de regressão** | Nível (Baixo/Médio/Alto), o que pode quebrar, testes existentes na área |

O resultado é incorporado ao plano apresentado ao Caique como item fixo.

### 3. Apresentação do plano ao Caique

O plano apresentado inclui **6 itens fixos**:
1. Setup de branches e worktree
2. Análise de Impacto — resumo do resultado do PO
3. Red — testes falhando
4. Green — implementação task a task
5. QA → UX Validator (se frontend) → Reviewer
6. Push + PR + board

### 4. Aguardar confirmação do Caique antes de qualquer ação externa

---

## Planejamento de issue no board (sprint planning clássico)

Para issues ainda no `Backlog` que precisam ser planejadas para uma sprint:

1. Listar issues: `gh issue list --state open --limit 50 --json number,title,body,labels --repo caiquedias/personal-finance`
2. Explorar codebase — identificar o que existe e o que falta implementar
3. Para cada issue, definir: Estimativa (h), Prioridade, Size (XS/S/M/L/XL), arquivos afetados
4. **Issues L/XL ou com >15 arquivos afetados → propor divisão em sub-issues antes de iniciar**
5. Postar comentário de planejamento na issue:
   ```
   ## 📋 Sprint Planning
   **Estimativa:** Xh | **Prioridade:** N | **Size:** XS/S/M/L/XL | **Risco:** Baixo/Médio/Alto
   ---
   **Backend** — o que criar/alterar
   **Frontend** — o que criar/alterar
   ### Arquivos afetados
   - lista de arquivos
   ```
6. Atualizar campos no board (Status → Ready, Size, Priority, Estimate)

### Issues válidas para planning
Planejar apenas issues com **Status = Backlog** no board — ignorar as demais.

### Critério de sizing

Sizing por **nº de arquivos afetados** (contar o arquivo de teste junto com o de produção — 1 teste
por classe é regra obrigatória, não só a feature).

| Size | Arquivos criados/alterados | Ação de planejamento |
|---|---|---|
| XS | 1–2 | — |
| S | 3–7 | — |
| M | 8–14 | — |
| L | 15–25 | **obrigatoriamente dividida em sub-issues** |
| XL | 25+ | **dividir em mais issues** |

> Size decide divisão de escopo, não estimativa de tempo — ver "Fórmula de Estimativa" abaixo.

> **Infra base antes do sizing:** em issue "filha" (ex.: `Parte de #N`), conferir se a infra que ela
> assume (pacote, registro DI, middleware, ponto de invocação) já existe no código. Se não existir,
> contá-la no sizing ou criar issue de base antes. Caso real: #396 foi estimada M (~8-10 arquivos) e
> entregou 35 porque o FluentValidation não existia no projeto.

---

## Fórmula de Estimativa

> **Última calibração:** 2026-10-02 · n=12 issues com codificação medida
> **Origem dos números:** amostra local (n=12, 10 issues novas desde 2026-09-28). Fórmula sugerida pelo
> script: codificação mediana 4min (p75 6min, p90 6min) + piso de orquestração 4min = 8min por issue;
> sem correlação entre nº de arquivos e tempo (Pearson −0,015; Spearman 0,231) — Size segue só para
> divisão de escopo. Maioria das issues com 0 ciclos de retrabalho (8 de 12). Tratar como indicativo.

Até a primeira calibração, `Estimativa: Xh` do comentário de planning continua sendo um número
definido manualmente por Caique/PO — **não** deriva do Size (correlação entre nº de arquivos e
tempo de codificação costuma ser fraca; Size serve só para disparar divisão em L/XL).

O `/end-issue` mede, a cada issue, duração de codificação e duração de sessão completa
(`docs/memory/<ISSUE-NUMBER>.md` → "Análise de eficiência da sessão") e chama
`node scripts/calibrate-estimates.js --check` para avisar quando a amostra local já sustenta
recalibrar a fórmula (codificação mediana + piso de orquestração). Rodar o script sem `--check`
mostra o relatório completo (correlações, medianas por bucket, duração por nº de ciclos de
retrabalho).

**Como registrar uma recalibração:** trocar a linha "Última calibração" acima e deixar rastro em
"Decisões fechadas fora da spec" (data, valores antes → depois, leitura do desvio).

---

## IDs fixos (Project #2)

| Campo | Field ID | Opções |
|-------|----------|--------|
| Status | `PVTSSF_lAHOAOhFlc4BUMJ_zhBWAHQ` | Backlog `f75ad846` · Ready `61e4505c` · In progress `47fc9ee4` · In review `df73e18b` · Done `98236657` |
| Priority | `PVTSSF_lAHOAOhFlc4BUMJ_zhBWAPM` | P0 `79628723` · P1 `0a877460` · P2 `da944a9c` |
| Size | `PVTSSF_lAHOAOhFlc4BUMJ_zhBWAPQ` | XS `6c6483d2` · S `f784b110` · M `7515a9f1` · L `817d0097` · XL `db339eb2` |
| Estimate | `PVTF_lAHOAOhFlc4BUMJ_zhBWAPU` | número (horas) |

**Project ID:** `PVT_kwHOAOhFlc4BUMJ_`

---

## Armadilhas conhecidas do gerenciador de issues (GitHub)

Seção viva: registrar aqui toda armadilha descoberta em runtime, com como detectar e como prevenir.

- **`gh project item-edit` exige o item ID do projeto (`PVTI_...`), não o número da issue** — obter
  via GraphQL (`gh api graphql`) antes de qualquer update de campo do board.

---

## Decisões técnicas pendentes / débitos conhecidos

Nenhuma delas vira issue autônoma no board — são task/comentário da feature correspondente, quando
ela for planejada.

| Issue | Decisão | Motivo | Bloqueia |
|---|---|---|---|
| #391 | IP > 45 chars faz `LoginThrottle.Create` lançar `DomainException("IP inválido.")` após senha errada, devolvendo mensagem diferente de "Credenciais inválidas." (oráculo). Mitigação futura: truncar/normalizar o IP no controller | Review do ciclo 3 | Não |
| #391 | O `Verify` real do Argon2 é refeito a cada tentativa do retry de concorrência (custo de CPU) | Review do ciclo 3 | Não |

---

## Débitos de tooling identificados em review

Achados não-bloqueantes do Reviewer que ainda não têm feature relacionada para virar task — ficam
aqui até que uma issue de tooling/infra justifique abrir work item.

| Data | Achado | Origem | Ação sugerida / status |
|---|---|---|---|
| 2026-10-02 | #391 resolvido no ciclo 2: ForwardedHeaders só de redes privadas, rowversion + retry no contador, Verify dummy (inexistente/bloqueado/inativo), validação das options no startup, Retry-After no 429, lockout por (conta, IP) com teto global | #391 | Resolvido |
| 2026-10-02 | Risco residual: atacante com MUITOS IPs ainda pode atingir o teto global (`GlobalMaxFailedAttempts`, default 50) e bloquear a conta da vítima por `LockoutMinutes` | #391 | Aceito; mitigação futura: CAPTCHA/notificação ao usuário |
| 2026-10-02 | Risco residual: contador global do `User` sem coluna de janela — zera só no sucesso ou após expirar o lockout | #391 | Aceito |
| 2026-10-02 | Risco residual: tabela de throttle cheia de bloqueios ativos → rastreio por (conta, IP) em fail-open (só teto global) | #391 | Aceito (fail-closed permitiria negar login a todos) |
| 2026-10-02 | `/mfa/disable` e `/mfa/enable` sem rate limit nem contagem de falhas no lockout — token completo roubado pode tentar senha sem limite (disable ainda exige senha E código) | #393 | Média — policy de rate limit + contagem de falhas |
| 2026-10-02 | `code` sem limite de tamanho antes do Argon2 em Verify/Disable (até 10 verificações por falha) | #393 | Baixa — rejeitar `code` > 16 chars antes de hashear |
| 2026-10-02 | FE MFA: build avisa `qrcode` não-ESM (`allowedCommonJsDependencies`); `package.json` com reordenação cosmética de chaves; 400 do `/mfa/setup` exibe erro acima do card "ativo"; confirmar que o interceptor ignora 401 em `/auth/` no verify; `as any` no spy do `qr-code.service.spec` | #394 | Baixa — resolver junto da próxima issue de auth/tooling FE |
| 2026-10-02 | `Unprotect` com chave rotacionada/secret corrompido vira 500 no verify/disable | #393 | Baixa — erro controlado + procedimento de reset (#479) |
| 2026-10-02 | Respostas de `/mfa/setup` e `/mfa/enable` sem `Cache-Control: no-store` | #393 | Baixa |
| 2026-10-02 | `Program.cs` importa namespace do controller só para ler `MfaVerifyController.ChallengeScheme` | #393 | Baixa (cosmético) |
| 2026-10-02 | Deploy: sem `Auth__Mfa__EncryptionKey` a app não sobe em nenhum ambiente — criar a variável no Render antes do merge em `master` | #393 | Lembrete |

---

## Decisões fechadas fora da spec

Registro auditável do que foi decidido em rodada de planning ou implementação e não está na
especificação — inclusive as recalibrações da fórmula de estimativa.

| Data | Decisão | Onde está documentada |
|---|---|---|
| 2026-10-02 | Recalibração da fórmula de estimativa: codificação mediana 5min → 4min, piso de orquestração 2min → 4min, total 7min → 8min por issue (n=2 → 12); correlação nº de arquivos × tempo continua inexistente | `Fórmula de Estimativa` (acima) e `node scripts/calibrate-estimates.js` |
