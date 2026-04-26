import { Component, inject, input, signal } from '@angular/core';
import { ThemeService } from '../../../core/services/theme.service';
import { TweaksPanelComponent } from '../tweaks-panel/tweaks-panel.component';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [TweaksPanelComponent],
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.css']
})
export class HeaderComponent {
  readonly title      = input<string>('');
  readonly subtitle   = input<string>('');
  readonly theme      = inject(ThemeService);
  readonly tweaksOpen = signal(false);
}
