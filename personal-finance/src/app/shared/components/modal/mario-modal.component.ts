import { Component, computed, input, OnDestroy, OnInit, output, signal } from '@angular/core';

@Component({
  selector: 'app-mario-modal',
  standalone: true,
  template: `
    <div class="mario-box" role="dialog" aria-modal="true">
      @if (title()) {
        <div class="mario-title">{{ title() }}</div>
      }
      <div class="mario-content">{{ displayedText() }}</div>
      <div class="mario-footer">
        @if (!animDone()) {
          <button class="skip-btn" (click)="skipAnimation()" title="Pular animação">▼</button>
        }
        @if (animDone()) {
          <button class="ok-btn" (click)="closeModal()">OK</button>
        }
      </div>
    </div>
  `,
  styles: [`
    :host {
      display: block;
    }

    .mario-box {
      position: relative;
      background: #000;
      color: #fff;
      font-family: 'Press Start 2P', monospace;
      font-size: 0.6rem;
      line-height: 1.8;
      padding: 28px 28px 64px;
      min-width: 480px;
      max-width: 660px;
      width: 90vw;
      border: 4px solid #fff;
      box-shadow:
        0 0 0 2px #000,
        0 0 0 6px #fff,
        0 0 0 8px #000;
      border-radius: 2px;
    }

    .mario-title {
      font-size: 0.65rem;
      color: #facc15;
      margin-bottom: 20px;
      letter-spacing: 0.05em;
      border-bottom: 2px solid #333;
      padding-bottom: 12px;
    }

    .mario-content {
      white-space: pre-wrap;
      min-height: 80px;
      word-break: break-word;
    }

    .mario-footer {
      position: absolute;
      bottom: 16px;
      right: 20px;
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .skip-btn {
      background: transparent;
      border: none;
      color: #fff;
      font-family: 'Press Start 2P', monospace;
      font-size: 0.9rem;
      cursor: pointer;
      padding: 4px;
      line-height: 1;
      animation: mario-bounce 0.55s ease-in-out infinite alternate;
    }

    .skip-btn:hover {
      color: #facc15;
    }

    @keyframes mario-bounce {
      from { transform: translateY(0); }
      to   { transform: translateY(-7px); }
    }

    .ok-btn {
      background: #000;
      border: 2px solid #fff;
      color: #fff;
      font-family: 'Press Start 2P', monospace;
      font-size: 0.6rem;
      padding: 8px 20px;
      cursor: pointer;
      letter-spacing: 0.05em;
      transition: background 100ms ease, color 100ms ease;
    }

    .ok-btn:hover {
      background: #fff;
      color: #000;
    }
  `]
})
export class MarioModalComponent implements OnInit, OnDestroy {
  readonly title   = input<string>('');
  readonly content = input.required<string>();
  readonly closed  = output<void>();

  readonly charIndex = signal(0);
  readonly animDone  = signal(false);

  readonly displayedText = computed(() => this.content().slice(0, this.charIndex()));

  private intervalId: ReturnType<typeof setInterval> | null = null;

  // Velocidade: ~3 caracteres por tick de 16ms (~60fps)
  private readonly CHARS_PER_TICK = 3;

  ngOnInit(): void {
    const full = this.content();
    if (!full) {
      this.animDone.set(true);
      return;
    }

    this.intervalId = setInterval(() => {
      const next = Math.min(this.charIndex() + this.CHARS_PER_TICK, full.length);
      this.charIndex.set(next);
      if (next >= full.length) {
        this.clearInterval();
        this.animDone.set(true);
      }
    }, 16);
  }

  ngOnDestroy(): void {
    this.clearInterval();
  }

  skipAnimation(): void {
    this.clearInterval();
    this.charIndex.set(this.content().length);
    this.animDone.set(true);
  }

  closeModal(): void {
    this.closed.emit();
  }

  private clearInterval(): void {
    if (this.intervalId !== null) {
      clearInterval(this.intervalId);
      this.intervalId = null;
    }
  }
}
