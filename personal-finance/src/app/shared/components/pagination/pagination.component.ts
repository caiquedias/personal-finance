import { Component, input, output, computed } from '@angular/core';

@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [],
  template: `
    <div class="pagination-bar">
      <span class="pagination-info">
        {{ rangeStart() }}–{{ rangeEnd() }} de {{ totalCount() }} registros
      </span>

      <div class="pagination-controls">
        <select class="page-size-select" [value]="pageSize()" (change)="onPageSizeChange($event)">
          <option value="10">10 / pág.</option>
          <option value="20">20 / pág.</option>
          <option value="50">50 / pág.</option>
        </select>

        <button class="page-btn" [disabled]="currentPage() <= 1" (click)="goTo(currentPage() - 1)">
          ‹
        </button>

        @for (p of visiblePages(); track p) {
          @if (p === -1) {
            <span class="page-ellipsis">…</span>
          } @else {
            <button
              class="page-btn"
              [class.active]="p === currentPage()"
              (click)="goTo(p)"
            >{{ p }}</button>
          }
        }

        <button class="page-btn" [disabled]="currentPage() >= totalPages()" (click)="goTo(currentPage() + 1)">
          ›
        </button>
      </div>
    </div>
  `,
  styles: [`
    .pagination-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 12px 16px;
      border-top: 1px solid var(--border);
      font-size: 0.8125rem;
      color: var(--ink3);
    }

    .pagination-controls {
      display: flex;
      align-items: center;
      gap: 4px;
    }

    .page-size-select {
      height: 28px;
      padding: 0 8px;
      border: 1px solid var(--border);
      border-radius: var(--radius-sm);
      background: var(--surface);
      color: var(--ink2);
      font-size: 0.8125rem;
      cursor: pointer;
      margin-right: 8px;
    }

    .page-btn {
      min-width: 28px;
      height: 28px;
      padding: 0 6px;
      border: 1px solid var(--border);
      border-radius: var(--radius-sm);
      background: none;
      color: var(--ink2);
      font-size: 0.8125rem;
      cursor: pointer;
      transition: all var(--transition);
    }

    .page-btn:hover:not(:disabled) {
      background: var(--surface-overlay);
      border-color: var(--sage2);
      color: var(--sage2);
    }

    .page-btn.active {
      background: var(--sage2);
      border-color: var(--sage2);
      color: #fff;
      font-weight: 600;
    }

    .page-btn:disabled {
      opacity: 0.35;
      cursor: not-allowed;
    }

    .page-ellipsis {
      padding: 0 4px;
      color: var(--ink3);
      line-height: 28px;
    }
  `]
})
export class PaginationComponent {
  readonly totalCount  = input.required<number>();
  readonly pageSize    = input.required<number>();
  readonly currentPage = input.required<number>();

  readonly pageChange     = output<number>();
  readonly pageSizeChange = output<number>();

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));

  readonly rangeStart = computed(() =>
    this.totalCount() === 0 ? 0 : (this.currentPage() - 1) * this.pageSize() + 1);

  readonly rangeEnd = computed(() =>
    Math.min(this.currentPage() * this.pageSize(), this.totalCount()));

  readonly visiblePages = computed<number[]>(() => {
    const total = this.totalPages();
    const current = this.currentPage();
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);

    const pages: number[] = [1];
    if (current > 3) pages.push(-1);

    const start = Math.max(2, current - 1);
    const end   = Math.min(total - 1, current + 1);
    for (let i = start; i <= end; i++) pages.push(i);

    if (current < total - 2) pages.push(-1);
    pages.push(total);
    return pages;
  });

  goTo(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.pageChange.emit(page);
  }

  onPageSizeChange(event: Event): void {
    const size = Number((event.target as HTMLSelectElement).value);
    this.pageSizeChange.emit(size);
  }
}
