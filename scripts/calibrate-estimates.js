#!/usr/bin/env node
/**
 * calibrate-estimates.js — recalibra a Fórmula de Estimativa a partir dos blocos
 * "## Análise de eficiência da sessão" dos arquivos docs/memory/<issue-number>.md.
 *
 * Uso:
 *   node scripts/calibrate-estimates.js                 # relatório em texto
 *   node scripts/calibrate-estimates.js --check         # só diz se a recalibração já é devida
 *   node scripts/calibrate-estimates.js --json          # dataset + métricas em JSON
 *   node scripts/calibrate-estimates.js --dir docs/memory --verbose
 *
 * --check compara o nº atual de issues com codificação medida contra a linha
 * "> **Última calibração:** <AAAA-MM-DD> · n=<N> issues ..." de docs/sprint-planning.md, e sai com
 * código 1 quando passou do limite (--limite, padrão 10). É o que o /end-issue chama.
 *
 * Saída: correlações (nº de arquivos x codificação), medianas por bucket de Size,
 * percentis de codificação, piso de orquestração e duração total por nº de ciclos.
 * Rodar a cada ~10 issues novas e atualizar docs/sprint-planning.md → "Fórmula de Estimativa".
 */

const fs = require('fs');
const path = require('path');

// ---------------------------------------------------------------- configuração
const args = process.argv.slice(2);
const arg = (name, dflt) => {
  const i = args.indexOf(name);
  return i >= 0 && args[i + 1] && !args[i + 1].startsWith('--') ? args[i + 1] : dflt;
};
const DIR = arg('--dir', 'docs/memory');
const AS_JSON = args.includes('--json');
const VERBOSE = args.includes('--verbose');
const CHECK = args.includes('--check');
const PLANNING = arg('--planning', 'docs/sprint-planning.md');
// quantas issues novas com codificação medida disparam nova calibração
const LIMITE_RECALIBRACAO = +arg('--limite', 10);

// Buckets de Size por nº de arquivos (produção + teste) — docs/sprint-planning.md
const BUCKETS = [['XS', 2], ['S', 7], ['M', 14], ['L', 25], ['XL', Infinity]];

// Uma "duração de sessão completa" é descartada quando o próprio doc registra que o
// intervalo não é tempo de trabalho contínuo (pausa noturna, retomada em outro dia,
// trabalho fora de escopo na mesma branch).
const UNRELIABLE = /hiato|pausa|overnight|noturn|não é tempo de trabalho|2 janelas|span (?:em|bruto)|fora de escopo|não contínu|não mensur|não comparável|não medida|até doc final/i;

// ------------------------------------------------------------------- utilitários
const med = (a) => {
  const s = [...a].sort((x, y) => x - y);
  const n = s.length;
  if (!n) return null;
  return n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
};
const pct = (a, p) => {
  const s = [...a].sort((x, y) => x - y);
  return s.length ? s[Math.min(s.length - 1, Math.floor(s.length * p))] : null;
};
const r1 = (x) => (x == null ? null : Math.round(x * 10) / 10);
const r3 = (x) => (x == null ? null : Math.round(x * 1000) / 1000);
const hhmm = (m) => (m == null ? '-' : (m >= 60 ? Math.floor(m / 60) + 'h' + String(Math.round(m % 60)).padStart(2, '0') : Math.round(m) + 'min'));

function pearson(xs, ys) {
  const n = xs.length;
  if (n < 3) return null;
  const mx = xs.reduce((a, b) => a + b, 0) / n;
  const my = ys.reduce((a, b) => a + b, 0) / n;
  let num = 0, dx = 0, dy = 0;
  for (let i = 0; i < n; i++) { num += (xs[i] - mx) * (ys[i] - my); dx += (xs[i] - mx) ** 2; dy += (ys[i] - my) ** 2; }
  return dx && dy ? num / Math.sqrt(dx * dy) : null;
}
function spearman(xs, ys) {
  const rank = (v) => {
    const s = v.map((x, i) => [x, i]).sort((a, b) => a[0] - b[0]);
    const out = Array(v.length);
    s.forEach((p, k) => { out[p[1]] = k + 1; });
    return out;
  };
  return pearson(rank(xs), rank(ys));
}

