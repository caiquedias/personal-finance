import { Injectable, signal, effect } from '@angular/core';

const THEME_KEY = 'pf_theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly isDark = signal<boolean>(
    localStorage.getItem(THEME_KEY) === 'dark' ||
    (!localStorage.getItem(THEME_KEY) &&
      typeof window.matchMedia === 'function' &&
      window.matchMedia('(prefers-color-scheme: dark)').matches)
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
