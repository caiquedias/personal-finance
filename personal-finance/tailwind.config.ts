import type { Config } from 'tailwindcss';

export default {
  content: ['./src/**/*.{html,ts}'],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        // Paleta MonkeyBomb — Personal Finance
        border:  { DEFAULT: '#c8bfaf', 2: '#b8ad9a' },
        ink:     { DEFAULT: '#3a2e22', 2: '#5c4e3c', 3: '#8a7a68', 4: '#b0a090' },
        sage:    { DEFAULT: '#6b8f71', 2: '#527a58' },
        terra:   { DEFAULT: '#b5674a', 2: '#9a5339' },
        sand:    { DEFAULT: '#c4a882', 2: '#a88d68' },
        rust:    { DEFAULT: '#c0603a', 2: '#a34e2c' },
        olive:   { DEFAULT: '#7a8c4a', 2: '#667840' },
        cream:   '#faf7f2',
        bg:      { DEFAULT: '#f5f0e8', 2: '#ede8de', 3: '#e4ddd1', 4: '#d9d1c3' },

        // Dark mode overrides mapeados
        dark: {
          bg:    '#1e1a14',
          bg2:   '#26211a',
          bg3:   '#2f2920',
          bg4:   '#3a3026',
          ink:   '#e8e0d4',
          ink2:  '#c8bfaf',
          ink3:  '#a89888',
          border:'#4a3f32',
        }
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'Courier New', 'monospace'],
      },
      borderRadius: {
        'sm': '4px',
        DEFAULT: '8px',
        'md': '8px',
        'lg': '12px',
        'xl': '16px',
      },
      boxShadow: {
        'card': '0 1px 3px rgba(58, 46, 34, 0.08)',
        'card-hover': '0 4px 12px rgba(58, 46, 34, 0.12)',
        'modal': '0 8px 32px rgba(58, 46, 34, 0.16)',
      },
    },
  },
  plugins: [],
} satisfies Config;
