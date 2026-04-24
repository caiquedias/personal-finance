import { Component, inject, signal } from '@angular/core';
import { HttpClient, HttpEventType } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { HeaderComponent } from '../../../../shared/components/header/header.component';
import { environment } from '../../../../../environments/environment';

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
  templateUrl: './import.component.html',
  styleUrls: ['./import.component.css'],
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
