import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ThoughtBubbleComponent } from './thought-bubble.component';

describe('ThoughtBubbleComponent', () => {
  let fixture: ComponentFixture<ThoughtBubbleComponent>;
  let component: ThoughtBubbleComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ThoughtBubbleComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ThoughtBubbleComponent);
    fixture.componentRef.setInput('description', 'Aluguel mensal');
    fixture.componentRef.setInput('notes', 'Observação de teste');
    fixture.componentRef.setInput('dueDate', '2099-12-31');
    fixture.componentRef.setInput('amount', 250.00);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('deve iniciar fechado', () => {
    expect(component.open()).toBeFalse();
    expect(component.bubblesPhase()).toBe(0);
  });

  it('deve abrir ao chamar toggle()', fakeAsync(() => {
    component.toggle();
    expect(component.open()).toBeTrue();
    expect(component.bubblesPhase()).toBe(1);
    tick(2000);
    fixture.detectChanges();
  }));

  it('deve progredir as fases das bolhas', fakeAsync(() => {
    component.toggle();
    tick(300);
    expect(component.bubblesPhase()).toBe(2);
    tick(300);
    expect(component.bubblesPhase()).toBe(3);
    tick(350);
    expect(component.bubblesPhase()).toBe(4);
    tick(2000);
  }));

  it('deve executar o typewriter e marcar animDone', fakeAsync(() => {
    component.toggle();
    tick(950);
    const len = 'Observação de teste'.length;
    tick(len * 70 + 600);
    expect(component.animDone()).toBeTrue();
    expect(component.showDueDate()).toBeTrue();
  }));

  it('deve usar description como fallback quando notes vazio', fakeAsync(() => {
    fixture.componentRef.setInput('notes', '');
    fixture.detectChanges();
    component.toggle();
    tick(950);
    const len = 'Aluguel mensal'.length;
    tick(len * 70 + 600);
    expect(component.animDone()).toBeTrue();
    expect(component.showDueDate()).toBeTrue();
  }));

  it('deve fechar e resetar estado ao chamar close()', fakeAsync(() => {
    component.toggle();
    tick(1000);
    component.close();
    expect(component.open()).toBeFalse();
    expect(component.bubblesPhase()).toBe(0);
    expect(component.charIndex()).toBe(0);
    expect(component.animDone()).toBeFalse();
    expect(component.showDueDate()).toBeFalse();
  }));

  it('isDueWarning deve ser false para data distante', () => {
    expect(component.isDueWarning()).toBeFalse();
  });

  it('isDueWarning deve ser true para vencimento dentro de 3 dias', () => {
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    fixture.componentRef.setInput('dueDate', tomorrow.toISOString().slice(0, 10));
    fixture.detectChanges();
    expect(component.isDueWarning()).toBeTrue();
  });

  it('formattedDueDate deve formatar corretamente', () => {
    fixture.componentRef.setInput('dueDate', '2025-03-05');
    fixture.detectChanges();
    expect(component.formattedDueDate()).toBe('05/03/25');
  });

  it('formattedAmount deve formatar em BRL', () => {
    fixture.componentRef.setInput('amount', 1500.50);
    fixture.detectChanges();
    expect(component.formattedAmount()).toContain('1.500,50');
  });
});
