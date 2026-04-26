# Design System — Personal Finance (MonkeyBomb)

> Extraído do design de referência da issue #105. Usado pelas issues #126–#133 (Refactory Layout).

---

## Variáveis CSS

```css
/* Paleta principal */
--terracotta: #C4674A;   /* cor de destaque (accent), active states, CTAs */
--olive:      #7B8C5F;   /* positivo, saldo, pago */
--purple:     #9E8BB5;   /* receitas, categorias secundárias */
--amber:      #C4924A;   /* alertas leves, a pagar */
--sage:       #6B9C82;
--slate:      #6B8CAD;
--rose:       #B57A9E;
--gold:       #D4B87A;

/* Tipografia */
--font-b: 'Inter', sans-serif;         /* body, UI */
--font-d: 'DM Serif Display', serif;   /* números, títulos de destaque */

/* Layout */
--radius: 18px;          /* cards, modais, botões grandes */
--radius-sm: 11px;       /* botões menores, chips */
--radius-xs: 9px;        /* tags, badges */

/* Sombra */
--shadow: 0 4px 24px rgba(0,0,0,0.08);

/* Transição padrão */
--t: all 0.22s ease;
--ease: cubic-bezier(0.4, 0, 0.2, 1);

/* Superfícies (dark mode — padrão) */
--bg:           #1A1410;
--sidebar-bg:   #141210;
--surface:      rgba(255,255,255,0.04);
--surface-solid:#1F1B17;
--border:       rgba(255,255,255,0.07);
--text:         #F0E8DC;
--text-muted:   #9A8E81;
--text-subtle:  #6A5E52;

/* Superfícies (light mode) */
/* data-dark="false" → inverter valores acima conforme design */
```

---

## Animações

```css
@keyframes fadeUp {
  from { opacity: 0; transform: translateY(14px); }
  to   { opacity: 1; transform: translateY(0);    }
}
/* Uso: animation: fadeUp 0.5s var(--ease) <delay>s both; */
/* Escalonamento de delay: 0.08s + index * 0.05s */
```

---

## Componentes de referência

### KPI Card
- `background: var(--surface)` + `backdropFilter: blur(24px)`
- `borderRadius: var(--radius)` + `padding: 22px 24px`
- Rótulo: `font-size: 10px`, `font-weight: 600`, `letter-spacing: 0.1em`, `text-transform: uppercase`, cor `var(--text-subtle)`
- Valor: `font-family: var(--font-d)`, `font-size: 26px`, `font-weight: 500`
- Subtítulo: `font-size: 11px`, `color: var(--text-muted)`
- Barra inferior: `height: 2px`, `border-radius: 1px`, `background: linear-gradient(to right, <accent>70, <accent>10)`
- Ponto de cor: `width: 7px`, `height: 7px`, `border-radius: 50%`, `box-shadow: 0 0 8px <accent>80`

### Widget / Card genérico
- `background: var(--surface)` + `backdropFilter: blur(24px)`
- `borderRadius: var(--radius)` + `padding: 22px`
- `boxShadow: var(--shadow)`
- Título interno: `font-size: 13px`, `font-weight: 600`, `color: var(--text)`, `margin-bottom: 16px`
- Suporta drag handle (ícone 6 pontos) posicionado `absolute top:17 right:17`

### Botão de navegação (sidebar)
- Ativo: `background: var(--terracotta)`, `color: #fff`, `font-weight: 600`, `box-shadow: 0 2px 12px rgba(196,103,74,0.28)`
- Inativo: `background: transparent`, `color: var(--text-muted)`
- `border-radius: 13px`, `padding: 10px 13px`, `font-size: 13px`
- Transição: `background 0.22s, color 0.22s, box-shadow 0.22s`

### Chip de período (mês ativo)
- Ativo: `background: var(--terracotta)`, `color: #fff`, `font-weight: 600`, `box-shadow: 0 2px 12px rgba(196,103,74,0.3)`
- Inativo: `background: var(--surface)`, `color: var(--text-muted)`
- `padding: 7px 15px`, `border-radius: 11px`, `font-size: 12px`

### Botão de ação rápida
- `background: <color>1A` (15% opacidade), `color: <color>` (sem borda)
- `padding: 12px 15px`, `border-radius: 13px`, `font-size: 13px`, `font-weight: 500`

