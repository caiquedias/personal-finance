import { Injectable, signal } from '@angular/core';
import { Density } from '../../shared/components/tweaks-panel/tweaks-panel.component';

const KEY_ACCENT  = 'pf_accent';
const KEY_DENSITY = 'pf_density';

const DENSITY_GAPS: Record<Density, string> = {
  compact:     '12px',
  normal:      '24px',
  comfortable: '36px',
};

const DENSITY_PADDING: Record<Density, string> = {
  compact:     '16px',
  normal:      '28px',
  comfortable: '40px',
};

@Injectable({ providedIn: 'root' })
export class TweaksService {
  readonly accent  = signal<string>(localStorage.getItem(KEY_ACCENT)  ?? '#C4674A');
  readonly density = signal<Density>((localStorage.getItem(KEY_DENSITY) as Density) ?? 'normal');

  setAccent(color: string): void {
    this.accent.set(color);
    localStorage.setItem(KEY_ACCENT, color);
    this.applyAccent(color);
  }

  setDensity(density: Density): void {
    this.density.set(density);
    localStorage.setItem(KEY_DENSITY, density);
    this.applyDensity(density);
  }

  /** Aplica os valores persistidos no DOM (chamar no bootstrap). */
  apply(): void {
    this.applyAccent(this.accent());
    this.applyDensity(this.density());
  }

  private applyAccent(color: string): void {
    document.documentElement.style.setProperty('--terracotta', color);
  }

  private applyDensity(density: Density): void {
    document.documentElement.style.setProperty('--density-gap',     DENSITY_GAPS[density]);
    document.documentElement.style.setProperty('--density-padding', DENSITY_PADDING[density]);
  }
}
