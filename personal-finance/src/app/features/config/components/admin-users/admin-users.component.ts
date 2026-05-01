import { Component, inject, signal, OnInit } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { animate, style, transition, trigger } from '@angular/animations';
import { ApiService } from '../../../../core/services/api.service';
import { HeaderComponent } from '../../../../shared/components/header/header.component';
import { PaginationComponent } from '../../../../shared/components/pagination/pagination.component';
import { SonicModalComponent } from '../../../../shared/components/modal/sonic-modal/sonic-modal.component';
import { FilterModalComponent } from '../../../../shared/components/filter-modal/filter-modal.component';
import { FilterFieldConfig } from '../../../../shared/components/filter-modal/filter-field-config';
import { AdminUserResponse } from '../../../../core/models/models';

const AVAILABLE_ROLES = [
  { id: 1, name: 'Admin' },
  { id: 2, name: 'User'  },
];

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [HeaderComponent, ReactiveFormsModule, PaginationComponent, SonicModalComponent, FilterModalComponent],
  templateUrl: './admin-users.component.html',
  styleUrls: ['./admin-users.component.css'],
  animations: [
    trigger('backdropAnim', [
      transition(':enter', [style({ opacity: 0 }), animate('200ms ease', style({ opacity: 1 }))]),
      transition(':leave', [animate('180ms ease', style({ opacity: 0 }))]),
    ]),
    trigger('modalAnim', [
      transition(':enter', [
        style({ opacity: 0, transform: 'scale(0.88) translateY(-16px)' }),
        animate('260ms cubic-bezier(0.34,1.56,0.64,1)', style({ opacity: 1, transform: 'scale(1) translateY(0)' })),
      ]),
      transition(':leave', [
        animate('180ms ease-in', style({ opacity: 0, transform: 'scale(0.93) translateY(10px)' })),
      ]),
    ]),
  ]
})
export class AdminUsersComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb  = inject(FormBuilder);

  readonly availableRoles = AVAILABLE_ROLES;

  readonly users          = signal<AdminUserResponse[]>([]);
  readonly loading        = signal(true);
  readonly loadingAction  = signal(false);
  readonly selectedUser   = signal<AdminUserResponse | null>(null);
  readonly totalCount     = signal(0);
  readonly currentPage    = signal(1);
  readonly pageSize       = signal(20);

  readonly filterOpen = signal(false);

  readonly showCreateModal = signal(false);
  readonly showEditModal   = signal(false);
  readonly showResetModal  = signal(false);

  readonly createError = signal<string | null>(null);
  readonly editError   = signal<string | null>(null);
  readonly resetError  = signal<string | null>(null);

  nameFilter   = '';
  emailFilter  = '';
  statusFilter = '';

  get filterFields(): FilterFieldConfig[] {
    return [
      { key: 'name',   label: 'Nome',   type: 'text',   value: this.nameFilter },
      { key: 'email',  label: 'E-mail', type: 'text',   value: this.emailFilter },
      { key: 'status', label: 'Status', type: 'select', value: this.statusFilter,
        options: [{ value: '', label: 'Todos' }, { value: 'true', label: 'Ativo' }, { value: 'false', label: 'Inativo' }] },
    ];
  }

  readonly createForm = this.fb.group({
    name:     ['', [Validators.required]],
    email:    ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  readonly editForm = this.fb.group({
    name: ['', [Validators.required]],
  });

  readonly resetForm = this.fb.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
  });

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.loading.set(true);
    const isActive = this.statusFilter === '' ? undefined : this.statusFilter === 'true';
    this.api.getAdminUsers({
      pageNumber: this.currentPage(),
      pageSize:   this.pageSize(),
      name:       this.nameFilter  || undefined,
      email:      this.emailFilter || undefined,
      isActive,
    }).subscribe({
      next: result => { this.users.set(result.items); this.totalCount.set(result.totalCount); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  applyFilters(): void { this.currentPage.set(1); this.load(); }

  clearFilters(): void {
    this.nameFilter = ''; this.emailFilter = ''; this.statusFilter = '';
    this.currentPage.set(1); this.load();
  }

  onFilterApply(values: Record<string, unknown>): void {
    this.nameFilter   = (values['name']   as string) ?? '';
    this.emailFilter  = (values['email']  as string) ?? '';
    this.statusFilter = (values['status'] as string) ?? '';
    this.filterOpen.set(false);
    this.applyFilters();
  }

  onFilterClear(): void {
    this.filterOpen.set(false);
    this.clearFilters();
  }

  onPageChange(page: number): void { this.currentPage.set(page); this.load(); }
  onPageSizeChange(size: number): void { this.pageSize.set(size); this.currentPage.set(1); this.load(); }

  toggleActive(user: AdminUserResponse): void {
    this.api.toggleUserActive(user.id).subscribe({
      next: () => this.users.update(list => list.map(u => u.id === user.id ? { ...u, isActive: !u.isActive } : u)),
    });
  }

  openCreate(): void {
    this.createForm.reset();
    this.createError.set(null);
    this.showCreateModal.set(true);
  }

  onCreateUser(): void {
    if (this.createForm.invalid) return;
    this.loadingAction.set(true);
    const { name, email, password } = this.createForm.value;
    this.api.createAdminUser({ name: name!, email: email!, password: password! }).subscribe({
      next: created => {
        this.users.update(list => [created, ...list]);
        this.totalCount.update(c => c + 1);
        this.showCreateModal.set(false);
        this.loadingAction.set(false);
      },
      error: err => {
        this.createError.set(err.error?.message ?? 'Erro ao criar usuário.');
        this.loadingAction.set(false);
      },
    });
  }

  openEdit(user: AdminUserResponse): void {
    this.selectedUser.set(user);
    this.editForm.patchValue({ name: user.name });
    this.editError.set(null);
    this.showEditModal.set(true);
  }

  onUpdateUser(): void {
    if (this.editForm.invalid) return;
    this.loadingAction.set(true);
    const userId = this.selectedUser()!.id;
    const { name } = this.editForm.value;
    this.api.updateAdminUser(userId, { name: name! }).subscribe({
      next: updated => {
        this.users.update(list => list.map(u => u.id === userId ? updated : u));
        this.showEditModal.set(false);
        this.loadingAction.set(false);
      },
      error: err => {
        this.editError.set(err.error?.message ?? 'Erro ao atualizar usuário.');
        this.loadingAction.set(false);
      },
    });
  }

  onRoleToggle(roleId: number, roleName: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const user    = this.selectedUser()!;
    this.loadingAction.set(true);

    const req$ = checked
      ? this.api.assignRole(user.id, { roleId })
      : this.api.removeRole(user.id, roleId);

    req$.subscribe({
      next: () => {
        const updatedRoles = checked
          ? [...user.roles, roleName]
          : user.roles.filter(r => r !== roleName);
        const updatedUser = { ...user, roles: updatedRoles };
        this.selectedUser.set(updatedUser);
        this.users.update(list => list.map(u => u.id === user.id ? updatedUser : u));
        this.loadingAction.set(false);
      },
      error: err => {
        this.editError.set(err.error?.message ?? 'Erro ao alterar role.');
        this.loadingAction.set(false);
        (event.target as HTMLInputElement).checked = !checked;
      },
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
      next: () => { this.showResetModal.set(false); this.loadingAction.set(false); },
      error: err => { this.resetError.set(err.error?.message ?? 'Erro ao resetar senha.'); this.loadingAction.set(false); },
    });
  }

  formatDate(d: string): string {
    return new Date(d).toLocaleDateString('pt-BR');
  }
}
