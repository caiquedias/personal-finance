import { Component, inject, signal, OnInit } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { HeaderComponent } from '../../../shared/components/header/header.component';
import { CategoryResponse, LookupItem } from '../../../core/models/models';

@Component({
  selector: 'app-config',
  standalone: true,
  imports: [HeaderComponent, ReactiveFormsModule],
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
              @if (!cat.isGlobal) {
                <button class="action-btn action-danger" (click)="deleteCategory(cat.id)">✕</button>
              }
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
            </div>
          }
        </div>
      }

    </div>
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

    .action-btn {
      width: 28px; height: 28px; border: 1px solid var(--border);
      background: none; border-radius: var(--radius-sm);
      cursor: pointer; font-size: 12px;
      display: flex; align-items: center; justify-content: center;
      transition: all var(--transition);
    }
    .action-danger:hover { background: var(--color-danger-bg); border-color: var(--rust); color: var(--rust); }
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

  ngOnInit(): void {
    this.api.getCategories().subscribe(c => this.categories.set(c));
    this.api.getPaymentStatuses().subscribe(p => this.paymentStatuses.set(p));
    this.api.getSourceTypes().subscribe(s => this.sourceTypes.set(s));
    this.api.getFortnightTypes().subscribe(f => this.fortnightTypes.set(f));
  }

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

  deleteCategory(id: string): void {
    if (!confirm('Confirma a exclusão?')) return;
    this.api.deleteCategory(id).subscribe({
      next: () => this.categories.update(list => list.filter(c => c.id !== id))
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
}
