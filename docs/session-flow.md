# Fluxo de sessão — Planning vs Implementação

## Regra de pré-ação obrigatória

Antes de qualquer ação externa, exibir o que será feito e aguardar confirmação explícita do Caique:

| Ação externa | Exige confirmação |
|---|---|
| Postar comentário em issue | Sim |
| Atualizar campos do board (Status, Size, Priority) | Sim |
| `git push` | Sim |
| `gh pr create` | Sim |

---

## Fluxo de sessão de planning

1. Levantamento autônomo (ler arquivos, board, issues)
2. Montar o planning completo
3. Apresentar ao Caique — **parar aqui**
4. Aguardar OK explícito
5. Somente após OK: executar ações externas (post na issue, update do board)

---

## Revisão de sessão

Ao final de cada planning, verificar:
- O fluxo acima foi seguido?
- Houve ação externa sem confirmação prévia?
- O planning foi postado antes da confirmação?

Se sim a qualquer item: registrar como feedback e corrigir no próximo planning.

---

## Regras do Macro Agent durante implementação

**Regra absoluta: o Macro Agent nunca edita arquivos de código. Toda alteração passa pelo Implementador.**

| Situação | O que o Macro faz | O que NUNCA faz |
|---|---|---|
| Reviewer → REQUER CORREÇÃO | Spawna Red com os findings como spec → Green → QA → Reviewer | Editar arquivos diretamente |
| Caique reporta erro de build/runtime | Spawna QA em "modo erro" → QA classifica → spawna Red/Green | Editar arquivos diretamente |
| Qualquer outro ajuste de código | Spawna Green com instrução precisa | Editar arquivos diretamente |

### Protocolo para erro de build/runtime reportado pelo Caique

1. Macro spawna QA em **modo erro** com: mensagem de erro exata + arquivos modificados desde o último push
2. QA identifica root cause, classifica sizing (S/M/L/XL)
3. **S/M** → QA spawna Red (regressão/teste falhando que captura o erro) → Green (corrige) → QA revalida → Reviewer
4. **L/XL** → QA reporta ao Caique com root cause e sizing — não avança autonomamente
