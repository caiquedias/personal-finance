import { Component, inject, computed } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';

interface NavItem {
  label:    string;
  route:    string;
  icon:     string;
  adminOnly?: boolean;
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard',  route: '/',          icon: 'grid'       },
  { label: 'Períodos',   route: '/periods',   icon: 'calendar'   },
  { label: 'Despesas',   route: '/expenses',  icon: 'arrow-down' },
  { label: 'Receitas',   route: '/incomes',   icon: 'arrow-up'   },
  { label: 'Importar',   route: '/import',    icon: 'upload'     },
  { label: 'Config',     route: '/config',    icon: 'settings'   },
  { label: 'Usuários',   route: '/admin/users', icon: 'users', adminOnly: true },
];

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <aside class="sidebar">
      <!-- Logo -->
      <div class="sidebar-logo">
        <span class="sidebar-logo-mark">₿</span>
        <span class="sidebar-logo-text">Personal Finance</span>
      </div>

      <!-- Nav -->
      <nav class="sidebar-nav">
        @for (item of visibleItems(); track item.route) {
          <a
            [routerLink]="item.route"
            routerLinkActive="active"
            [routerLinkActiveOptions]="{ exact: item.route === '/' }"
            class="sidebar-item"
          >
            <span class="sidebar-icon" [innerHTML]="getIcon(item.icon)"></span>
            <span>{{ item.label }}</span>
          </a>
        }
      </nav>

      <!-- User info -->
      <div class="sidebar-footer">
        <div class="sidebar-user">
          <div class="sidebar-avatar">
            {{ userInitials() }}
          </div>
          <div class="sidebar-user-info">
            <span class="sidebar-user-name">{{ auth.currentUser()?.name }}</span>
            <span class="sidebar-user-email">{{ auth.currentUser()?.email }}</span>
          </div>
        </div>
        <button class="sidebar-logout" (click)="auth.logout()" title="Sair">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
            <polyline points="16 17 21 12 16 7"/>
            <line x1="21" y1="12" x2="9" y2="12"/>
          </svg>
        </button>
      </div>
    </aside>
  `,
  styles: [`
    .sidebar {
      width: var(--sidebar-width);
      height: 100vh;
      background: var(--sidebar-bg);
      border-right: 1px solid var(--sidebar-border);
      display: flex;
      flex-direction: column;
      position: fixed;
      left: 0; top: 0;
      z-index: 40;
    }

    .sidebar-logo {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 20px 20px 16px;
      border-bottom: 1px solid var(--border);
    }

    .sidebar-logo-mark {
      width: 32px;
      height: 32px;
      background: var(--sage2);
      color: #fff;
      border-radius: var(--radius);
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 14px;
      flex-shrink: 0;
    }

    .sidebar-logo-text {
      font-size: 0.9375rem;
      font-weight: 600;
      color: var(--ink);
    }

    .sidebar-nav {
      flex: 1;
      padding: 12px 10px;
      display: flex;
      flex-direction: column;
      gap: 2px;
      overflow-y: auto;
    }

    .sidebar-item {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 9px 10px;
      border-radius: var(--radius);
      color: var(--ink2);
      font-size: 0.875rem;
      font-weight: 500;
      transition: all var(--transition);
      text-decoration: none;
    }

    .sidebar-item:hover {
      background: var(--sidebar-item-hover);
      color: var(--ink);
    }

    .sidebar-item.active {
      background: var(--sidebar-item-active-bg);
      color: var(--sidebar-item-active);
    }

    .sidebar-icon {
      width: 18px;
      height: 18px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      opacity: 0.7;
    }

    .sidebar-item.active .sidebar-icon { opacity: 1; }

    .sidebar-footer {
      padding: 12px;
      border-top: 1px solid var(--border);
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .sidebar-user {
      flex: 1;
      display: flex;
      align-items: center;
      gap: 10px;
      min-width: 0;
    }

    .sidebar-avatar {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      background: var(--bg4);
      color: var(--ink2);
      font-size: 12px;
      font-weight: 600;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .sidebar-user-info {
      display: flex;
      flex-direction: column;
      min-width: 0;
      overflow: hidden;
    }

    .sidebar-user-name {
      font-size: 0.8125rem;
      font-weight: 600;
      color: var(--ink);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .sidebar-user-email {
      font-size: 0.75rem;
      color: var(--ink3);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .sidebar-logout {
      padding: 6px;
      border: none;
      background: none;
      color: var(--ink3);
      border-radius: var(--radius);
      cursor: pointer;
      display: flex;
      align-items: center;
      transition: all var(--transition);
      flex-shrink: 0;
    }

    .sidebar-logout:hover {
      background: var(--color-danger-bg);
      color: var(--color-danger);
    }
  `]
})
export class SidebarComponent {
  readonly auth = inject(AuthService);

  readonly visibleItems = computed(() =>
    NAV_ITEMS.filter(item => !item.adminOnly || this.auth.isAdmin())
  );

  readonly userInitials = computed(() => {
    const name = this.auth.currentUser()?.name ?? '';
    return name.split(' ').slice(0, 2).map(n => n[0]).join('').toUpperCase();
  });

  getIcon(name: string): string {
    const icons: Record<string, string> = {
      grid:       `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>`,
      calendar:   `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>`,
      'arrow-down':`<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="8 12 12 16 16 12"/><line x1="12" y1="8" x2="12" y2="16"/></svg>`,
      'arrow-up':  `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="16 12 12 8 8 12"/><line x1="12" y1="16" x2="12" y2="8"/></svg>`,
      settings:   `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>`,
      upload:     `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>`,
      users:      `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>`,
    };
    return icons[name] ?? '';
  }
}
