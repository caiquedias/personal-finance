import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { StatCardComponent } from './stat-card.component';

describe('StatCardComponent', () => {
  let fixture: ComponentFixture<StatCardComponent>;
  let component: StatCardComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatCardComponent],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(StatCardComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('label', 'Saldo');
    fixture.componentRef.setInput('value', 1000);
    fixture.detectChanges();
  });

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  describe('isNumber()', () => {
    it('retorna true para número', () => {
      expect(component.isNumber(100)).toBeTrue();
    });

    it('retorna false para string', () => {
      expect(component.isNumber('texto')).toBeFalse();
    });
  });

  describe('asNumber()', () => {
    it('retorna o valor como número', () => {
      expect(component.asNumber(42)).toBe(42);
    });
  });

  describe('inputs', () => {
    it('aceita variant "success"', () => {
      fixture.componentRef.setInput('variant', 'success');
      fixture.detectChanges();
      expect(component.variant()).toBe('success');
    });

    it('aceita variant "danger"', () => {
      fixture.componentRef.setInput('variant', 'danger');
      fixture.detectChanges();
      expect(component.variant()).toBe('danger');
    });

    it('variant padrão é "default"', () => {
      expect(component.variant()).toBe('default');
    });

    it('isCurrency padrão é true', () => {
      expect(component.isCurrency()).toBeTrue();
    });

    it('aceita value como string', () => {
      fixture.componentRef.setInput('value', 'R$ 1.000');
      fixture.detectChanges();
      expect(component.isNumber(component.value())).toBeFalse();
    });
  });
});
