import { Component, inject, input } from '@angular/core';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-header',
  standalone: true,
  template: `
    <header class="header">
      <div class="header-title">
        <h1 class="page-title">{{ title() }}</h1>
        @if (subtitle()) {
          <span class="text-muted" style="font-size: 0.875rem">{{ subtitle() }}</span>
        }
      </div>

      <div class="header-actions">
        <ng-content />

        <!-- Theme toggle -->
        <button class="btn btn-ghost btn-sm theme-toggle" (click)="theme.toggle()" [title]="theme.isDark() ? 'Modo claro' : 'Modo escuro'">
          @if (theme.isDark()) {
            <!-- Sol -->
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="5"/>
              <line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/>
              <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/>
              <line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/>
              <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/>
            </svg>
          } @else {
            <!-- Lua -->
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
            </svg>
          }
        </button>
      </div>
    </header>
  `,
  styles: [`
    .header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 20px 28px;
      background: var(--surface-raised);
      border-bottom: 1px solid var(--border);
      min-height: 68px;
      gap: 16px;
    }

    .header-title {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .header-actions {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .theme-toggle {
      color: var(--ink3);
    }
  `]
})
export class HeaderComponent {
  readonly title    = input<string>('');
  readonly subtitle = input<string>('');
  readonly theme    = inject(ThemeService);
}
