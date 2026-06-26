import { Component, OnInit, OnDestroy, ViewEncapsulation } from '@angular/core';

@Component({
  selector: 'app-purge-warning-banner',
  standalone: true,
  encapsulation: ViewEncapsulation.None,
  template: `
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link href="https://fonts.googleapis.com/css2?family=Press+Start+2P&display=swap" rel="stylesheet">

    <style>
      .purge-warning-banner {
        background: #000000;
        color: #ffffff;
        font-family: 'Press Start 2P', monospace;
        font-size: 0.75rem;
        padding: 1rem 1.5rem;
        display: flex;
        align-items: center;
        gap: 0.75rem;
        justify-content: center;
        text-align: center;
        line-height: 1.6;
      }

      .purge-warning-banner .banner-icon {
        font-size: 1.25rem;
        animation: pulse-warning 1.2s ease-in-out infinite;
        flex-shrink: 0;
      }

      @keyframes pulse-warning {
        0%   { color: #ffcc00; }
        33%  { color: #ff8800; }
        66%  { color: #ff2200; }
        100% { color: #ffcc00; }
      }
    </style>

    <div class="purge-warning-banner">
      <span class="banner-icon">⚠</span>
      <span>{{ displayedText }}</span>
      <span class="banner-icon">⚠</span>
    </div>
  `,
})
export class PurgeWarningBannerComponent implements OnInit, OnDestroy {

  readonly fullText = 'Atenção: Exibição de período expurgado';
  displayedText = '';
  private intervalId: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    let index = 0;
    this.intervalId = setInterval(() => {
      if (index < this.fullText.length) {
        this.displayedText += this.fullText[index];
        index++;
      } else {
        clearInterval(this.intervalId!);
        this.intervalId = null;
      }
    }, 60);
  }

  ngOnDestroy(): void {
    if (this.intervalId !== null) {
      clearInterval(this.intervalId);
    }
  }
}
