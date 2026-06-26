import { Component, inject, signal, OnInit } from '@angular/core';
import { ApiService } from '../../../../core/services/api.service';
import { EligiblePeriodResponse, PurgeResultResponse } from '../../../../core/models/models';

// Stub — implementação pendente (fase RED do TDD)
@Component({
  selector: 'app-purge',
  standalone: true,
  imports: [],
  template: `<div>purge</div>`,
})
export class PurgeComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly eligiblePeriods  = signal<EligiblePeriodResponse[]>([]);
  readonly selectedPeriod   = signal<EligiblePeriodResponse | null>(null);
  readonly confirmModalOpen = signal(false);
  readonly csvReady         = signal(false);
  readonly purgeResult      = signal<PurgeResultResponse | null>(null);
  readonly apiError         = signal<string | null>(null);

  ngOnInit(): void { /* stub */ }

  downloadCsv(_period: EligiblePeriodResponse): void { /* stub */ }

  openConfirmModal(_period: EligiblePeriodResponse): void { /* stub */ }

  closeConfirmModal(): void { /* stub */ }

  confirmPurge(): void { /* stub */ }
}
