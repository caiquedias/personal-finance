import { Component, computed, ElementRef, input, OnDestroy, OnInit, output, signal, ViewChild } from '@angular/core';

@Component({
  selector: 'app-mario-modal',
  standalone: true,
  templateUrl: './mario-modal.component.html',
  styleUrls: ['./mario-modal.component.css']
})
export class MarioModalComponent implements OnInit, OnDestroy {
  @ViewChild('contentEl') contentEl!: ElementRef<HTMLDivElement>;

  readonly title   = input<string>('');
  readonly content = input.required<string>();
  readonly closed  = output<void>();

  readonly charIndex = signal(0);
  readonly animDone  = signal(false);

  readonly displayedText = computed(() => this.content().slice(0, this.charIndex()));

  private intervalId: ReturnType<typeof setInterval> | null = null;

  // Velocidade: 1 caractere por tick de 30ms
  private readonly CHARS_PER_TICK = 1;

  ngOnInit(): void {
    const full = this.content();
    if (!full) {
      this.animDone.set(true);
      return;
    }

    this.intervalId = setInterval(() => {
      const next = Math.min(this.charIndex() + this.CHARS_PER_TICK, full.length);
      this.charIndex.set(next);
      this.scrollToBottom();
      if (next >= full.length) {
        this.clearInterval();
        this.animDone.set(true);
      }
    }, 30);
  }

  ngOnDestroy(): void {
    this.clearInterval();
  }

  skipAnimation(): void {
    this.clearInterval();
    this.charIndex.set(this.content().length);
    this.animDone.set(true);
    this.scrollToBottom();
  }

  closeModal(): void {
    this.closed.emit();
  }

  private scrollToBottom(): void {
    const el = this.contentEl?.nativeElement;
    if (el) el.scrollTop = el.scrollHeight;
  }

  private clearInterval(): void {
    if (this.intervalId !== null) {
      clearInterval(this.intervalId);
      this.intervalId = null;
    }
  }
}
