import { Injectable, signal, effect, inject, InjectionToken } from '@angular/core';

const THEME_KEY = 'pf_theme';

export const MATCH_MEDIA_FN = new InjectionToken<(query: string) => MediaQueryList>(
  'MATCH_MEDIA_FN',
  { factory: () => window.matchMedia.bind(window) }
);

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly matchMedia = inject(MATCH_MEDIA_FN);

  readonly isDark = signal<boolean>(
    localStorage.getItem(THEME_KEY) === 'dark' ||
    (!localStorage.getItem(THEME_KEY) &&
      typeof this.matchMedia === 'function' &&
      this.matchMedia('(prefers-color-scheme: dark)').matches)
  );

  constructor() {
    // Aplica a classe dark no <html> sempre que o signal mudar
    effect(() => {
      const dark = this.isDark();
      document.documentElement.classList.toggle('dark', dark);
      document.documentElement.setAttribute('data-dark', String(dark));
      localStorage.setItem(THEME_KEY, dark ? 'dark' : 'light');
    });
  }

  toggle(): void {
    this.isDark.update(v => !v);
  }
}
