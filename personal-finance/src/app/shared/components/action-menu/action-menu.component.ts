import { Component, Output, EventEmitter, signal, HostListener, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-action-menu',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './action-menu.component.html',
  styleUrls: ['./action-menu.component.css'],
})
export class ActionMenuComponent {
  @Input() showPay = true;
  @Output() pay    = new EventEmitter<MouseEvent>();
  @Output() edit   = new EventEmitter<void>();
  @Output() delete = new EventEmitter<void>();

  readonly open = signal(false);

  toggle(e: MouseEvent): void {
    e.stopPropagation();
    this.open.update(v => !v);
  }

  close(): void { this.open.set(false); }

  onPay(e: MouseEvent): void  { this.pay.emit(e);  this.close(); }
  onEdit(): void              { this.edit.emit();   this.close(); }
  onDelete(): void            { this.delete.emit(); this.close(); }

  @HostListener('document:click')
  onDocumentClick(): void { this.close(); }
}
