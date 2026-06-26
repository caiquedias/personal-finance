import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { CsvReaderService } from '../../core/services/csv-reader.service';

@Component({
  selector: 'app-purge-analysis',
  standalone: true,
  template: `
    <div class="purge-analysis-container">
      <h2>Análise Offline de CSV</h2>
      <p>Selecione o arquivo CSV exportado de um período expurgado para visualizar os dados offline.</p>

      <label class="file-upload-label" for="csv-upload">
        Selecionar arquivo CSV
        <input
          id="csv-upload"
          type="file"
          accept=".csv"
          (change)="onFileSelected($event)"
          style="display:none"
        />
      </label>
    </div>
  `,
  styles: [`
    .purge-analysis-container {
      padding: 2rem;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1.5rem;
    }

    .file-upload-label {
      display: inline-block;
      padding: 0.75rem 1.5rem;
      background: var(--color-primary, #6366f1);
      color: #fff;
      border-radius: 0.5rem;
      cursor: pointer;
      font-weight: 600;
      transition: opacity 0.2s;
    }

    .file-upload-label:hover {
      opacity: 0.85;
    }
  `],
})
export class PurgeAnalysisComponent {
  private readonly csvReaderService = inject(CsvReaderService);
  private readonly router           = inject(Router);

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;

    const file   = input.files[0];
    const reader = new FileReader();

    reader.onload = () => {
      const content = reader.result as string;
      this.csvReaderService.parseCsv(content);
      this.router.navigate(['purge', 'analysis', 'detail']);
    };

    reader.readAsText(file, 'utf-8');
  }
}
