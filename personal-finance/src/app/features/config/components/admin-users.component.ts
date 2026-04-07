import { Component, inject, signal, OnInit } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { HeaderComponent } from '../../../shared/components/header/header.component';
import { AdminUserResponse } from '../../../core/models/models';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [HeaderComponent, ReactiveFormsModule],
  template: `
    <app-header title="Usuários" subtitle="Gerenciamento de usuários do sistema" />

    <div class="page-content">
      @if (loading()) {
        <div class="loading-state"><span class="spinner-lg"></span></div>
      } @else {
        <div class="table-wrap card">
          <table class="table">
            <thead>
              <tr>
                <th>Nome</th>
                <th>E-mail</th>
                <th>Roles</th>
                <th>Status</th>
                <th>Cadastro</th>
                <th>Ações</th>
              </tr>
            </thead>
            <tbody>
              @for (user of users(); track user.id) {
                <tr [class.row-inactive]="!user.isActive">
                  <td class="cell-primary">{{ user.name }}</td>
                  <td class="text-muted text-sm">{{ user.email }}</td>
                  <td>
                    <div class="roles-wrap">
                      @for (role of user.roles; track role) {
                        <span class="badge" [class]="role === 'Admin' ? 'badge-warning' : 'badge-info'">{{ role }}</span>
                      }
                    </div>
                  </td>
                  <td>
                    <span class="badge" [class]="user.isActive ? 'badge-success' : 'badge-neutral'">
                      {{ user.isActive ? 'Ativo' : 'Inativo' }}
                    </span>
                  </td>
                  <td class="text-muted text-sm">{{ formatDate(user.createdAt) }}</td>
                  <td>
                    <div class="row-actions">
                      <button
                        class="action-btn"
                        [class]="user.isActive ? 'action-warning' : 'action-success'"
                        (click)="toggleActive(user)"
                        [title]="user.isActive ? 'Desativar' : 'Ativar'"
                      >
                        {{ user.isActive ? '⏸' : '▶' }}
                      </button>
                      <button
                        class="action-btn action-info"
                        (click)="openResetPassword(user)"
                        title="Resetar senha"
                      >
                        🔑
                      </button>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <!-- Modal reset senha -->
        @if (showResetModal()) {
          <div class="modal-overlay" (click)="showResetModal.set(false)">
            <div class="modal-card card" (click)="$event.stopPropagation()">
              <h3 class="modal-title">Resetar senha — {{ selectedUser()?.name }}</h3>
              <form [formGroup]="resetForm" (ngSubmit)="onResetPassword()">
                <div class="field" style="margin-bottom: 16px">
                  <label class="field-label">Nova senha *</label>
                  <input
                    type="password"
                    formControlName="newPassword"
                    class="input"
                    placeholder="Mínimo 8 caracteres"
                  />
                </div>
                @if (resetError()) {
                  <div class="form-error">{{ resetError() }}</div>
                }
                <div class="modal-footer">
                  <button type="button" class="btn btn-secondary" (click)="showResetModal.set(false)">Cancelar</button>
                  <button type="submit" class="btn btn-primary" [disabled]="loadingAction()">
                    {{ loadingAction() ? 'Salvando...' : 'Salvar' }}
                  </button>
                </div>
              </form>
            </div>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .page-content { padding: 28px; display: flex; flex-direction: column; gap: 20px; }

    .table-wrap { overflow-x: auto; }
    .table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
    .table th {
      text-align: left; padding: 12px 16px;
      font-size: 0.75rem; font-weight: 600;
      text-transform: uppercase; letter-spacing: 0.04em;
      color: var(--ink3); border-bottom: 1px solid var(--border);
    }
    .table td {
      padding: 12px 16px; border-bottom: 1px solid var(--bg3);
      color: var(--ink2); vertical-align: middle;
    }
    .table tr:last-child td  { border-bottom: none; }
    .table tbody tr:hover    { background: var(--surface-overlay); }
    .table tbody .row-inactive td { opacity: 0.5; }

    .cell-primary { color: var(--ink); font-weight: 500; }
    .text-sm      { font-size: 0.8125rem; }
    .text-muted   { color: var(--ink3); }

    .roles-wrap { display: flex; gap: 4px; flex-wrap: wrap; }

    .row-actions { display: flex; gap: 4px; }
    .action-btn {
      width: 28px; height: 28px; border: 1px solid var(--border);
      background: none; border-radius: var(--radius-sm);
      cursor: pointer; font-size: 13px;
      display: flex; align-items: center; justify-content: center;
      transition: all var(--transition);
    }
    .action-success:hover { background: var(--color-success-bg); border-color: var(--sage2); }
    .action-warning:hover { background: var(--color-warning-bg); border-color: var(--color-warning); }
    .action-info:hover    { background: var(--color-info-bg);    border-color: var(--color-info);    }

    /* Modal */
    .modal-overlay {
      position: fixed; inset: 0;
      background: rgba(0,0,0,.4);
      display: flex; align-items: center; justify-content: center;
      z-index: 100;
    }

    .modal-card {
      width: 100%; max-width: 400px;
      padding: 28px;
      box-shadow: var(--shadow-modal);
    }

    .modal-title { font-size: 1rem; margin-bottom: 20px; }

    .field { display: flex; flex-direction: column; gap: 6px; }
    .field-label { font-size: 0.875rem; font-weight: 500; color: var(--ink2); }

    .form-error {
      margin-top: 12px; padding: 10px 14px;
      background: var(--color-danger-bg); color: var(--color-danger);
      border-radius: var(--radius); font-size: 0.875rem;
    }

    .modal-footer { display: flex; justify-content: flex-end; gap: 10px; margin-top: 20px; }

    .loading-state {
      display: flex; align-items: center;
      justify-content: center; padding: 64px;
    }

    .spinner-lg {
      width: 32px; height: 32px;
      border: 3px solid var(--border);
      border-top-color: var(--sage2);
      border-radius: 50%;
      animation: spin .8s linear infinite; display: block;
    }

    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class AdminUsersComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb  = inject(FormBuilder);

  readonly users         = signal<AdminUserResponse[]>([]);
  readonly loading       = signal(true);
  readonly loadingAction = signal(false);
  readonly showResetModal = signal(false);
  readonly selectedUser  = signal<AdminUserResponse | null>(null);
  readonly resetError    = signal<string | null>(null);

  readonly resetForm = this.fb.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

  ngOnInit(): void {
    this.api.getAdminUsers().subscribe({
      next: u => { this.users.set(u); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  toggleActive(user: AdminUserResponse): void {
    this.api.toggleUserActive(user.id).subscribe({
      next: () => {
        this.users.update(list => list.map(u =>
          u.id === user.id ? { ...u, isActive: !u.isActive } : u
        ));
      }
    });
  }

  openResetPassword(user: AdminUserResponse): void {
    this.selectedUser.set(user);
    this.resetForm.reset();
    this.resetError.set(null);
    this.showResetModal.set(true);
  }

  onResetPassword(): void {
    if (this.resetForm.invalid) return;

    this.loadingAction.set(true);
    const userId = this.selectedUser()!.id;
    const pass   = this.resetForm.value.newPassword!;

    this.api.resetUserPassword(userId, { newPassword: pass }).subscribe({
      next: () => {
        this.showResetModal.set(false);
        this.loadingAction.set(false);
      },
      error: err => {
        this.resetError.set(err.error?.message ?? 'Erro ao resetar senha.');
        this.loadingAction.set(false);
      }
    });
  }

  formatDate(d: string): string {
    return new Date(d).toLocaleDateString('pt-BR');
  }
}
