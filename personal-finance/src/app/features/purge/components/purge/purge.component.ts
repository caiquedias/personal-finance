import { Component, inject, signal, OnInit } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { trigger, transition, style, animate } from '@angular/animations';
import { ApiService } from '../../../../core/services/api.service';
import { EligiblePeriodResponse, PurgeResultResponse, MONTH_NAMES } from '../../../../core/models/models';

@Component({
  selector: 'app-purge',
  standalone: true,
  imports: [DecimalPipe],
  animations: [
    trigger('backdropAnim', [
      transition(':enter', [style({opacity:0}), animate('200ms ease', style({opacity:1}))]),
      transition(':leave', [animate('180ms ease', style({opacity:0}))])
    ]),
    trigger('modalAnim', [
      transition(':enter', [
        style({opacity:0, transform:'scale(0.88) translateY(-16px)'}),
        animate('260ms cubic-bezier(0.34,1.56,0.64,1)',
          style({opacity:1, transform:'scale(1) translateY(0)'}))
      ]),
      transition(':leave', [
        animate('180ms ease-in',
          style({opacity:0, transform:'scale(0.93) translateY(10px)'}))
      ])
    ])
  ],
  template: `
    <div class="purge-container">
      @if (apiError()) {
        <div class="error">{{ apiError() }}</div>
      }

      @if (eligiblePeriods().length === 0) {
        <div class="empty-state">Nenhum período elegível para expurgo.</div>
      } @else {
        <ul class="periods-list">
          @for (period of eligiblePeriods(); track period.periodId) {
            <li class="period-item">
              <span class="period-year">{{ period.year }}</span>
              <span class="period-month">{{ monthName(period.month) }}</span>
              <span class="period-income">{{ period.totalIncome | number:'1.2-2' }}</span>
              <span class="period-expense">{{ period.totalExpense | number:'1.2-2' }}</span>
              <span class="period-count">{{ period.itemCount }}</span>
              <button (click)="openConfirmModal(period)">Expurgar</button>
            </li>
          }
        </ul>
      }

      @if (purgeResult()) {
        <div class="purge-result">
          Expurgo concluído. Espaço estimado liberado: {{ purgeResult()!.estimatedSpaceKb }} KB
        </div>
      }

      @if (confirmModalOpen()) {
        <div class="modal-overlay" @backdropAnim (click)="closeConfirmModal()"></div>
        <div class="modal" @modalAnim>
          <h2>Confirmar Expurgo</h2>
          @if (selectedPeriod()) {
            <p>{{ selectedPeriod()!.year }} — {{ monthName(selectedPeriod()!.month) }}</p>
          }
          <p class="modal-confirm-text">
            Confirmo que o arquivo CSV foi salvo com sucesso. Desejo excluir permanentemente os dados de {{ monthName(selectedPeriod()!.month) }}/{{ selectedPeriod()!.year }} do banco de dados.
          </p>
          <button (click)="downloadCsv(selectedPeriod()!)">Baixar CSV</button>
          <button class="btn-danger" [disabled]="!csvReady()" (click)="confirmPurge()">Confirmar</button>
          <button (click)="closeConfirmModal()">Cancelar</button>
        </div>
      }
    </div>
  `,
})
export class PurgeComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly eligiblePeriods  = signal<EligiblePeriodResponse[]>([]);
  readonly selectedPeriod   = signal<EligiblePeriodResponse | null>(null);
  readonly confirmModalOpen = signal(false);
  readonly csvReady         = signal(false);
  readonly purgeResult      = signal<PurgeResultResponse | null>(null);
  readonly apiError         = signal<string | null>(null);

  // Converte número de mês (1-12) para nome em PT-BR
  monthName(month: number): string {
    return MONTH_NAMES[month - 1] ?? '';
  }

  ngOnInit(): void {
    this.api.getEligiblePeriods().subscribe({
      next:  periods => this.eligiblePeriods.set(periods),
      error: err     => this.apiError.set(err?.error?.message ?? 'Erro ao carregar períodos.'),
    });
  }

  downloadCsv(period: EligiblePeriodResponse): void {
    this.api.exportPurgeCsv(period.periodId).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.setAttribute('download', `expurgo-${period.periodId}.csv`);
        a.click();
        URL.revokeObjectURL(url);
        this.csvReady.set(true);
      },
      error: () => this.csvReady.set(false),
    });
  }

  openConfirmModal(period: EligiblePeriodResponse): void {
    this.csvReady.set(false);
    this.selectedPeriod.set(period);
    this.confirmModalOpen.set(true);
  }

  closeConfirmModal(): void {
    this.confirmModalOpen.set(false);
  }

  confirmPurge(): void {
    const period = this.selectedPeriod();
    if (!period) return;

    this.api.executePurge(period.periodId).subscribe({
      next: result => {
        // Remove o período expurgado da lista
        this.eligiblePeriods.update(list => list.filter(p => p.periodId !== period.periodId));
        this.purgeResult.set(result);
        this.confirmModalOpen.set(false);
      },
      error: err => {
        this.apiError.set(err?.error?.message ?? 'Erro ao executar expurgo.');
        this.confirmModalOpen.set(false);
      },
    });
  }
}
