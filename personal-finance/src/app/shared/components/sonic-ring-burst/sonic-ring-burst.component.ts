import { Component, input, output, signal, OnInit, OnDestroy, ChangeDetectionStrategy } from '@angular/core';

interface Ring {
  id: number;
  x: number;
  y: number;
  vx: number;
  vy: number;
  ground: number;
  bounces: number;
  opacity: number;
  blinking: boolean;
  blinkFrame: number;
  size: number;
}

@Component({
  selector: 'app-sonic-ring-burst',
  standalone: true,
  imports: [],
  templateUrl: './sonic-ring-burst.component.html',
  styleUrls: ['./sonic-ring-burst.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SonicRingBurstComponent implements OnInit, OnDestroy {
  readonly origin = input.required<{ x: number; y: number }>();
  readonly done = output<void>();

  readonly rings = signal<Ring[]>([]);

  private rafId: number | null = null;
  private readonly G = 0.32;
  private readonly BOUNCE_AMP = 0.50;
  private readonly FRICTION = 0.88;

  ngOnInit(): void {
    const COUNT = 24;
    const o = this.origin();
    this.rings.set(Array.from({ length: COUNT }, (_, i) => {
      const pct   = i / (COUNT - 1);
      const deg   = 200 + pct * 140;
      const rad   = (deg * Math.PI) / 180;
      const inner = i % 2 === 0;
      const speed = inner ? 3 + pct * 2 : 5 + pct * 3;
      return {
        id: i,
        x: o.x,
        y: o.y,
        vx: Math.cos(rad) * speed,
        vy: Math.sin(rad) * speed,
        ground: o.y + 90 + Math.random() * 60,
        bounces: 0,
        opacity: 1,
        blinking: false,
        blinkFrame: 0,
        size: inner ? 20 : 24,
      };
    }));
    this.rafId = requestAnimationFrame(() => this.step());
  }

  private blinkOpacity(f: number): number {
    if (f < 120) { const fi = f % 40;        return fi < 20 ? 1 : 0; }
    if (f < 180) { const fi = (f - 120) % 20; return fi < 10 ? 1 : 0; }
    if (f < 220) { const fi = (f - 180) % 10; return fi < 5  ? 1 : 0; }
    return -1;
  }

  private step(): void {
    let alive = 0;
    const updated = this.rings().map(r => {
      if (r.opacity < 0) return r;
      let { x, y, vx, vy, ground, bounces, opacity, blinking, blinkFrame } = r;

      if (!blinking) {
        vy += this.G;
        x  += vx;
        y  += vy;
        if (y >= ground && vy > 0) {
          y       = ground;
          vy      = -Math.abs(vy) * this.BOUNCE_AMP;
          vx     *= this.FRICTION;
          bounces += 1;
          if (bounces >= 3 || Math.abs(vy) < 1.4) {
            blinking = true;
            vy = 0; vx = 0; y = ground;
          }
        }
        opacity = 1;
      } else {
        blinkFrame += 1;
        const bop = this.blinkOpacity(blinkFrame);
        opacity = bop < 0 ? -1 : bop;
      }

      if (opacity >= 0) alive++;
      return { ...r, x, y, vx, vy, bounces, opacity, blinking, blinkFrame };
    });

    this.rings.set(updated);

    if (alive > 0) {
      this.rafId = requestAnimationFrame(() => this.step());
    } else {
      this.done.emit();
    }
  }

  ngOnDestroy(): void {
    if (this.rafId !== null) cancelAnimationFrame(this.rafId);
  }
}
