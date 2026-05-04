import { Component, input, signal, computed, OnDestroy } from '@angular/core';
import { DatePipe } from '@angular/common';

@Component({
  selector: 'app-thought-bubble',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './thought-bubble.component.html',
  styleUrls: ['./thought-bubble.component.css'],
})
export class ThoughtBubbleComponent implements OnDestroy {
  readonly description = input.required<string>();
  readonly dueDate     = input.required<string>();

  readonly open         = signal(false);
  readonly bubblesPhase = signal(0); // 0=hidden, 1=dot1, 2=dot2, 3=dot3, 4=cloud
  readonly charIndex    = signal(0);
  readonly animDone     = signal(false);
  readonly showDueDate  = signal(false);

  readonly displayedText = computed(() => this.description().slice(0, this.charIndex()));

  readonly formattedDueDate = computed(() => {
    const raw = this.dueDate();
    if (!raw) return '';
    const d = new Date(raw + 'T00:00:00');
    const dd = String(d.getDate()).padStart(2, '0');
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const yy = String(d.getFullYear()).slice(2);
    return `${dd}/${mm}/${yy}`;
  });

  readonly isDueWarning = computed(() => {
    const raw = this.dueDate();
    if (!raw) return false;
    const due   = new Date(raw + 'T00:00:00');
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const diff = (due.getTime() - today.getTime()) / (1000 * 60 * 60 * 24);
    return diff >= 0 && diff <= 3;
  });

  private timeouts: ReturnType<typeof setTimeout>[] = [];
  private intervalId: ReturnType<typeof setInterval> | null = null;

  toggle(): void {
    if (this.open()) {
      this.close();
    } else {
      this.openBubble();
    }
  }

  close(): void {
    this.open.set(false);
    this.bubblesPhase.set(0);
    this.charIndex.set(0);
    this.animDone.set(false);
    this.showDueDate.set(false);
    this.clearTimers();
  }

  private openBubble(): void {
    this.open.set(true);
    this.bubblesPhase.set(1);

    const t1 = setTimeout(() => this.bubblesPhase.set(2), 250);
    const t2 = setTimeout(() => this.bubblesPhase.set(3), 500);
    const t3 = setTimeout(() => {
      this.bubblesPhase.set(4);
      this.startTypewriter();
    }, 800);

    this.timeouts.push(t1, t2, t3);
  }

  private startTypewriter(): void {
    const full = this.description();
    if (!full) {
      this.animDone.set(true);
      this.showDueDate.set(true);
      return;
    }

    this.intervalId = setInterval(() => {
      const next = Math.min(this.charIndex() + 1, full.length);
      this.charIndex.set(next);
      if (next >= full.length) {
        this.clearInterval();
        this.animDone.set(true);
        const t = setTimeout(() => this.showDueDate.set(true), 200);
        this.timeouts.push(t);
      }
    }, 30);
  }

  ngOnDestroy(): void {
    this.clearTimers();
  }

  private clearTimers(): void {
    this.timeouts.forEach(t => clearTimeout(t));
    this.timeouts = [];
    this.clearInterval();
  }

  private clearInterval(): void {
    if (this.intervalId !== null) {
      clearInterval(this.intervalId);
      this.intervalId = null;
    }
  }
}
