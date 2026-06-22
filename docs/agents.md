# Guia de Criação de Sub-agentes — Personal Finance

## Estrutura de arquivos

Todo agente tem dois arquivos:

```
.claude/agents/<nome>.md   ← definição (role, contexto, output format)
docs/<referência>.md       ← protocolo detalhado (checklist, critérios, comandos)
```

## Template do arquivo de definição

```markdown
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

## Como registrar novo agente no CLAUDE.md

Ao criar um novo agente, atualizar **dois lugares** em `CLAUDE.md`:
1. Seção de Orquestração (se existir)
2. Seção de Referências / docs

E atualizar a tabela acima neste arquivo.
