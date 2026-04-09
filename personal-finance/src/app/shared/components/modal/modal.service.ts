import { Injectable, inject } from '@angular/core';
import { TemplatePortal } from '@angular/cdk/portal';
import { Overlay, OverlayRef } from '@angular/cdk/overlay';

@Injectable({ providedIn: 'root' })
export class ModalService {
  private readonly overlay = inject(Overlay);
  private overlayRef: OverlayRef | null = null;

  open(portal: TemplatePortal): void {
    this.destroyImmediate(); // limpa sem animação ao abrir novo

    this.overlayRef = this.overlay.create({
      hasBackdrop:      true,
      backdropClass:    'modal-backdrop',
      panelClass:       'modal-panel',
      positionStrategy: this.overlay.position()
        .global()
        .centerHorizontally()
        .centerVertically(),
      scrollStrategy: this.overlay.scrollStrategies.block(),
    });

    this.overlayRef.attach(portal);
    this.overlayRef.backdropClick().subscribe(() => this.close());
  }

  close(): void {
    if (!this.overlayRef) return;

    const pane = this.overlayRef.overlayElement;

    if (!pane) {
      this.destroyImmediate();
      return;
    }

    // Adiciona classe de saída no filho direto (onde está a animação)
    const child = pane.firstElementChild as HTMLElement | null;
    if (child) {
      child.style.animation = 'modalOut 180ms ease-in forwards';

      // Aguarda fim da animação para destruir
      const onEnd = () => {
        child.removeEventListener('animationend', onEnd);
        this.destroyImmediate();
      };
      child.addEventListener('animationend', onEnd);

      // Fallback: destrói após 250ms caso animationend não dispare
      setTimeout(() => this.destroyImmediate(), 250);
    } else {
      this.destroyImmediate();
    }
  }

  private destroyImmediate(): void {
    if (this.overlayRef) {
      this.overlayRef.detach();
      this.overlayRef.dispose();
      this.overlayRef = null;
    }
  }
}
