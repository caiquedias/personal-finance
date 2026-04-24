import { Component, inject, input } from '@angular/core';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-header',
  standalone: true,
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.css']
})
export class HeaderComponent {
  readonly title    = input<string>('');
  readonly subtitle = input<string>('');
  readonly theme    = inject(ThemeService);
}
