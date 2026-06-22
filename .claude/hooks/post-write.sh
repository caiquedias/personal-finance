#!/bin/bash
# Linter automático pós-escrita de arquivo — PostToolUse (Write | Edit)

INPUT=$(cat)
FILE=$(echo "$INPUT" | python3 -c "
import sys, json
d = json.load(sys.stdin)
print(d.get('tool_input', {}).get('file_path', ''))
" 2>/dev/null)

[ -z "$FILE" ] && exit 0

EXT="${FILE##*.}"

case "$EXT" in
  cs)
    dotnet format PersonalFinance.sln --include "$FILE" --no-restore 2>&1
    ;;
  ts|html)
    npx eslint --fix "$FILE" 2>&1
    ;;
esac

exit 0
