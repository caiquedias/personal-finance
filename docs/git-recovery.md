# Recuperação após travamento / reinício forçado

Reinício forçado no Windows pode deixar arquivos do `.git` existindo com conteúdo zerado
(SHA `000…0`). Sintoma típico: `bad object refs/remotes/origin/...`, `invalid sha1 pointer`
no `git fsck`/`git branch -a`.

## Depois de qualquer reinício forçado

```bash
bash scripts/git-heal.sh
```

O script apaga só o que é descartável (refs de rastreio remoto e `ORIG_HEAD` zerados), roda
`git fetch --prune` para recriar o rastreio e termina com `git fsck`. Refs **locais** quebrados
(`refs/heads`, tags) só são reportados.

## Casos manuais

| Problema | Correção |
|---|---|
| Ref local quebrado | `git reflog` para achar o SHA, ou `git branch -f <b> origin/<b>` |
| Objeto vazio/corrompido em `.git/objects` | apagar o arquivo de tamanho zero e `git fetch` |
| `git update-ref` falha em ref quebrado | o Git não trava ref inválido: apagar o arquivo do ref e `git fetch` |

## Prevenção

- `git config --global core.fsync all` (Git ≥ 2.36): flush de objetos, packs e refs; custa um pouco de velocidade
- Exclusão do antivírus para a pasta do repo
- Push cedo e com frequência para `claude/` — o remoto é a fonte de verdade
- Investigar a causa dos travamentos (saúde do SSD, Event Viewer `Kernel-Power 41`, drivers)
