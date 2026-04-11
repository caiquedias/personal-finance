import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { MarioModalComponent } from './mario-modal.component';

describe('MarioModalComponent', () => {
  let fixture: ComponentFixture<MarioModalComponent>;
  let component: MarioModalComponent;

  function createWithContent(content: string): void {
    fixture.componentRef.setInput('content', content);
    fixture.componentRef.setInput('title', 'Teste');
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MarioModalComponent],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(MarioModalComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    // Garante que o intervalo seja limpo
    fixture.destroy();
  });

  it('cria o componente', () => {
    createWithContent('Hello');
    expect(component).toBeTruthy();
  });

  describe('animação typewriter', () => {
    it('começa com charIndex = 0', () => {
      createWithContent('Olá');
      expect(component.charIndex()).toBe(0);
    });

    it('avança charIndex após tick de 30ms', fakeAsync(() => {
      createWithContent('ABC');
      tick(30);
      expect(component.charIndex()).toBeGreaterThan(0);
      tick(60);
    }));

    it('displayedText mostra apenas os chars revelados', fakeAsync(() => {
      createWithContent('ABC');
      tick(30);
      const shown = component.displayedText();
      expect('ABC'.startsWith(shown)).toBeTrue();
      tick(60);
    }));

    it('define animDone=true quando chega ao fim do texto', fakeAsync(() => {
      createWithContent('AB');
      tick(30 * 3); // suficiente para revelar todo o texto
      expect(component.animDone()).toBeTrue();
    }));

    it('conteúdo vazio define animDone=true imediatamente', () => {
      createWithContent('');
      expect(component.animDone()).toBeTrue();
    });
  });

  describe('skipAnimation()', () => {
    it('revela todo o texto e define animDone=true', fakeAsync(() => {
      createWithContent('Texto longo aqui');
      tick(0);
      component.skipAnimation();
      expect(component.charIndex()).toBe('Texto longo aqui'.length);
      expect(component.animDone()).toBeTrue();
      expect(component.displayedText()).toBe('Texto longo aqui');
    }));
  });

  describe('closeModal()', () => {
    it('emite evento "closed"', () => {
      createWithContent('Teste');
      let emitted = false;
      component.closed.subscribe(() => { emitted = true; });
      component.closeModal();
      expect(emitted).toBeTrue();
    });
  });
});
