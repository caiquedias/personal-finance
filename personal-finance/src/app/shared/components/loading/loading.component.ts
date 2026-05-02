import { Component, computed, effect, inject, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { LoadingService } from '../../../core/services/loading.service';

type AnimState = 'entering' | 'idle' | 'exiting' | 'hidden';

@Component({
  selector: 'app-loading',
  standalone: true,
  imports: [NgClass],
  templateUrl: './loading.component.html',
  styleUrl: './loading.component.scss'
})
export class LoadingComponent {
  private readonly loadingService = inject(LoadingService);
  private pendingHide = false;

  protected readonly animState = signal<AnimState>('hidden');
  protected readonly visible = signal(false);
  protected readonly gif = this.loadingService.currentGif;

  protected readonly animClass = computed(() => {
    const state = this.animState();
    if (state === 'entering') return 'anim-entering';
    if (state === 'exiting') return 'anim-exiting';
    if (state === 'idle') {
      const idle = this.gif().idleAnimation;
      if (idle === 'bounce-horizontal') return 'anim-idle-bounce-h';
      if (idle === 'bounce-vertical') return 'anim-idle-bounce-v';
    }
    return '';
  });

  constructor() {
    effect(() => {
      const loading = this.loadingService.isLoading();
      if (loading) {
        this.pendingHide = false;
        this.visible.set(true);
        this.animState.set('entering');
      } else if (this.animState() === 'idle') {
        this.animState.set('exiting');
      } else if (this.animState() === 'entering') {
        this.pendingHide = true;
      }
    });
  }

  protected onAnimationEnd(): void {
    const state = this.animState();
    if (state === 'entering') {
      this.animState.set(this.pendingHide ? 'exiting' : 'idle');
    } else if (state === 'exiting') {
      this.animState.set('hidden');
      this.visible.set(false);
    }
  }
}
