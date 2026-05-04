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
    fixture.componentRef.setInput('dueDate', '2099-12-31');
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
    tick(1000);
    fixture.detectChanges();
  }));

  it('deve progredir as fases das bolhas', fakeAsync(() => {
    component.toggle();
    tick(250);
    expect(component.bubblesPhase()).toBe(2);
    tick(250);
    expect(component.bubblesPhase()).toBe(3);
    tick(300);
    expect(component.bubblesPhase()).toBe(4);
    tick(1000);
  }));

  it('deve executar o typewriter e marcar animDone', fakeAsync(() => {
    component.toggle();
    tick(800);
    const len = 'Aluguel mensal'.length;
    tick(len * 30 + 500);
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
    const iso = tomorrow.toISOString().slice(0, 10);
    fixture.componentRef.setInput('dueDate', iso);
    fixture.detectChanges();
    expect(component.isDueWarning()).toBeTrue();
  });

  it('formattedDueDate deve formatar corretamente', () => {
    fixture.componentRef.setInput('dueDate', '2025-03-05');
    fixture.detectChanges();
    expect(component.formattedDueDate()).toBe('05/03/25');
  });
});