// duração em minutos a partir de texto livre: 2h10min | 1h00min19s | ~59min | 5h31m | 12min36s | 1h
function dur(v) {
  if (!v) return null;
  const s = v.replace(/\([^)]*\)/g, ' ');
  if (/não mensur|não dispon|não regist|não calcul|não medid/i.test(s)) return null;
  const h = s.match(/(\d+)\s*h\s*(\d+)?\s*(?:min|m)?/);
  if (h && /\d\s*h/.test(h[0])) return (+h[1]) * 60 + (+(h[2] || 0));
  const m = s.match(/(?:^|[^\dh])(\d+)\s*min/);
  return m ? +m[1] : null;
}
// estimativa: aceita "1.5h", "0,7h (42min)", "90min"
function estMin(v) {
  if (!v) return null;
  if (/não regist|não dispon|não calcul|não encontrad/i.test(v)) return null;
  const h = v.match(/~?\s*(\d+[.,]?\d*)\s*h/);
  if (h) return Math.round(parseFloat(h[1].replace(',', '.')) * 60);
  const m = v.match(/~?\s*(\d+)\s*min/);
  return m ? +m[1] : null;
}

// ------------------------------------------------------------------- extração
function parseFile(file) {
  const txt = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
  const lines = txt.split('\n');
  const section = (title) => {
    const i = lines.findIndex((l) => l.trim().toLowerCase().startsWith('## ' + title));
    if (i < 0) return [];
    const out = [];
    for (let j = i + 1; j < lines.length && !lines[j].startsWith('## '); j++) out.push(lines[j]);
    return out;
  };
  const countBullets = (title) => section(title).filter((l) => /^\s*-\s+`/.test(l)).length;

  const ef = section('análise de eficiência da sessão').join('\n');
  const segs = [];
  const re = /\*\*([^*]+?):\*\*\s*([^|\n]*)/g;
  let m;
  while ((m = re.exec(ef)) !== null) segs.push([m[1].trim().toLowerCase(), m[2].trim()]);
  const get = (rx) => { const s = segs.find(([k]) => rx.test(k)); return s ? s[0] + ': ' + s[1] : null; };

  const estRaw = get(/^estimativa/);
  const codRaw = get(/duração de codificação/i);
  const totRaw = get(/duração de sessão completa/i);

  const cycVal = (get(/ciclos de retrabalho/i) || '').replace(/^[^:]*:\s*/, '');
  const qa = cycVal.match(/QA:\s*(\d+)/i);
  const rev = cycVal.match(/Reviewer:\s*(\d+)/i);
  const cycTotal = /^\s*(\d+)/.test(cycVal)
    ? +cycVal.match(/^\s*(\d+)/)[1]
    : (qa ? (+qa[1]) + (rev ? +rev[1] : 0) : null);

  const cod = dur(codRaw);
  const tot = dur(totRaw);
  const totOk = tot != null && !UNRELIABLE.test(totRaw || '') && !(cod != null && tot <= cod);

  return {
    id: path.basename(file, '.md'),
    arq: countBullets('arquivos criados') + countBullets('arquivos modificados'),
    criados: countBullets('arquivos criados'),
    est: estMin(estRaw),
    cod,
    tot: totOk ? tot : null,
    totDescartado: tot != null && !totOk ? tot : null,
    motivoDescarte: tot != null && !totOk
      ? (UNRELIABLE.test(totRaw) ? totRaw.match(UNRELIABLE)[0] : 'igual à codificação')
      : null,
    ciclos: cycTotal,
    temBloco: ef.trim().length > 0,
  };
}

// ------------------------------------------------------------------- execução
if (!fs.existsSync(DIR)) {
  console.error('Diretório não encontrado: ' + DIR);
  process.exit(1);
}
const files = fs.readdirSync(DIR).filter((f) => /^\d+\.md$/.test(f)).sort();
const all = files.map((f) => parseFile(path.join(DIR, f)));

const semBloco = all.filter((r) => !r.temBloco).map((r) => r.id);
const semCod = all.filter((r) => r.temBloco && r.cod == null).map((r) => r.id);
const d = all.filter((r) => r.cod != null); // amostra da calibração
const comTot = d.filter((r) => r.tot != null);
const bucket = (a) => (BUCKETS.find(([, max]) => a <= max) || ['?'])[0];

const codAll = d.map((r) => r.cod);
const metrics = {
  n: d.length,
  correlacao: {
    arquivos: { pearson: r3(pearson(d.map((r) => r.arq), codAll)), spearman: r3(spearman(d.map((r) => r.arq), codAll)) },
    criados: { pearson: r3(pearson(d.map((r) => r.criados), codAll)), spearman: r3(spearman(d.map((r) => r.criados), codAll)) },
  },
  codificacao: codAll.length
    ? { mediana: r1(med(codAll)), p25: r1(pct(codAll, 0.25)), p75: r1(pct(codAll, 0.75)), p90: r1(pct(codAll, 0.9)), min: r1(Math.min(...codAll)), max: r1(Math.max(...codAll)) }
    : { mediana: null, p25: null, p75: null, p90: null, min: null, max: null },
  porBucket: {},
  porCiclos: {},
  pisoOrquestracao: null,
  formulaSugerida: {},
};
for (const [b] of BUCKETS) {
  const g = d.filter((r) => bucket(r.arq) === b).map((r) => r.cod);
  if (g.length) metrics.porBucket[b] = { n: g.length, mediana: r1(med(g)), min: r1(Math.min(...g)), max: r1(Math.max(...g)) };
}
const ciclosVistos = [...new Set(d.filter((r) => r.ciclos != null).map((r) => r.ciclos))].sort((a, b) => a - b);
for (const c of ciclosVistos) {
  const g = d.filter((r) => r.ciclos === c);
  const gt = comTot.filter((r) => r.ciclos === c);
  metrics.porCiclos[c] = {
    n: g.length,
    nTotal: gt.length,
    codMediana: r1(med(g.map((r) => r.cod))),
    totalMediana: gt.length ? r1(med(gt.map((r) => r.tot))) : null,
    orquestracaoMediana: gt.length ? r1(med(gt.map((r) => r.tot - r.cod))) : null,
  };
}
metrics.pisoOrquestracao = metrics.porCiclos[0] ? metrics.porCiclos[0].orquestracaoMediana : null;
metrics.formulaSugerida = {
  codificacao: metrics.codificacao.mediana,
  piso: metrics.pisoOrquestracao,
  estimativa: metrics.pisoOrquestracao != null && metrics.codificacao.mediana != null
    ? r1(metrics.codificacao.mediana + metrics.pisoOrquestracao)
    : null,
};

// ---------------------------------------------------- baseline / --check
function lerBaseline() {
  if (!PLANNING || !fs.existsSync(PLANNING)) return null;
  const m = fs.readFileSync(PLANNING, 'utf8').match(/Última calibração:\*{0,2}\s*([\d-]{10})[^\n]*?n\s*=\s*(\d+)/i);
  return m ? { data: m[1], n: +m[2] } : null;
}
const baseline = lerBaseline();
const novas = baseline ? d.length - baseline.n : null;
const devida = baseline == null || novas >= LIMITE_RECALIBRACAO;

if (CHECK) {
  if (baseline == null) {
    console.log('RECALIBRAR: sem calibração registrada em ' + (PLANNING || '(doc de planning não localizado)')
      + ' — esperada a linha "> **Última calibração:** <AAAA-MM-DD> · n=<N> issues ..." com data preenchida');
  } else if (devida) {
    console.log('RECALIBRAR: ' + novas + ' issues novas com codificação medida desde ' + baseline.data
      + ' (n=' + baseline.n + ' → ' + d.length + ', limite ' + LIMITE_RECALIBRACAO + ')');
  } else {
    console.log('OK: n=' + d.length + ' (última calibração ' + baseline.data + ', n=' + baseline.n
      + '); faltam ' + (LIMITE_RECALIBRACAO - novas) + ' issues para recalibrar');
  }
  process.exit(devida ? 1 : 0);
}

if (AS_JSON) {
  console.log(JSON.stringify({ metrics, dataset: d, ignorados: { semBloco, semCod }, baseline, recalibracaoDevida: devida }, null, 2));
  process.exit(0);
}

const line = (s) => console.log(s);
line('Calibração de estimativas — ' + DIR + ' (' + files.length + ' issues, ' + d.length + ' com codificação medida)');
line('');
if (!d.length) {
  line('Nenhuma issue com "Duração de codificação" preenchida ainda — nada a calibrar.');
  line('Use a estimativa manual (Xh no comentário de planning) até acumular ~10 issues medidas.');
  if (semBloco.length) line('! sem bloco "Análise de eficiência da sessão": ' + semBloco.join(', '));
  process.exit(0);
}
line('== nº de arquivos prevê tempo de codificação? ==');
line('  arquivos (criados+modificados): Pearson ' + metrics.correlacao.arquivos.pearson + ' | Spearman ' + metrics.correlacao.arquivos.spearman);
line('  só arquivos criados:            Pearson ' + metrics.correlacao.criados.pearson + ' | Spearman ' + metrics.correlacao.criados.spearman);
line('  (|r| < 0,4 = sem sinal: manter Size só para divisão de escopo, não para estimar tempo)');
line('');
line('== codificação medida por bucket de Size ==');
for (const [b, v] of Object.entries(metrics.porBucket)) {
  line('  ' + b.padEnd(3) + ' n=' + String(v.n).padEnd(3) + ' mediana=' + hhmm(v.mediana).padEnd(7) + ' min=' + hhmm(v.min).padEnd(7) + ' max=' + hhmm(v.max));
}
line('  (medianas não-monotônicas entre buckets = mais um sinal de que o Size não prevê tempo)');
line('');
line('== codificação global ==');
const c = metrics.codificacao;
line('  mediana=' + hhmm(c.mediana) + '  p25=' + hhmm(c.p25) + '  p75=' + hhmm(c.p75) + '  p90=' + hhmm(c.p90) + '  min=' + hhmm(c.min) + '  max=' + hhmm(c.max));
line('');
line('== duração por nº de ciclos de retrabalho ==');
line('  ciclos | issues | codificação | total (mediana) | orquestração | n (duração confiável)');
for (const [k, v] of Object.entries(metrics.porCiclos)) {
  line('  ' + k.padEnd(6) + ' | ' + String(v.n).padEnd(6) + ' | ' + hhmm(v.codMediana).padEnd(11) + ' | '
    + hhmm(v.totalMediana).padEnd(15) + ' | ' + hhmm(v.orquestracaoMediana).padEnd(12) + ' | ' + v.nTotal);
}
line('');
line('== fórmula sugerida para docs/sprint-planning.md ==');
line('  Codificação (qualquer Size) : ' + hhmm(metrics.formulaSugerida.codificacao) + '  (p75 ' + hhmm(c.p75) + ' · p90 ' + hhmm(c.p90) + ')');
line('  Piso de orquestração        : +' + hhmm(metrics.formulaSugerida.piso));
line('  Estimativa: Xh              = ' + hhmm(metrics.formulaSugerida.estimativa));
line('');
if (baseline) {
  line('  Última calibração registrada em ' + PLANNING + ': ' + baseline.data + ' (n=' + baseline.n + ')'
    + (devida ? ' — RECALIBRAR: ' + novas + ' issues novas' : ' — faltam ' + (LIMITE_RECALIBRACAO - novas) + ' issues para a próxima'));
  line('  Ao atualizar, troque a linha por: > **Última calibração:** ' + new Date().toISOString().slice(0, 10) + ' · n=' + d.length + ' issues com codificação medida');
  line('');
}
if (semBloco.length) line('! sem bloco "Análise de eficiência da sessão": ' + semBloco.join(', '));
if (semCod.length) line('! bloco presente mas sem "Duração de codificação": ' + semCod.join(', '));
const desc = d.filter((r) => r.totDescartado != null);
if (desc.length) line('! duração total descartada (não é tempo contínuo): ' + desc.map((r) => r.id + ' [' + r.motivoDescarte + ']').join(', '));
if (VERBOSE) {
  line('');
  line('id\tarq\tSize\tcod\ttot\tciclos\test');
  for (const r of [...d].sort((a, b) => a.cod - b.cod)) {
    line([r.id, r.arq, bucket(r.arq), r1(r.cod), r.tot == null ? '-' : r1(r.tot), r.ciclos == null ? '-' : r.ciclos, r.est == null ? '-' : r.est].join('\t'));
  }
}
