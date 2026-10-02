import { TestBed } from '@angular/core/testing';
import QRCode from 'qrcode';
import { QrCodeService } from './qr-code.service';

describe('QrCodeService', () => {
  let service: QrCodeService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(QrCodeService);
  });

  it('gera o QR com quiet zone de 4 módulos (margin: 4)', async () => {
    const spy = spyOn(QRCode as any, 'toDataURL').and.returnValue(Promise.resolve('data:image/png;base64,x'));

    const result = await service.toDataUrl('otpauth://totp/x?secret=ABC');

    expect(result).toBe('data:image/png;base64,x');
    expect(spy).toHaveBeenCalledTimes(1);
    const [text, options] = spy.calls.mostRecent().args as [string, { margin?: number }];
    expect(text).toBe('otpauth://totp/x?secret=ABC');
    expect(options.margin).toBe(4);
  });
});
