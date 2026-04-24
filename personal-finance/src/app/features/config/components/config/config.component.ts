import { Component, inject, signal, OnInit } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { trigger, style, animate, transition } from '@angular/animations';
import { ApiService } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { HeaderComponent } from '../../../../shared/components/header/header.component';
import { SonicModalComponent } from '../../../../shared/components/modal/sonic-modal/sonic-modal.component';
import { CategoryResponse, LookupItem } from '../../../../core/models/models';

@Component({
  selector: 'app-config',
  standalone: true,
  imports: [HeaderComponent, ReactiveFormsModule, SonicModalComponent],
  templateUrl: './config.component.html',
  styleUrls: ['./config.component.css'],
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
  ]
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
    icon:  ['data:image/png;base64,'],
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

  getIcon(cat: any): string {
    return `data:image/png;base64,${cat.icon}`;
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
