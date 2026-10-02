import { Injectable } from '@angular/core';
import QRCode from 'qrcode';

/** Gera QR code localmente (nunca usar serviço externo — vazaria o secret). */
@Injectable({ providedIn: 'root' })
export class QrCodeService {
  toDataUrl(text: string): Promise<string> {
    return QRCode.toDataURL(text, { margin: 4, width: 220 });
  }
}
