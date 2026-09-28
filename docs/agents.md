# Guia de Criação de Sub-agentes — Personal Finance

## Estrutura de arquivos

Todo agente tem dois arquivos:

```
.claude/agents/<nome>.md   ← definição (role, contexto, output format)
docs/<referência>.md       ← protocolo detalhado (checklist, critérios, comandos)
```

## Template do arquivo de definição

```markdown
---
name: <nome-kebab-case>            # é por este nome que o Macro spawna: subagent_type: "<nome>"
description: <quando usar este agente, em 1-2 frases>
tools: <lista explícita — a restrição é real, não é só texto de prompt>
model: <sonnet | opus | omitir para herdar o modelo da sessão>
---

# Sub-agente: <Nome>

Você é o sub-agente <Nome> do projeto Personal Finance (MonkeyBomb).
Leia o CLAUDE.md e o `docs/<referência>.md` antes de qualquer ação.

## Contexto recebido
[campos que o agente recebe]

## Responsabilidades
Você tem acesso de **<nível>** — <restrição>.
[responsabilidades]

## Shell e ambiente
O Bash tool executa **bash Linux** — nunca PowerShell.
[comandos relevantes para a stack]

## Output obrigatório
[formato do output]

## Próximo passo
[quem spawnar e quando]
```

## Níveis de autonomia

| Nível | Frase padrão |
|---|---|
| Escrita plena | `autonomia para criar e editar arquivos dentro do escopo da issue` |
| Leitura + execução (testes) | `acesso de **somente leitura e execução de testes**` |
| Leitura + execução (app) | `acesso de **somente leitura e execução do app**` |
| Somente leitura | `acesso de **somente leitura** — não edite nenhum arquivo` |

## Agentes existentes

| Agente | Arquivo | Protocolo | Autonomia |
|---|---|---|---|
| Implementador Red | `.claude/agents/implementer-red.md` | `docs/test-factory.md` | Somente arquivos de teste |
| Implementador Green | `.claude/agents/implementer-green.md` | `CLAUDE.md` | Escrita plena no escopo |
| QA | `.claude/agents/qa.md` | `docs/qa-agent.md` | Leitura + execução de testes |
| Reviewer | `.claude/agents/reviewer.md` | `docs/code-review.md` | Somente leitura |
| PO | `.claude/agents/po.md` | `docs/sprint-planning.md` | Somente leitura (modelo Opus) |
| UX Validator | `.claude/agents/ux-validator.md` | `docs/patterns.md` | Leitura + execução do app |

## `subagent_type` nomeado — não colar o corpo do arquivo de agente

Todo arquivo em `.claude/agents/` tem frontmatter (`name`/`description`/`tools`/`model`) e é
invocado por `subagent_type: "<name>"` — o harness carrega a definição sozinho. **Nunca** spawnar
um agente genérico colando o markdown inteiro do agente no prompt: isso duplica o mesmo bloco fixo
(role + output format) em *todo* spawn.

`tools` no frontmatter **restringe de verdade** o acesso, não é só instrução em texto: QA, Reviewer
e PO sem `Write`/`Edit` na lista passam a ter o "somente leitura" reforçado pelo harness, não pelo
prompt.

## Prompt de spawn — só fatos da sessão

O agente já é instruído a ler `CLAUDE.md` + `docs/<referência>.md` sozinho. Quem orquestra (Macro)
**nunca** cola o conteúdo desses arquivos no prompt de spawn.

**Heurística de checagem:** se o bloco de "contexto" do prompt passar de ~40 linhas, é sinal de que
protocolo estático está sendo duplicado — cortar antes de spawnar, não depois.

## Contexto compartilhado entre Red e Green — arquivo único, não colar duas vezes

Sempre que o mesmo bloco fixo (critérios de aceite, assinaturas de interface, resultado da Análise
de Impacto do PO) for necessário para mais de um agente, quem orquestra escreve esse bloco **uma
única vez** em um arquivo (scratchpad da sessão, ver `.claude/commands/start-issue.md` → Passo 2.5)
e passa apenas o **caminho** no prompt de cada agente. Em ciclos de retrabalho (GAP do QA, REQUER
CORREÇÃO do Reviewer), reaproveitar o mesmo arquivo em vez de recolar o bloco a cada novo prompt.

## Um spawn de Green por task — exceto tasks triviais fortemente acopladas

O default é um Green por task (rastreabilidade em tempo real). **Exceção:** quando 2+ tasks são
pequenas (juntas < ~60 linhas de produção) e uma é pré-requisito da outra para qualquer teste
rodar, spawnar **um** Green nomeando todos os IDs no prompt.

## Projeto de teste que só compila no Green da última task

Numa issue cujos arquivos de teste se cruzam (cada `*Tests` referencia o tipo de produção de uma
task irmã), o projeto de teste não compila até o último tipo existir — inerente ao Red-primeiro.
Não é bloqueio: o Red nomeia no output qual task fecha a compilação; os Greens intermediários
verificam por build do módulo de produção + inspeção do próprio arquivo de teste, sem escalar; o
Green da última task roda a suíte inteira.

## Sub-agentes nunca usam Monitor/background para a própria verificação

Ao rodar testes/build, o sub-agente executa **sempre em foreground**. Um sub-agente não recebe
notificação de tarefas em background como o Macro recebe, e fica preso indefinidamente até quem
orquestra reenviar mensagem manualmente. Essa regra está inline em cada arquivo de agente
(`implementer-red.md`, `implementer-green.md`, `qa.md`, `reviewer.md`, `ux-validator.md`) — não
depender só deste doc.

## Hooks dependem do interpretador que existe de fato na máquina

Os hooks em `.claude/hooks/` parseiam o JSON do harness com **`node`** (com fallback em `sed`), não
`python3`: em Windows, `python3` costuma resolver para o stub da Microsoft Store, que falha
silenciosamente — e o comando extraído ficaria vazio, transformando todos os guardrails em no-op.
O `pre-bash.sh` bloqueia (`exit 2`) se não conseguir parsear, em vez de liberar. Ao editar hooks,
manter esse comportamento *fail-closed*.

## Como registrar novo agente no CLAUDE.md

Ao criar um novo agente, atualizar **dois lugares** em `CLAUDE.md`:
1. Seção de Orquestração (se existir)
2. Seção de Referências / docs

E atualizar a tabela acima neste arquivo.
