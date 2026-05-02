import { Injectable, signal } from '@angular/core';

export type IdleAnimation = 'bounce-horizontal' | 'bounce-vertical' | 'none';

export interface GifConfig {
  src: string;
  idleAnimation: IdleAnimation;
}

const GIFS: GifConfig[] = [
  { src: 'green-shell-mario.gif', idleAnimation: 'bounce-horizontal' },
  { src: 'dr-eggman.gif', idleAnimation: 'bounce-vertical' },
  { src: 'sonic-running.gif', idleAnimation: 'none' },
  { src: 'sonic-running-2.gif', idleAnimation: 'none' },
];

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private _requestCount = 0;
  private readonly _isLoading = signal(false);
  private readonly _currentGif = signal<GifConfig>(GIFS[0]);

  readonly isLoading = this._isLoading.asReadonly();
  readonly currentGif = this._currentGif.asReadonly();

  show(): void {
    if (this._requestCount === 0) {
      this._currentGif.set(GIFS[Math.floor(Math.random() * GIFS.length)]);
      this._isLoading.set(true);
    }
    this._requestCount++;
  }

  hide(): void {
    this._requestCount = Math.max(0, this._requestCount - 1);
    if (this._requestCount === 0) {
      this._isLoading.set(false);
    }
  }
}
