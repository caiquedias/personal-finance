import { Component, input, output, HostListener } from '@angular/core';

@Component({
  selector: 'app-sonic-modal',
  standalone: true,
  templateUrl: './sonic-modal.component.html',
  styleUrls: ['./sonic-modal.component.css']
})
export class SonicModalComponent {
  readonly title    = input<string>('');
  readonly subtitle = input<string>('');
  readonly closed   = output<void>();

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}
