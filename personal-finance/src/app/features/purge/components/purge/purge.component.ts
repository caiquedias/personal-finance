import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { trigger, transition, style, animate } from '@angular/animations';
import { ApiService } from '../../../../core/services/api.service';
import { EligiblePeriodResponse, PurgeResultResponse, PurgeRecordResponse, MONTH_NAMES } from '../../../../core/models/models';
import { HeaderComponent } from '../../../../shared/components/header/header.component';
import { CurrencyBrlPipe } from '../../../../shared/pipes/currency-brl.pipe';

@Component({
  selector: 'app-purge',
  standalone: true,
  imports: [DatePipe, HeaderComponent, CurrencyBrlPipe],
  styleUrls: ['./purge.component.css'],
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

      <app-header title="Expurgo de Períodos" subtitle="Exclua permanentemente dados de períodos fechados e exportados">
        <button class="btn btn-sm btn-analise" (click)="navigateToAnalysis()">Análise</button>
      </app-header>

      <!-- Warning banner estático -->
      <div class="warning-callout">
        <svg class="ico-warning" width="18" height="18" viewBox="0 0 24 24" fill="none">
          <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" stroke="#C4964A" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          <line x1="12" y1="9" x2="12" y2="13" stroke="#C4964A" stroke-width="2" stroke-linecap="round"/>
          <line x1="12" y1="17" x2="12.01" y2="17" stroke="#C4964A" stroke-width="2.5" stroke-linecap="round"/>
        </svg>
        <p><strong>Atenção:</strong> O expurgo remove <strong>permanentemente</strong> os dados do banco de dados. Certifique-se de exportar o CSV do período antes de prosseguir. Esta operação <strong>não pode ser desfeita</strong>.</p>
      </div>

      <!-- Seção de cards de períodos elegíveis -->
      <section class="eligible-section">
        <div class="section-header">
          <h2 class="section-title">Períodos elegíveis para expurgo</h2>
          @if (eligiblePeriods().length > 0) {
            <span class="count-badge">{{ eligiblePeriods().length }}</span>
          }
        </div>

        @if (eligiblePeriods().length > 0) {
          <div class="cards-grid">
            @for (period of eligiblePeriods(); track period.periodId; let i = $index) {
              <div class="period-card">
                <div class="card-accent-line"></div>
                <div class="card-header">
                  <div>
                    <div class="card-month">{{ monthName(period.month) }}</div>
                    <div class="card-year">{{ period.year }}</div>
                  </div>
                  <span class="badge-eligible">
                    <svg width="7" height="7" viewBox="0 0 8 8"><circle cx="4" cy="4" r="4" fill="#6B9C82"/></svg>
                    Elegível
                  </span>
                </div>
                <div class="card-stats">
                  <div class="stat">
                    <div class="stat-label">Lançamentos</div>
                    <div class="stat-value">{{ period.itemCount }}</div>
                  </div>
                  <div class="stat">
                    <div class="stat-label">Despesas</div>
                    <div class="stat-value stat-danger">{{ period.totalExpense | currencyBrl }}</div>
                  </div>
                  <div class="stat">
                    <div class="stat-label">Receitas</div>
                    <div class="stat-value stat-green">{{ period.totalIncome | currencyBrl }}</div>
                  </div>
                </div>
                <div class="card-divider"></div>
                <div class="card-footer">
                  <div></div>
                  <div class="card-actions">
                    <button class="btn-csv-card" (click)="downloadCsv(period)">
                      <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
                      CSV
                    </button>
                    <button class="btn-expurgar" (click)="openConfirmModal(period)">Expurgar</button>
                  </div>
                </div>
              </div>
            }
          </div>
        } @else {
          <div class="empty-state">
            <div class="empty-icon">
              <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#6B9C82" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
            </div>
            <div class="empty-title">Nenhum período pendente</div>
            <div class="empty-text">Todos os períodos elegíveis foram expurgados com sucesso.</div>
          </div>
        }
      </section>

      @if (purgeResult()) {
        <div class="purge-result">
          Expurgo concluído. Espaço estimado liberado: {{ purgeResult()!.estimatedSpaceKb }} KB
        </div>
      }

      <!-- Modal de confirmação de expurgo -->
      @if (confirmModalOpen()) {
        <div class="modal-overlay" @backdropAnim (click)="closeConfirmModal()"></div>
        <div class="modal modal-purge-confirm" @modalAnim>
          <!-- pixel-art frame -->
          <div class="sonic-frame">
            <div class="sf-top"></div>
            <div class="sf-bottom"></div>
            <div class="sf-left"></div>
            <div class="sf-right"></div>
            <div class="sf-corner sf-tl"></div>
            <div class="sf-corner sf-tr"></div>
            <div class="sf-corner sf-bl"></div>
            <div class="sf-corner sf-br"></div>
          </div>

          <!-- header -->
          <div class="modal-header">
            <div>
              <h2 class="modal-title">Confirmar Expurgo</h2>
              @if (selectedPeriod()) {
                <div class="modal-subtitle">
                  <span>{{ monthName(selectedPeriod()!.month) }} / {{ selectedPeriod()!.year }}</span>
                  <span class="dot"></span>
                  <span>{{ selectedPeriod()!.itemCount }} lançamentos</span>
                </div>
              }
            </div>
            <button class="btn-close-modal" (click)="closeConfirmModal()">
              <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
                <line x1="2" y1="2" x2="10" y2="10" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
                <line x1="10" y1="2" x2="2" y2="10" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
              </svg>
            </button>
          </div>

          <!-- body -->
          <div class="modal-body">
            <!-- danger callout -->
            <div class="danger-callout">
              <svg class="ico-warning" width="17" height="17" viewBox="0 0 24 24" fill="none">
                <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" stroke="#C4674A" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                <line x1="12" y1="9" x2="12" y2="13" stroke="#C4674A" stroke-width="2" stroke-linecap="round"/>
                <line x1="12" y1="17" x2="12.01" y2="17" stroke="#C4674A" stroke-width="2.5" stroke-linecap="round"/>
              </svg>
              <p>Esta ação irá <strong class="text-danger">excluir permanentemente</strong> todos os dados de <strong>{{ monthName(selectedPeriod()!.month) }}/{{ selectedPeriod()!.year }}</strong> do banco de dados. Esta operação <strong>não pode ser desfeita</strong>.</p>
            </div>

            <!-- stats grid -->
            <div class="modal-stats-grid">
              <div class="modal-stat">
                <div class="modal-stat-label">Lançamentos</div>
                <div class="modal-stat-value">{{ selectedPeriod()!.itemCount }}</div>
              </div>
              <div class="modal-stat">
                <div class="modal-stat-label">Total Despesas</div>
                <div class="modal-stat-value stat-danger">{{ selectedPeriod()!.totalExpense | currencyBrl }}</div>
              </div>
            </div>

            <!-- checkbox -->
            <label class="confirm-checkbox-label" [class.checked]="purgeConfirmed()">
              <input type="checkbox" [checked]="purgeConfirmed()" (change)="purgeConfirmed.set(!purgeConfirmed())" class="confirm-checkbox" />
              <span class="modal-confirm-text">Confirmo que o arquivo CSV foi salvo com sucesso. Desejo excluir permanentemente os dados de {{ monthName(selectedPeriod()!.month) }}/{{ selectedPeriod()!.year }} do banco de dados.</span>
            </label>
          </div>

          <!-- footer -->
          <div class="modal-footer">
            <button class="btn-csv-modal" (click)="downloadCsv(selectedPeriod()!)">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
              Baixar CSV
            </button>
            <div class="modal-footer-actions">
              <button class="btn-cancel" (click)="closeConfirmModal()">Cancelar</button>
              <button class="btn-danger" [disabled]="!purgeConfirmed()" (click)="confirmPurge()">Confirmar Expurgo</button>
            </div>
          </div>
        </div>
      }

      <!-- Seção de registros de expurgo -->
      <section class="records-section">
        <div class="section-header">
          <h2 class="section-title">Histórico de expurgos</h2>
        </div>

        @if (purgeRecords().length === 0) {
          <div class="empty-state">Nenhum registro de expurgo encontrado.</div>
        } @else {
          <div class="table-wrap">
            <table class="table">
              <thead>
                <tr>
                  <th>Período</th>
                  <th>Lançamentos</th>
                  <th>Receitas</th>
                  <th>Despesas</th>
                  <th>Data do expurgo</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (record of purgeRecords(); track record.id; let last = $last) {
                  <tr [class.last]="last">
                    <td class="td-period">{{ monthName(record.month) }}/{{ record.year }}</td>
                    <td class="td-muted">{{ record.itemCount }}</td>
                    <td class="td-muted stat-green">{{ record.totalIncome | currencyBrl }}</td>
                    <td class="td-muted stat-danger">{{ record.totalExpense | currencyBrl }}</td>
                    <td class="td-muted td-num">{{ record.purgedAt | date:'dd/MM/yyyy' }}</td>
                    <td>
                      <span class="badge-expurgado">
                        <svg width="10" height="10" viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><polyline points="2,7 5.5,11 12,3"/></svg>
                        Expurgado
                      </span>
                    </td>
                    <td>
                      <button class="btn-delete-record" (click)="openDeleteRecordModal(record)" title="Excluir metadados">
                        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/><path d="M9 6V4h6v2"/></svg>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>

      <!-- Modal de delete de metadados -->
      @if (deleteRecordModalOpen()) {
        <div class="modal-overlay" @backdropAnim (click)="closeDeleteRecordModal()"></div>
        <div class="modal modal-delete-record" @modalAnim>
          <!-- pixel-art frame -->
          <div class="sonic-frame">
            <div class="sf-top"></div><div class="sf-bottom"></div>
            <div class="sf-left"></div><div class="sf-right"></div>
            <div class="sf-corner sf-tl"></div><div class="sf-corner sf-tr"></div>
            <div class="sf-corner sf-bl"></div><div class="sf-corner sf-br"></div>
          </div>
          <div class="modal-header">
            <h2 class="modal-title">Excluir Metadados do Expurgo</h2>
            <button class="btn-close-modal" (click)="closeDeleteRecordModal()">
              <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
                <line x1="2" y1="2" x2="10" y2="10" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
                <line x1="10" y1="2" x2="2" y2="10" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
              </svg>
            </button>
          </div>
          <div class="modal-body">
            <div class="danger-callout">
              <svg class="ico-warning" width="17" height="17" viewBox="0 0 24 24" fill="none">
                <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" stroke="#C4674A" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                <line x1="12" y1="9" x2="12" y2="13" stroke="#C4674A" stroke-width="2" stroke-linecap="round"/>
                <line x1="12" y1="17" x2="12.01" y2="17" stroke="#C4674A" stroke-width="2.5" stroke-linecap="round"/>
              </svg>
              <p>Os dados deste período já foram excluídos do banco de dados. Esta ação remove apenas o registro de metadados do expurgo.</p>
            </div>
          </div>
          <div class="modal-footer">
            <div></div>
            <div class="modal-footer-actions">
              <button class="btn-cancel" (click)="closeDeleteRecordModal()">Cancelar</button>
              <button class="btn-danger" (click)="confirmDeleteRecord()">Confirmar exclusão</button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class PurgeComponent implements OnInit {
  private readonly api    = inject(ApiService);
  private readonly router = inject(Router);

  readonly eligiblePeriods       = signal<EligiblePeriodResponse[]>([]);
  readonly selectedPeriod        = signal<EligiblePeriodResponse | null>(null);
  readonly confirmModalOpen      = signal(false);
  readonly purgeConfirmed        = signal(false);
  readonly purgeResult           = signal<PurgeResultResponse | null>(null);
  readonly apiError              = signal<string | null>(null);
  readonly purgeRecords          = signal<PurgeRecordResponse[]>([]);
  readonly deleteRecordModalOpen = signal(false);
  readonly selectedRecord        = signal<PurgeRecordResponse | null>(null);

  navigateToAnalysis(): void {
    this.router.navigate(['purge', 'analysis']);
  }

  // Converte número de mês (1-12) para nome em PT-BR
  monthName(month: number): string {
    return MONTH_NAMES[month - 1] ?? '';
  }

  ngOnInit(): void {
    this.api.getEligiblePeriods().subscribe({
      next:  periods => this.eligiblePeriods.set(periods),
      error: err     => this.apiError.set(err?.error?.message ?? 'Erro ao carregar períodos.'),
    });

    this.api.getPurgeRecords().subscribe({
      next:  records => this.purgeRecords.set(records),
      error: err     => this.apiError.set(err?.error?.message ?? 'Erro ao carregar registros.'),
    });
  }

  downloadCsv(period: EligiblePeriodResponse): void {
    this.api.exportPurgeCsv(period.periodId).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.setAttribute('download', `expurgo-${period.periodId}-${period.year}_${period.month}.csv`);
        a.click();
        URL.revokeObjectURL(url);
        this.purgeConfirmed.set(true);
      },
      error: () => {},
    });
  }

  openConfirmModal(period: EligiblePeriodResponse): void {
    this.purgeConfirmed.set(false);
    this.selectedPeriod.set(period);
    this.confirmModalOpen.set(true);
  }

  closeConfirmModal(): void {
    this.confirmModalOpen.set(false);
  }

  confirmPurge(): void {
    const period = this.selectedPeriod();
    if (!period) return;

    const csvFileName = `expurgo-${period.periodId}-${period.year}_${period.month}.csv`;
    this.api.executePurge(period.periodId, csvFileName).subscribe({
      next: result => {
        // Remove o período expurgado da lista e recarrega os registros de expurgo
        this.eligiblePeriods.update(list => list.filter(p => p.periodId !== period.periodId));
        this.purgeResult.set(result);
        this.confirmModalOpen.set(false);
        this.api.getPurgeRecords().subscribe({
          next:  records => this.purgeRecords.set(records),
          error: err     => this.apiError.set(err?.error?.message ?? 'Erro ao recarregar registros.'),
        });
      },
      error: err => {
        this.apiError.set(err?.error?.message ?? 'Erro ao executar expurgo.');
        this.confirmModalOpen.set(false);
      },
    });
  }

  openDeleteRecordModal(record: PurgeRecordResponse): void {
    this.selectedRecord.set(record);
    this.deleteRecordModalOpen.set(true);
  }

  closeDeleteRecordModal(): void {
    this.deleteRecordModalOpen.set(false);
  }

  confirmDeleteRecord(): void {
    const record = this.selectedRecord();
    if (!record) return;

    this.api.deletePurgeRecord(record.id).subscribe({
      next: () => {
        // Remove o record da lista após delete bem-sucedido
        this.purgeRecords.update(list => list.filter(r => r.id !== record.id));
        this.deleteRecordModalOpen.set(false);
      },
      error: err => {
        this.apiError.set(err?.error?.message ?? 'Erro ao excluir registro.');
        this.deleteRecordModalOpen.set(false);
      },
    });
  }
}
