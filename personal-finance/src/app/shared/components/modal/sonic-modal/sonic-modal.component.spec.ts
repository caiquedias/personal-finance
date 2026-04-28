import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { SonicModalComponent } from './sonic-modal.component';

describe('SonicModalComponent', () => {
  let fixture: ComponentFixture<SonicModalComponent>;
  let component: SonicModalComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SonicModalComponent],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(SonicModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  it('title tem valor padrão vazio', () => {
    expect(component.title()).toBe('');
  });

  it('title recebe valor via input', () => {
    fixture.componentRef.setInput('title', 'Meu Modal');
    fixture.detectChanges();
    expect(component.title()).toBe('Meu Modal');
  });

  it('subtitle tem valor padrão vazio', () => {
    expect(component.subtitle()).toBe('');
  });

  it('subtitle recebe valor via input', () => {
    fixture.componentRef.setInput('subtitle', 'Subtítulo do modal');
    fixture.detectChanges();
    expect(component.subtitle()).toBe('Subtítulo do modal');
  });

  it('emite "closed" ao pressionar Escape', () => {
    let emitted = false;
    component.closed.subscribe(() => { emitted = true; });

    component.onEscape();

    expect(emitted).toBeTrue();
  });

  it('emite "closed" ao clicar no botão X', () => {
    let emitted = false;
    component.closed.subscribe(() => { emitted = true; });

    fixture.detectChanges();
    const closeBtn = fixture.nativeElement.querySelector('.sonic-modal-close');
    closeBtn?.click();

    expect(emitted).toBeTrue();
  });

});
