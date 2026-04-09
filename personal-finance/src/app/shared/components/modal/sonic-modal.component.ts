import {
  Component, input, output, OnInit, OnDestroy,
  signal, HostListener
} from '@angular/core';

@Component({
  selector: 'app-sonic-modal',
  standalone: true,

  template: `
    <div class="sonic-modal-wrap">

      <!-- Borda superior com blocos Green Hill Zone -->
      <div class="sonic-border sonic-border--top">
        <div class="sonic-blocks">
          @for (b of blocks; track $index) {
            <div class="sonic-block" [class.sonic-block--dark]="b === 'd'"></div>
          }
        </div>
      </div>

      <!-- Conteúdo -->
      <div class="sonic-modal-body">
        <!-- Header -->
        <div class="sonic-modal-header">
          <h2 class="sonic-modal-title">{{ title() }}</h2>
          <button
            class="sonic-modal-close"
            (click)="closed.emit()"
            aria-label="Fechar"
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none"
                 stroke="currentColor" stroke-width="2.5">
              <line x1="18" y1="6" x2="6" y2="18"/>
              <line x1="6" y1="6" x2="18" y2="18"/>
            </svg>
          </button>
        </div>

        <!-- Slot de conteúdo -->
        <div class="sonic-modal-content">
          <ng-content />
        </div>
      </div>

      <!-- Borda inferior com blocos Green Hill Zone -->
      <div class="sonic-border sonic-border--bottom">
        <div class="sonic-blocks">
          @for (b of blocks; track $index) {
            <div class="sonic-block" [class.sonic-block--dark]="b === 'd'"></div>
          }
        </div>
      </div>

    </div>
  `,
  styles: [`
    /* ── Padrão de blocos Green Hill Zone ── */
    /*
      Cada bloco simula o tijolo marrom/laranja do Sonic 1 - GHZ.
      Padrão: tijolo claro com borda escura e rejunte branco/creme.
      Alterna blocos claros e escuros para dar profundidade.
    */

    :host { display: block; }

    .sonic-modal-wrap {
      background: var(--surface-raised);
      border-radius: 0;
      box-shadow: var(--shadow-modal),
                  4px 4px 0 #1a0e00,
                  -1px -1px 0 #1a0e00;
      min-width: 480px;
      max-width: 600px;
      width: 100%;
      overflow: hidden;
      position: relative;
    }

    /* ── Bordas com blocos ── */
    .sonic-border {
      height: 28px;
      overflow: hidden;
      background: #1a0e00;
    }

    .sonic-border--top    { border-bottom: 2px solid #1a0e00; }
    .sonic-border--bottom { border-top: 2px solid #1a0e00; }

    .sonic-blocks {
      display: flex;
      height: 100%;
      gap: 2px;
      padding: 2px;
    }

    .sonic-block {
      /* Tijolo claro — tom marrom-laranja da GHZ */
      flex: 1;
      height: 100%;
      background: linear-gradient(
        135deg,
        #c8722a 0%,
        #d4892f 30%,
        #b85e20 70%,
        #a04e18 100%
      );
      border: 1px solid #1a0e00;
      border-radius: 1px;
      position: relative;
      box-shadow: inset 1px 1px 0 rgba(255,200,120,0.35),
                  inset -1px -1px 0 rgba(0,0,0,0.4);
    }

    /* Tijolo escuro alternado */
    .sonic-block--dark {
      background: linear-gradient(
        135deg,
        #7a3a0e 0%,
        #8c4510 30%,
        #6a2e08 70%,
        #5a2406 100%
      );
      box-shadow: inset 1px 1px 0 rgba(180,100,50,0.2),
                  inset -1px -1px 0 rgba(0,0,0,0.6);
    }

    /* Rejunte horizontal simulado com pseudo-elemento */
    .sonic-block::after {
      content: '';
      position: absolute;
      bottom: 3px;
      left: 8%;
      right: 8%;
      height: 1px;
      background: rgba(255, 220, 160, 0.2);
    }

    /* ── Corpo do modal ── */
    .sonic-modal-body {
      padding: 0;
    }

    .sonic-modal-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 16px 24px 12px;
      border-bottom: 1px solid var(--border);
    }

    .sonic-modal-title {
      font-size: 1.0625rem;
      font-weight: 600;
      color: var(--ink);
      margin: 0;
    }

    .sonic-modal-close {
      width: 32px;
      height: 32px;
      display: flex;
      align-items: center;
      justify-content: center;
      border: 1px solid var(--border);
      background: var(--surface-raised);
      border-radius: var(--radius);
      cursor: pointer;
      color: var(--ink3);
      transition: all var(--transition);
      flex-shrink: 0;
    }

    .sonic-modal-close:hover {
      background: var(--color-danger-bg);
      border-color: var(--rust);
      color: var(--rust);
    }

    .sonic-modal-content {
      padding: 20px 24px 24px;
    }

    @media (max-width: 560px) {
      .sonic-modal-wrap { min-width: 0; width: calc(100vw - 32px); }
    }
  `]
})
export class SonicModalComponent {
  readonly title  = input<string>('');
  readonly closed = output<void>();

  // Padrão de blocos — 'l' = light, 'd' = dark
  // Simula o padrão xadrez de tijolos da Green Hill Zone
  readonly blocks: ('l' | 'd')[] = [
    'l','d','l','l','d','l','l','d','l','l','d','l',
    'l','d','l','l','d','l','l','d','l','l','d','l',
    'l','d','l','l','d','l',
  ];

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}
