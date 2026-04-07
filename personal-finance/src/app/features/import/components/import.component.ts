import { Component, inject, signal } from '@angular/core';
import { HttpClient, HttpEventType } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { HeaderComponent } from '../../../shared/components/header/header.component';
import { environment } from '../../../../environments/environment';

interface ImportResult {
  periodsCreated:    number;
  periodsSkipped:    number;
  categoriesCreated: number;
  expensesImported:  number;
  incomesImported:   number;
  expensesSkipped:   number;
  incomesSkipped:    number;
  warnings:          string[];
}

type ImportState = 'idle' | 'uploading' | 'processing' | 'done' | 'error';

@Component({
  selector: 'app-import',
  standalone: true,
  imports: [HeaderComponent, RouterLink],
  template: `
    <app-header
      title="Importar Dados"
      subtitle="Importe o histórico financeiro da planilha legada (.xlsx)"
    >
      <a routerLink="/" class="btn btn-secondary btn-sm">← Dashboard</a>
    </app-header>

    <div class="page-content">

      <!-- Drop zone -->
      @if (state() === 'idle' || state() === 'error') {
        <div
          class="drop-zone card"
          [class.drag-over]="isDragging()"
          [class.has-file]="selectedFile()"
          (dragover)="onDragOver($event)"
          (dragleave)="isDragging.set(false)"
          (drop)="onDrop($event)"
        >
          <div class="drop-zone-inner">
            <div class="drop-icon">
              <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                <polyline points="14 2 14 8 20 8"/>
                <line x1="12" y1="18" x2="12" y2="12"/>
                <polyline points="9 15 12 12 15 15"/>
              </svg>
            </div>

            @if (selectedFile()) {
              <p class="drop-filename">{{ selectedFile()!.name }}</p>
              <p class="drop-filesize">{{ formatSize(selectedFile()!.size) }}</p>
            } @else {
              <p class="drop-title">Arraste o arquivo aqui</p>
              <p class="drop-subtitle">ou clique para selecionar</p>
              <p class="drop-hint">Apenas arquivos .xlsx — máximo 10 MB</p>
            }

            <input
              #fileInput
              type="file"
              accept=".xlsx"
              class="file-input"
              (change)="onFileSelected($event)"
            />

            <div class="drop-actions">
              @if (selectedFile()) {
                <button class="btn btn-primary" (click)="startImport()">
                  Importar agora
                </button>
                <button class="btn btn-ghost btn-sm" (click)="clearFile(fileInput)">
                  Trocar arquivo
                </button>
              } @else {
                <button class="btn btn-secondary" (click)="fileInput.click()">
                  Selecionar arquivo
                </button>
              }
            </div>
          </div>
        </div>

        @if (state() === 'error') {
          <div class="error-banner">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"/>
              <line x1="12" y1="8" x2="12" y2="12"/>
              <line x1="12" y1="16" x2="12.01" y2="16"/>
            </svg>
            {{ errorMessage() }}
          </div>
        }
      }

      <!-- Progresso -->
      @if (state() === 'uploading' || state() === 'processing') {
        <div class="progress-card card">
          <div class="progress-header">
            <span class="progress-title">
              {{ state() === 'uploading' ? 'Enviando arquivo...' : 'Processando planilha...' }}
            </span>
            <span class="progress-pct">{{ uploadProgress() }}%</span>
          </div>
          <div class="progress-track">
            <div class="progress-fill" [style.width.%]="uploadProgress()"></div>
          </div>
          <p class="progress-hint text-muted">
            {{ state() === 'uploading'
              ? 'Aguarde enquanto o arquivo é enviado para o servidor.'
              : 'O servidor está lendo as abas e importando os registros...' }}
          </p>
        </div>
      }

      <!-- Resultado -->
      @if (state() === 'done' && result()) {
        <div class="result-section">
          <!-- Banner de sucesso -->
          <div class="success-banner">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/>
              <polyline points="22 4 12 14.01 9 11.01"/>
            </svg>
            Importação concluída com sucesso!
          </div>

          <!-- Grid de métricas -->
          <div class="metrics-grid">
            <div class="metric-card card">
              <span class="metric-label">Períodos criados</span>
              <span class="metric-value success">{{ result()!.periodsCreated }}</span>
              @if (result()!.periodsSkipped > 0) {
                <span class="metric-sub">{{ result()!.periodsSkipped }} já existiam</span>
              }
            </div>
            <div class="metric-card card">
              <span class="metric-label">Despesas importadas</span>
              <span class="metric-value">{{ result()!.expensesImported }}</span>
              @if (result()!.expensesSkipped > 0) {
                <span class="metric-sub">{{ result()!.expensesSkipped }} ignoradas</span>
              }
            </div>
            <div class="metric-card card">
              <span class="metric-label">Receitas importadas</span>
              <span class="metric-value success">{{ result()!.incomesImported }}</span>
              @if (result()!.incomesSkipped > 0) {
                <span class="metric-sub">{{ result()!.incomesSkipped }} ignoradas</span>
              }
            </div>
            <div class="metric-card card">
              <span class="metric-label">Categorias criadas</span>
              <span class="metric-value info">{{ result()!.categoriesCreated }}</span>
              <span class="metric-sub">globais</span>
            </div>
          </div>

          <!-- Avisos -->
          @if (result()!.warnings.length > 0) {
            <div class="warnings-section card">
              <div class="warnings-header">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/>
                  <line x1="12" y1="9" x2="12" y2="13"/>
                  <line x1="12" y1="17" x2="12.01" y2="17"/>
                </svg>
                {{ result()!.warnings.length }} aviso(s)
              </div>
              <ul class="warnings-list">
                @for (w of result()!.warnings; track $index) {
                  <li>{{ w }}</li>
                }
              </ul>
            </div>
          }

          <!-- Ações pós importação -->
          <div class="post-actions">
            <a routerLink="/" class="btn btn-primary">Ver dashboard</a>
            <a routerLink="/periods" class="btn btn-secondary">Ver períodos</a>
            <button class="btn btn-ghost btn-sm" (click)="resetState()">
              Importar outro arquivo
            </button>
          </div>
        </div>
      }

      <!-- Instruções -->
      @if (state() === 'idle') {
        <div class="instructions card">
          <h3 class="instructions-title">Como funciona</h3>
          <ol class="instructions-list">
            <li>Selecione ou arraste sua planilha <strong>.xlsx</strong> no formato legado</li>
            <li>O sistema lê cada aba como um <strong>período mensal</strong></li>
            <li>Despesas planejadas, gastos diversos e receitas são importados automaticamente</li>
            <li>Períodos já existentes são <strong>ignorados</strong> — a operação é segura</li>
            <li>Ao final você verá um sumário completo com avisos de inconsistências</li>
          </ol>

          <div class="instructions-note">
            <strong>Atenção:</strong> despesas com quinzena não definida (valor 0) são ignoradas —
            elas indicam pagamentos realizados no mês anterior.
          </div>
        </div>
      }

    </div>
  `,
  styles: [`
    .page-content {
      padding: 28px;
      display: flex;
      flex-direction: column;
      gap: 24px;
      max-width: 760px;
    }

    /* ── Drop zone ── */
    .drop-zone {
      border: 2px dashed var(--border);
      border-radius: var(--radius-xl);
      padding: 48px 24px;
      text-align: center;
      transition: all var(--transition);
      cursor: pointer;
      background: var(--surface-raised);
    }

    .drop-zone:hover,
    .drop-zone.drag-over {
      border-color: var(--sage2);
      background: rgba(107, 143, 113, 0.04);
    }

    .drop-zone.has-file {
      border-color: var(--sage2);
      border-style: solid;
    }

    .drop-zone-inner {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 10px;
    }

    .drop-icon { color: var(--ink3); margin-bottom: 4px; }
    .drop-zone.has-file .drop-icon  { color: var(--sage2); }
    .drop-zone.drag-over .drop-icon { color: var(--sage); }

    .drop-title    { font-size: 1rem; font-weight: 600; color: var(--ink); margin: 0; }
    .drop-subtitle { font-size: 0.875rem; color: var(--ink3); margin: 0; }
    .drop-hint     { font-size: 0.8125rem; color: var(--ink4); margin: 0; }
    .drop-filename { font-size: 0.9375rem; font-weight: 600; color: var(--ink); margin: 0; }
    .drop-filesize { font-size: 0.8125rem; color: var(--ink3); margin: 0; }

    .file-input { display: none; }

    .drop-actions {
      display: flex;
      gap: 10px;
      align-items: center;
      margin-top: 8px;
    }

    /* ── Banners ── */
    .error-banner {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 12px 16px;
      background: var(--color-danger-bg);
      color: var(--color-danger);
      border-radius: var(--radius);
      font-size: 0.875rem;
    }

    .success-banner {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 14px 18px;
      background: var(--color-success-bg);
      color: var(--sage2);
      border-radius: var(--radius);
      font-size: 0.9375rem;
      font-weight: 600;
    }

    /* ── Progresso ── */
    .progress-card { padding: 24px; }

    .progress-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 12px;
    }

    .progress-title { font-weight: 600; color: var(--ink); font-size: 0.9375rem; }
    .progress-pct   { font-weight: 700; color: var(--sage2); font-size: 1rem; }

    .progress-track {
      height: 8px;
      background: var(--bg3);
      border-radius: 4px;
      overflow: hidden;
      margin-bottom: 12px;
    }

    .progress-fill {
      height: 100%;
      background: linear-gradient(90deg, var(--sage2), var(--sage));
      border-radius: 4px;
      transition: width 0.3s ease;
    }

    .progress-hint { font-size: 0.8125rem; margin: 0; }

    /* ── Resultado ── */
    .result-section { display: flex; flex-direction: column; gap: 20px; }

    .metrics-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 14px;
    }

    @media (max-width: 760px) { .metrics-grid { grid-template-columns: repeat(2, 1fr); } }

    .metric-card {
      padding: 18px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .metric-label { font-size: 0.75rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; color: var(--ink3); }
    .metric-value { font-size: 1.75rem; font-weight: 700; color: var(--ink); line-height: 1.2; }
    .metric-value.success { color: var(--sage2); }
    .metric-value.info    { color: var(--color-info); }
    .metric-sub   { font-size: 0.75rem; color: var(--ink4); }

    /* ── Avisos ── */
    .warnings-section { padding: 20px; }

    .warnings-header {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 0.875rem;
      font-weight: 600;
      color: var(--color-warning);
      margin-bottom: 12px;
    }

    .warnings-list {
      margin: 0;
      padding-left: 20px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .warnings-list li { font-size: 0.8125rem; color: var(--ink2); }

    /* ── Ações pós importação ── */
    .post-actions { display: flex; gap: 10px; flex-wrap: wrap; }

    /* ── Instruções ── */
    .instructions { padding: 24px; }
    .instructions-title { font-size: 0.9375rem; margin-bottom: 16px; }

    .instructions-list {
      margin: 0;
      padding-left: 20px;
      display: flex;
      flex-direction: column;
      gap: 8px;
      color: var(--ink2);
      font-size: 0.875rem;
    }

    .instructions-list li { line-height: 1.5; }

    .instructions-note {
      margin-top: 16px;
      padding: 10px 14px;
      background: var(--color-warning-bg);
      color: var(--ink2);
      border-radius: var(--radius);
      font-size: 0.8125rem;
      border-left: 3px solid var(--color-warning);
    }
  `]
})
export class ImportComponent {
  private readonly http = inject(HttpClient);

