import { Component, inject, signal, OnInit } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { trigger, style, animate, transition } from '@angular/animations';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { HeaderComponent } from '../../../shared/components/header/header.component';
import { SonicModalComponent } from '../../../shared/components/modal/sonic-modal.component';
import { CategoryResponse, LookupItem } from '../../../core/models/models';

@Component({
  selector: 'app-config',
  standalone: true,
  imports: [HeaderComponent, ReactiveFormsModule, SonicModalComponent],
  animations: [
    trigger('backdropAnim', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('200ms ease', style({ opacity: 1 }))
      ]),
      transition(':leave', [
        animate('180ms ease', style({ opacity: 0 }))
      ])
    ]),
    trigger('modalAnim', [
      transition(':enter', [
        style({ opacity: 0, transform: 'scale(0.88) translateY(-16px)' }),
        animate('260ms cubic-bezier(0.34, 1.56, 0.64, 1)',
          style({ opacity: 1, transform: 'scale(1) translateY(0)' }))
      ]),
      transition(':leave', [
        animate('180ms ease-in',
          style({ opacity: 0, transform: 'scale(0.93) translateY(10px)' }))
      ])
    ])
  ],
  template: `
    <app-header title="Configurações" subtitle="Categorias e tabelas de configuração" />

    <div class="page-content">

      <!-- Tabs -->
      <div class="tabs">
        <button class="tab" [class.active]="tab() === 'categories'" (click)="tab.set('categories')">Categorias</button>
        <button class="tab" [class.active]="tab() === 'payment'"    (click)="tab.set('payment')">Status de Pagamento</button>
        <button class="tab" [class.active]="tab() === 'source'"     (click)="tab.set('source')">Tipos de Fonte</button>
        <button class="tab" [class.active]="tab() === 'fortnight'"  (click)="tab.set('fortnight')">Quinzenas</button>
      </div>

      <!-- CATEGORIAS -->
      @if (tab() === 'categories') {
        <div class="section-header">
          <span class="section-title">Suas categorias</span>
          <button class="btn btn-primary btn-sm" (click)="showCatForm.update(v => !v)">
            {{ showCatForm() ? 'Cancelar' : '+ Nova categoria' }}
          </button>
        </div>

        @if (showCatForm()) {
          <div class="card form-card">
            <form [formGroup]="catForm" (ngSubmit)="onCreateCategory()">
              <div class="form-row">
                <div class="field">
                  <label class="field-label">Nome *</label>
                  <input formControlName="name" class="input" placeholder="Ex: Moradia" />
                </div>
                <div class="field">
                  <label class="field-label">Cor *</label>
                  <div class="color-input-wrap">
                    <input type="color" formControlName="color" class="color-picker" />
                    <input formControlName="color" class="input" placeholder="#1E4D2B" maxlength="7" />
                  </div>
                </div>
                <div class="field">
                  <label class="field-label">Ícone</label>
                  <input formControlName="icon" class="input" placeholder="home, car..." />
                </div>
                <div class="form-actions">
                  <button type="submit" class="btn btn-primary btn-sm" [disabled]="loadingCat()">
                    {{ loadingCat() ? 'Salvando...' : 'Criar' }}
                  </button>
                </div>
              </div>
              @if (catError()) {
                <div class="form-error">{{ catError() }}</div>
              }
            </form>
          </div>
        }

        <div class="categories-grid">
          @for (cat of categories(); track cat.id) {
            <div class="category-card card">
              <div class="category-color" [style.background]="cat.color"></div>
              <div class="category-info">
                <span class="category-name">{{ cat.name }}</span>
                @if (cat.isGlobal) {
                  <span class="badge badge-info" style="font-size:0.7rem">Global</span>
                }
              </div>
              <div class="card-actions">
                @if (!cat.isGlobal || auth.isAdmin()) {
                  <button class="action-btn action-edit" title="Editar" (click)="openEditCat(cat)">
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                  </button>
                }
                @if (!cat.isGlobal || auth.isAdmin()) {
                  <button class="action-btn action-danger" title="Excluir" (click)="deleteCategory(cat.id, cat.isGlobal)">✕</button>
                }
              </div>
            </div>
          }
        </div>
      }

      <!-- STATUS DE PAGAMENTO -->
      @if (tab() === 'payment') {
        <div class="section-header">
          <span class="section-title">Status de pagamento</span>
          @if (auth.isAdmin()) {
            <button class="btn btn-primary btn-sm" (click)="showPayForm.update(v => !v)">
              {{ showPayForm() ? 'Cancelar' : '+ Novo status' }}
            </button>
          }
        </div>

        @if (showPayForm() && auth.isAdmin()) {
          <div class="card form-card">
            <form [formGroup]="payForm" (ngSubmit)="onCreatePaymentStatus()">
              <div class="form-row">
                <div class="field">
                  <label class="field-label">Nome *</label>
                  <input formControlName="name" class="input" placeholder="Ex: Parcelado" />
                </div>
                <div class="field field-wide">
                  <label class="field-label">Descrição *</label>
                  <input formControlName="description" class="input" placeholder="Descreva o status..." />
                </div>
                <div class="form-actions">
                  <button type="submit" class="btn btn-primary btn-sm" [disabled]="loadingLookup()">Criar</button>
                </div>
              </div>
            </form>
          </div>
        }

        <div class="lookup-list card">
          @for (item of paymentStatuses(); track item.id) {
            <div class="lookup-item">
              <div class="lookup-info">
                <span class="lookup-name">{{ item.name }}</span>
                @if (item.description) {
                  <span class="lookup-desc">{{ item.description }}</span>
                }
              </div>
              @if (item.isSystemSeed) {
                <span class="badge badge-neutral" style="font-size:0.7rem">Sistema</span>
              }
              @if (!item.isSystemSeed && auth.isAdmin()) {
                <div class="card-actions">
                  <button class="action-btn action-edit" title="Editar" (click)="openEditLookup(item, 'payment')">
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                  </button>
                  <button class="action-btn action-danger" title="Excluir" (click)="deleteLookupItem(item.id, 'payment')">✕</button>
                </div>
              }
            </div>
          }
        </div>
      }

      <!-- TIPOS DE FONTE -->
      @if (tab() === 'source') {
        <div class="section-header">
          <span class="section-title">Tipos de fonte</span>
          @if (auth.isAdmin()) {
            <button class="btn btn-primary btn-sm" (click)="showSrcForm.update(v => !v)">
              {{ showSrcForm() ? 'Cancelar' : '+ Novo tipo' }}
            </button>
          }
        </div>

        @if (showSrcForm() && auth.isAdmin()) {
          <div class="card form-card">
            <form [formGroup]="srcForm" (ngSubmit)="onCreateSourceType()">
              <div class="form-row">
                <div class="field field-wide">
                  <label class="field-label">Nome *</label>
                  <input formControlName="name" class="input" placeholder="Ex: Empresarial" />
                </div>
                <div class="form-actions">
                  <button type="submit" class="btn btn-primary btn-sm">Criar</button>
                </div>
              </div>
            </form>
          </div>
        }

        <div class="lookup-list card">
          @for (item of sourceTypes(); track item.id) {
            <div class="lookup-item">
              <span class="lookup-name">{{ item.name }}</span>
              @if (item.isSystemSeed) {
                <span class="badge badge-neutral" style="font-size:0.7rem">Sistema</span>
              }
              @if (!item.isSystemSeed && auth.isAdmin()) {
                <div class="card-actions">
                  <button class="action-btn action-edit" title="Editar" (click)="openEditLookup(item, 'source')">
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                  </button>
                  <button class="action-btn action-danger" title="Excluir" (click)="deleteLookupItem(item.id, 'source')">✕</button>
                </div>
              }
            </div>
          }
        </div>
      }

      <!-- QUINZENAS -->
      @if (tab() === 'fortnight') {
        <div class="section-header">
          <span class="section-title">Tipos de quinzena</span>
          @if (auth.isAdmin()) {
            <button class="btn btn-primary btn-sm" (click)="showFnForm.update(v => !v)">
              {{ showFnForm() ? 'Cancelar' : '+ Novo tipo' }}
            </button>
          }
        </div>

        @if (showFnForm() && auth.isAdmin()) {
          <div class="card form-card">
            <form [formGroup]="fnForm" (ngSubmit)="onCreateFortnightType()">
              <div class="form-row">
                <div class="field field-wide">
                  <label class="field-label">Nome *</label>
                  <input formControlName="name" class="input" placeholder="Ex: Third" />
                </div>
                <div class="form-actions">
                  <button type="submit" class="btn btn-primary btn-sm">Criar</button>
                </div>
              </div>
            </form>
          </div>
        }

        <div class="lookup-list card">
          @for (item of fortnightTypes(); track item.id) {
            <div class="lookup-item">
              <span class="lookup-name">{{ item.name }}</span>
              @if (item.isSystemSeed) {
                <span class="badge badge-neutral" style="font-size:0.7rem">Sistema</span>
              }
              @if (!item.isSystemSeed && auth.isAdmin()) {
                <div class="card-actions">
                  <button class="action-btn action-edit" title="Editar" (click)="openEditLookup(item, 'fortnight')">
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                  </button>
                  <button class="action-btn action-danger" title="Excluir" (click)="deleteLookupItem(item.id, 'fortnight')">✕</button>
                </div>
              }
            </div>
          }
        </div>
      }

    </div>

    <!-- MODAL DE EDIÇÃO -->
    @if (modalOpen()) {
      <div class="modal-overlay" @backdropAnim (click)="closeModal()"></div>
      <div class="modal-center" @modalAnim>
        <app-sonic-modal [title]="modalTitle()" (closed)="closeModal()">

          @if (modalMode() === 'edit-cat') {
            <form [formGroup]="editCatForm" (ngSubmit)="onSaveCat()" class="modal-form">
              <div class="form-grid">
                <div class="field field-full">
                  <label class="field-label">Nome *</label>
                  <input formControlName="name" class="input" placeholder="Ex: Moradia" />
                </div>
                <div class="field">
                  <label class="field-label">Cor *</label>
                  <div class="color-input-wrap">
                    <input type="color" formControlName="color" class="color-picker" />
                    <input formControlName="color" class="input" placeholder="#1E4D2B" maxlength="7" />
                  </div>
                </div>
                <div class="field">
                  <label class="field-label">Ícone</label>
                  <input formControlName="icon" class="input" placeholder="home, car..." />
                </div>
              </div>
              @if (modalError()) {
                <div class="form-error">{{ modalError() }}</div>
              }
              <div class="modal-footer">
                <button type="button" class="btn btn-ghost btn-sm" (click)="closeModal()">Cancelar</button>
                <button type="submit" class="btn btn-primary btn-sm" [disabled]="saving()">
                  {{ saving() ? 'Salvando...' : 'Salvar' }}
                </button>
              </div>
            </form>
          }

          @if (modalMode() === 'edit-lookup') {
            <form [formGroup]="editLookupForm" (ngSubmit)="onSaveLookup()" class="modal-form">
              <div class="form-grid">
                <div class="field field-full">
                  <label class="field-label">Nome *</label>
                  <input formControlName="name" class="input" />
                </div>
                @if (editingLookupTab === 'payment') {
                  <div class="field field-full">
                    <label class="field-label">Descrição</label>
                    <input formControlName="description" class="input" />
                  </div>
                }
              </div>
              @if (modalError()) {
                <div class="form-error">{{ modalError() }}</div>
              }
              <div class="modal-footer">
                <button type="button" class="btn btn-ghost btn-sm" (click)="closeModal()">Cancelar</button>
                <button type="submit" class="btn btn-primary btn-sm" [disabled]="saving()">
                  {{ saving() ? 'Salvando...' : 'Salvar' }}
                </button>
              </div>
            </form>
          }

        </app-sonic-modal>
      </div>
    }
  `,
  styles: [`
    .page-content { padding: 28px; display: flex; flex-direction: column; gap: 20px; }

    .tabs { display: flex; gap: 4px; border-bottom: 1px solid var(--border); }
    .tab {
      padding: 10px 16px; border: none; background: none;
      color: var(--ink3); font-size: 0.875rem; font-weight: 500;
      cursor: pointer; border-bottom: 2px solid transparent;
      margin-bottom: -1px; transition: all var(--transition);
    }
    .tab:hover  { color: var(--ink); }
    .tab.active { color: var(--sage2); border-bottom-color: var(--sage2); }

    .section-header {
      display: flex; align-items: center;
      justify-content: space-between;
    }

    .form-card { padding: 20px; }
    .form-row {
      display: flex; align-items: flex-end;
      gap: 16px; flex-wrap: wrap;
    }
    .field { display: flex; flex-direction: column; gap: 6px; min-width: 160px; }
    .field-wide { flex: 1; min-width: 240px; }
    .field-label { font-size: 0.875rem; font-weight: 500; color: var(--ink2); }
    .form-actions { display: flex; gap: 8px; align-items: flex-end; padding-bottom: 1px; }

    .form-error {
      margin-top: 12px; padding: 10px 14px;
      background: var(--color-danger-bg); color: var(--color-danger);
      border-radius: var(--radius); font-size: 0.875rem;
    }

    .color-input-wrap { display: flex; gap: 8px; align-items: center; }
    .color-picker { width: 40px; height: 40px; padding: 2px; border: 1px solid var(--border); border-radius: var(--radius); cursor: pointer; }

    /* Categories */
    .categories-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 12px;
    }

    .category-card {
      display: flex; align-items: center; gap: 12px;
      padding: 12px 16px;
    }

    .category-color {
      width: 16px; height: 16px;
      border-radius: 4px; flex-shrink: 0;
    }

    .category-info {
      flex: 1; display: flex; flex-direction: column; gap: 2px;
    }

    .category-name { font-size: 0.875rem; font-weight: 500; color: var(--ink); }

    /* Lookup list */
    .lookup-list { overflow: hidden; }

    .lookup-item {
      display: flex; align-items: center;
      justify-content: space-between;
      padding: 12px 20px;
      border-bottom: 1px solid var(--bg3);
    }

    .lookup-item:last-child { border-bottom: none; }

    .lookup-info { display: flex; flex-direction: column; gap: 2px; }
    .lookup-name { font-size: 0.875rem; font-weight: 500; color: var(--ink); }
    .lookup-desc { font-size: 0.8125rem; color: var(--ink3); }

    .card-actions { display: flex; gap: 6px; align-items: center; padding-bottom: 63px;}

    .action-btn {
      width: 28px; height: 28px; border: 1px solid var(--border);
      background: none; border-radius: var(--radius-sm);
      cursor: pointer; font-size: 12px;
      display: flex; align-items: center; justify-content: center;
      transition: all var(--transition);
    }
    .action-edit:hover  { background: var(--bg2); border-color: var(--ink3); color: var(--ink); }
    .action-danger:hover { background: var(--color-danger-bg); border-color: var(--rust); color: var(--rust); }

    /* Modal */
    .modal-overlay {
      position: fixed; inset: 0;
      background: rgba(0,0,0,0.45);
      z-index: 100;
    }

    .modal-center {
      position: fixed; inset: 0;
      display: flex; align-items: center; justify-content: center;
      z-index: 101; pointer-events: none;
    }

    .modal-center > * { pointer-events: all; }

    .modal-form { display: flex; flex-direction: column; gap: 0; }

    .form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
      padding: 20px 24px;
    }

    .field-full { grid-column: 1 / -1; }

    .modal-footer {
      display: flex; justify-content: flex-end; gap: 8px;
      padding: 16px 24px;
      border-top: 1px solid var(--border);
    }
  `]
})
export class ConfigComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly auth        = inject(AuthService);
  private readonly fb  = inject(FormBuilder);

  readonly tab              = signal<'categories' | 'payment' | 'source' | 'fortnight'>('categories');
  readonly categories       = signal<CategoryResponse[]>([]);
  readonly paymentStatuses  = signal<LookupItem[]>([]);
  readonly sourceTypes      = signal<LookupItem[]>([]);
  readonly fortnightTypes   = signal<LookupItem[]>([]);

  readonly showCatForm  = signal(false);
  readonly showPayForm  = signal(false);
  readonly showSrcForm  = signal(false);
  readonly showFnForm   = signal(false);

  readonly loadingCat    = signal(false);
  readonly loadingLookup = signal(false);
  readonly catError      = signal<string | null>(null);

  // Modal
  readonly modalOpen  = signal(false);
  readonly modalMode  = signal<'edit-cat' | 'edit-lookup'>('edit-cat');
  readonly saving     = signal(false);
  readonly modalError = signal<string | null>(null);

  private editingCatId:     string | null = null;
  editingLookupId:          number | null = null;
  editingLookupTab:         'payment' | 'source' | 'fortnight' | null = null;

  readonly catForm = this.fb.group({
    name:  ['', Validators.required],
    color: ['#6b8f71', Validators.required],
    icon:  [''],
  });

  readonly payForm = this.fb.group({
    name:        ['', Validators.required],
    description: ['', Validators.required],
  });

  readonly srcForm = this.fb.group({ name: ['', Validators.required] });
  readonly fnForm  = this.fb.group({ name: ['', Validators.required] });

  readonly editCatForm = this.fb.group({
    name:  ['', Validators.required],
    color: ['#6b8f71', Validators.required],
    icon:  [''],
  });

  readonly editLookupForm = this.fb.group({
    name:        ['', Validators.required],
    description: [''],
  });

  modalTitle(): string {
    if (this.modalMode() === 'edit-cat') return 'Editar Categoria';
    const labels: Record<string, string> = {
      payment:   'Editar Status de Pagamento',
      source:    'Editar Tipo de Fonte',
      fortnight: 'Editar Tipo de Quinzena',
    };
    return labels[this.editingLookupTab ?? ''] ?? 'Editar';
  }

  ngOnInit(): void {
    this.api.getCategories().subscribe(c => this.categories.set(c));
    this.api.getPaymentStatuses().subscribe(p => this.paymentStatuses.set(p));
    this.api.getSourceTypes().subscribe(s => this.sourceTypes.set(s));
    this.api.getFortnightTypes().subscribe(f => this.fortnightTypes.set(f));
  }

  // ── Criação ──────────────────────────────────────────────────────────────────

  onCreateCategory(): void {
    if (this.catForm.invalid) { this.catForm.markAllAsTouched(); return; }
    this.loadingCat.set(true);
    const v = this.catForm.getRawValue();
    this.api.createCategory({ name: v.name!, color: v.color!, icon: v.icon || undefined }).subscribe({
      next: c => {
        this.categories.update(list => [...list, c]);
        this.catForm.reset({ color: '#6b8f71' });
        this.showCatForm.set(false);
        this.loadingCat.set(false);
      },
      error: err => {
        this.catError.set(err.error?.message ?? 'Erro ao criar categoria.');
        this.loadingCat.set(false);
      }
    });
  }

  onCreatePaymentStatus(): void {
    if (this.payForm.invalid) return;
    const v = this.payForm.getRawValue();
    this.api.createPaymentStatus({ name: v.name!, description: v.description! }).subscribe({
      next: item => {
        this.paymentStatuses.update(list => [...list, item]);
        this.payForm.reset();
        this.showPayForm.set(false);
      }
    });
  }

  onCreateSourceType(): void {
    if (this.srcForm.invalid) return;
    this.api.createSourceType({ name: this.srcForm.value.name! }).subscribe({
      next: item => {
        this.sourceTypes.update(list => [...list, item]);
        this.srcForm.reset();
        this.showSrcForm.set(false);
      }
    });
  }

  onCreateFortnightType(): void {
    if (this.fnForm.invalid) return;
    this.api.createFortnightType({ name: this.fnForm.value.name! }).subscribe({
      next: item => {
        this.fortnightTypes.update(list => [...list, item]);
        this.fnForm.reset();
        this.showFnForm.set(false);
      }
    });
  }

  // ── Exclusão ─────────────────────────────────────────────────────────────────

  deleteCategory(id: string, isGlobal: boolean): void {
    const msg = isGlobal
      ? 'Confirma a exclusão da categoria global?'
      : 'Confirma a exclusão?';
    if (!confirm(msg)) return;
    this.api.deleteCategory(id).subscribe({
      next: () => this.categories.update(list => list.filter(c => c.id !== id))
    });
  }

  deleteLookupItem(id: number, tab: 'payment' | 'source' | 'fortnight'): void {
    if (!confirm('Confirma a exclusão?')) return;
    const req$ = tab === 'payment'
      ? this.api.deletePaymentStatus(id)
      : tab === 'source'
        ? this.api.deleteSourceType(id)
        : this.api.deleteFortnightType(id);

    req$.subscribe({
      next: () => {
        if (tab === 'payment')   this.paymentStatuses.update(list => list.filter(i => i.id !== id));
        if (tab === 'source')    this.sourceTypes.update(list => list.filter(i => i.id !== id));
        if (tab === 'fortnight') this.fortnightTypes.update(list => list.filter(i => i.id !== id));
      },
      error: err => alert(err.error?.message ?? 'Erro ao excluir item.')
    });
  }

  // ── Modal Categoria ───────────────────────────────────────────────────────────

  openEditCat(cat: CategoryResponse): void {
    this.editingCatId = cat.id;
    this.modalMode.set('edit-cat');
    this.modalError.set(null);
    this.editCatForm.patchValue({
      name:  cat.name,
      color: cat.color,
      icon:  cat.icon ?? '',
    });
    this.modalOpen.set(true);
  }

  onSaveCat(): void {
    if (this.editCatForm.invalid || !this.editingCatId) return;
    this.saving.set(true);
    this.modalError.set(null);
    const v = this.editCatForm.getRawValue();
    this.api.updateCategory(this.editingCatId, {
      name:  v.name!,
      color: v.color!,
      icon:  v.icon || undefined,
    }).subscribe({
      next: () => {
        this.categories.update(list => list.map(c =>
          c.id === this.editingCatId
            ? { ...c, name: v.name!, color: v.color!, icon: v.icon || null }
            : c
        ));
        this.saving.set(false);
        this.closeModal();
      },
      error: err => {
        this.modalError.set(err.error?.message ?? 'Erro ao salvar categoria.');
        this.saving.set(false);
      }
    });
  }

  // ── Modal Lookup ──────────────────────────────────────────────────────────────

  openEditLookup(item: LookupItem, tab: 'payment' | 'source' | 'fortnight'): void {
    this.editingLookupId  = item.id;
    this.editingLookupTab = tab;
    this.modalMode.set('edit-lookup');
    this.modalError.set(null);
    this.editLookupForm.patchValue({
      name:        item.name,
      description: item.description ?? '',
    });
    this.modalOpen.set(true);
  }

  onSaveLookup(): void {
    if (this.editLookupForm.invalid || this.editingLookupId === null) return;
    this.saving.set(true);
    this.modalError.set(null);
    const v   = this.editLookupForm.getRawValue();
    const id  = this.editingLookupId;
    const tab = this.editingLookupTab!;

    const req$ = tab === 'payment'
      ? this.api.updatePaymentStatus(id, { name: v.name!, description: v.description ?? '' })
      : tab === 'source'
        ? this.api.updateSourceType(id, { name: v.name! })
        : this.api.updateFortnightType(id, { name: v.name! });

    req$.subscribe({
      next: updated => {
        if (tab === 'payment')   this.paymentStatuses.update(list => list.map(i => i.id === id ? updated : i));
        if (tab === 'source')    this.sourceTypes.update(list => list.map(i => i.id === id ? updated : i));
        if (tab === 'fortnight') this.fortnightTypes.update(list => list.map(i => i.id === id ? updated : i));
        this.saving.set(false);
        this.closeModal();
      },
      error: err => {
        this.modalError.set(err.error?.message ?? 'Erro ao salvar.');
        this.saving.set(false);
      }
    });
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.editingCatId     = null;
    this.editingLookupId  = null;
    this.editingLookupTab = null;
  }
}
