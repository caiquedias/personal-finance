import { TestBed } from '@angular/core/testing';
import { LoadingService } from './loading.service';

describe('LoadingService', () => {
  let service: LoadingService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(LoadingService);
  });

  it('deve iniciar com isLoading false', () => {
    expect(service.isLoading()).toBeFalse();
  });

  it('show() deve definir isLoading como true', () => {
    service.show();
    expect(service.isLoading()).toBeTrue();
  });

  it('hide() após show() deve definir isLoading como false', () => {
    service.show();
    service.hide();
    expect(service.isLoading()).toBeFalse();
  });

  it('deve exigir hide() correspondente para múltiplos show()', () => {
    service.show();
    service.show();
    service.hide();
    expect(service.isLoading()).toBeTrue();
    service.hide();
    expect(service.isLoading()).toBeFalse();
  });

  it('currentGif deve ser um dos 4 GIFs válidos', () => {
    const validSrcs = ['green-shell-mario.gif', 'dr-eggman.gif', 'soni-running.gif', 'soni-running-2.gif'];
    service.show();
    expect(validSrcs).toContain(service.currentGif().src);
  });

  it('show() adicional não deve trocar o gif selecionado', () => {
    service.show();
    const firstGif = service.currentGif().src;
    service.show();
    expect(service.currentGif().src).toBe(firstGif);
  });

  it('hide() sem show() não deve lançar erro', () => {
    expect(() => service.hide()).not.toThrow();
    expect(service.isLoading()).toBeFalse();
  });

  it('currentGif deve ter idleAnimation válido', () => {
    const validAnimations = ['bounce-horizontal', 'bounce-vertical', 'none'];
    service.show();
    expect(validAnimations).toContain(service.currentGif().idleAnimation);
  });
});
