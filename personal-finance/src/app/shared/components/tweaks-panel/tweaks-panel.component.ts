import { Component, inject, output, signal, OnInit } from '@angular/core';
import { TweaksService } from '../../../core/services/tweaks.service';

const ACCENT_COLORS = [
  { name: 'Terracotta', value: '#C4674A' },
  { name: 'Slate',      value: '#6B8CAD' },
  { name: 'Olive',      value: '#7B8C5F' },
  { name: 'Purple',     value: '#9E8BB5' },
  { name: 'Rose',       value: '#B57A9E' },
  { name: 'Gold',       value: '#C4924A' },
];

export type Density = 'compact' | 'normal' | 'comfortable';

@Component({
  selector: 'app-tweaks-panel',
  standalone: true,
  templateUrl: './tweaks-panel.component.html',
  styleUrls: ['./tweaks-panel.component.css']
})
export class TweaksPanelComponent implements OnInit {
  readonly tweaks  = inject(TweaksService);
  readonly close   = output<void>();

  readonly accentColors = ACCENT_COLORS;
  readonly densities: { label: string; value: Density }[] = [
    { label: 'Compacto',     value: 'compact'     },
    { label: 'Normal',       value: 'normal'      },
    { label: 'Confortável',  value: 'comfortable' },
  ];

  ngOnInit(): void {
    this.tweaks.apply();
  }

  setAccent(color: string): void {
    this.tweaks.setAccent(color);
  }

  setDensity(density: Density): void {
    this.tweaks.setDensity(density);
  }
}