  readonly state          = signal<ImportState>('idle');
  readonly selectedFile   = signal<File | null>(null);
  readonly isDragging     = signal(false);
  readonly uploadProgress = signal(0);
  readonly result         = signal<ImportResult | null>(null);
  readonly errorMessage   = signal<string | null>(null);

  // ── Drag & Drop ───────────────────────────────────────────────────────────

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(true);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) this.setFile(file);
  }

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) this.setFile(file);
  }

  private setFile(file: File): void {
    if (!file.name.endsWith('.xlsx')) {
      this.state.set('error');
      this.errorMessage.set('Apenas arquivos .xlsx são aceitos.');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      this.state.set('error');
      this.errorMessage.set('O arquivo excede o limite de 10 MB.');
      return;
    }
    this.selectedFile.set(file);
    this.state.set('idle');
    this.errorMessage.set(null);
  }

  clearFile(input: HTMLInputElement): void {
    this.selectedFile.set(null);
    input.value = '';
  }

  // ── Upload ────────────────────────────────────────────────────────────────

  startImport(): void {
    const file = this.selectedFile();
    if (!file) return;

    const formData = new FormData();
    formData.append('file', file);

    this.state.set('uploading');
    this.uploadProgress.set(0);

    this.http.post<ImportResult>(
      `${environment.apiUrl}/import/legacy`,
      formData,
      { reportProgress: true, observe: 'events' }
    ).subscribe({
      next: (event) => {
        if (event.type === HttpEventType.UploadProgress && event.total) {
          const pct = Math.round(95 * event.loaded / event.total);
          this.uploadProgress.set(pct);
          if (pct >= 95) this.state.set('processing');
        }
        if (event.type === HttpEventType.Response) {
          this.uploadProgress.set(100);
          this.result.set(event.body!);
          this.state.set('done');
        }
      },
      error: (err) => {
        this.state.set('error');
        this.errorMessage.set(
          err.error?.message ?? 'Erro ao processar o arquivo. Verifique se é uma planilha válida.');
      }
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  resetState(): void {
    this.state.set('idle');
    this.selectedFile.set(null);
    this.result.set(null);
    this.errorMessage.set(null);
    this.uploadProgress.set(0);
  }

  formatSize(bytes: number): string {
    if (bytes < 1024)       return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
