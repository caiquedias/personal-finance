import {
  Component, input, output, OnInit, OnDestroy,
  signal, HostListener
} from '@angular/core';

@Component({
  selector: 'app-sonic-modal',
  standalone: true,
  templateUrl: './sonic-modal.component.html',
  styleUrls: ['./sonic-modal.component.css']
})
export class SonicModalComponent {
  readonly title  = input<string>('');
  readonly closed = output<void>();

  // Padrão de blocos — 'l' = light, 'd' = dark
  readonly blocks: ('l' | 'd')[] = [
    'l','d','l','l','d','l','l','d','l','l','d','l',
    'l','d','l','l','d','l','l','d','l','l','d','l',
    'l','d','l','l','d','l',
  ];

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}