### Barra de progresso
- Track: `height: 5px`, `background: var(--border)`, `border-radius: 3px`
- Fill: animação `width` de `0%` → `X%` em `1.3s cubic-bezier(0.4,0,0.2,1)`

### Segmented control (seletor de mês no gráfico)
- Container: `background: var(--border)`, `border-radius: 10px`, `padding: 3px`
- Opção ativa: `background: var(--surface-solid)`, `box-shadow: var(--shadow)`, `color: var(--text)`, `font-weight: 600`
- Opção inativa: `background: transparent`, `color: var(--text-muted)`
- Cada opção: `padding: 4px 9px`, `border-radius: 8px`, `font-size: 11px`

---

## Sidebar

- Largura aberta: `198px` | Largura colapsada: `64px`
- Transição: `width 0.34s cubic-bezier(0.4,0,0.2,1)`
- `background: var(--sidebar-bg)`, `border-right: 1px solid var(--border)`
- Logo mark: `width: 33px`, `height: 33px`, `border-radius: 11px`, `background: var(--terracotta)`
- Texto do logo: fade `opacity 0.22s` ao colapsar
- Botão fechar (aberto): chevron `‹` | Botão abrir (colapsado): `☰`
- Footer: toggle dark/light + avatar do usuário

---

## Gráfico Donut

- Biblioteca: Chart.js
- `type: 'doughnut'`, `cutout: '74%'`
- Sem legenda (`legend: { display: false }`)
- Tooltip: `backgroundColor: rgba(24,19,16,0.92)`, `cornerRadius: 12`, `padding: 11`
- Animação: `animateRotate: true`, `duration: 950`, `easing: easeInOutQuart`
- Centro: valor total em `var(--font-d)` + label "total" em `10px var(--text-muted)`

---

## Paleta de cores de categoria (gráficos)

```
#C4674A  #C4924A  #7B8C5F  #9E8BB5
#D4B87A  #6B9C82  #6B8CAD  #B57A9E
```

---

## Dark / Light mode

- Controlado por `data-dark` no `<html>` ou elemento raiz
- Toggle no footer da sidebar (`☀` / `☾`)
- Transição de background: `0.45s`
- Angular: `ThemeService` já implementado — usar o serviço existente

---

## Regras de aplicação

1. Usar variáveis CSS (`var(--...)`) — nunca cores hardcoded
2. `backdropFilter: blur(24px)` em todos os cards/surfaces
3. Animação `fadeUp` com delay escalonado em listas de cards
4. `border-radius: var(--radius)` em cards grandes, `var(--radius-sm)` em botões
5. Fonte de números sempre `var(--font-d)` (DM Serif Display)
6. Fonte de UI sempre `var(--font-b)` (Inter)
7. Cor de destaque sempre `var(--terracotta)` para estados ativos — nunca hardcoded `#C4674A`

---

## Estratégia de migração CSS (Option C) — issues #126–#133

As issues de layout são implementadas de forma gradual. Para evitar quebrar pages ainda não migradas, seguir esta convenção:

### Tokens sem conflito — adicionar direto ao `:root`
`--terracotta`, `--purple`, `--amber`, `--slate`, `--rose`, `--gold`, `--font-b`, `--font-d`, `--ease`, `--t`, `--text`, `--text-muted`, `--text-subtle`, `--radius-xs`, `--surface-solid`

### Tokens conflitantes — usar prefixo `--v2-` até migração completa
| Token design system | Nome transitório |
|---------------------|-----------------|
| `--bg`              | `--v2-bg`       |
| `--sidebar-bg`      | `--v2-sidebar-bg` |
| `--surface`         | `--v2-surface`  |
| `--border`          | `--v2-border`   |
| `--radius`          | `--v2-radius`   |
| `--radius-sm`       | `--v2-radius-sm`|
| `--shadow`          | `--v2-shadow`   |

### Regra de uso
- **Novos componentes** (issues #126–#133): usar `--v2-*` e tokens novos sem prefixo.
- **Pages já migradas**: já usam `--v2-*`.
- **Após todas as pages migrarem**: commit único renomeia `--v2-*` → nome canônico do design system.

---

*Referência: issue #105 — Refactory Layout Sistema*
