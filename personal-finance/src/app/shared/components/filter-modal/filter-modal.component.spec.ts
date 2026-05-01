import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { FilterModalComponent } from './filter-modal.component';
import { FilterFieldConfig } from './filter-field-config';

const TEXT_FIELD: FilterFieldConfig = { key: 'description', label: 'Descrição', type: 'text', value: '' };
const SELECT_FIELD: FilterFieldConfig = {
  key: 'status', label: 'Status', type: 'select',
  options: [{ value: '', label: 'Todos' }, { value: 1, label: 'Pendente' }, { value: 2, label: 'Pago' }],
  value: '',
};
const MULTI_FIELD: FilterFieldConfig = {
  key: 'tags', label: 'Tags', type: 'multiSelect',
  options: [{ value: 'a', label: 'A' }, { value: 'b', label: 'B' }],
  value: [],
};

describe('FilterModalComponent', () => {
  let fixture: ComponentFixture<FilterModalComponent>;
  let component: FilterModalComponent;

  async function setup(fields: FilterFieldConfig[], open = true): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [FilterModalComponent, BrowserAnimationsModule],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(FilterModalComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('fields', fields);
    fixture.componentRef.setInput('open', open);
    fixture.detectChanges();
  }

  describe('render dinâmico de campos', () => {
    it('renderiza campo text', async () => {
      await setup([TEXT_FIELD]);
      const input = fixture.nativeElement.querySelector('input:not([type=checkbox])');
      expect(input).toBeTruthy();
    });

    it('renderiza campo select com opções', async () => {
      await setup([SELECT_FIELD]);
      const select = fixture.nativeElement.querySelector('select');
      expect(select).toBeTruthy();
      expect(select.querySelectorAll('option').length).toBe(3);
    });

    it('renderiza campo multiSelect com checkboxes', async () => {
      await setup([MULTI_FIELD]);
      const checkboxes = fixture.nativeElement.querySelectorAll('input[type=checkbox]');
      expect(checkboxes.length).toBe(2);
    });

    it('não renderiza painel quando open=false', async () => {
      await setup([TEXT_FIELD], false);
      const panel = fixture.nativeElement.querySelector('.filter-panel');
      expect(panel).toBeNull();
    });
  });

  describe('badge de count (activeCount)', () => {
    it('draftValues inicializa vazio para campo text', async () => {
      await setup([TEXT_FIELD]);
      expect(component.getStringValue('description')).toBe('');
    });

    it('draftValues inicializa array vazio para campo multiSelect', async () => {
      await setup([MULTI_FIELD]);
      expect(component.getArrayValue('tags')).toEqual([]);
    });

    it('draftValues reflete value inicial do campo', async () => {
      const field: FilterFieldConfig = { key: 'status', label: 'Status', type: 'select', options: [], value: 1 };
      await setup([field]);
      expect(component.getStringValue('status')).toBe(1 as unknown as string);
    });
  });

  describe('emissão de apply', () => {
    it('emite apply com os valores do rascunho', async () => {
      await setup([TEXT_FIELD, SELECT_FIELD]);
      const emitted: Record<string, unknown>[] = [];
      component.apply.subscribe(v => emitted.push(v));

      component.setValue('description', 'teste');
      component.setValue('status', 1);
      component.onApply();

      expect(emitted.length).toBe(1);
      expect(emitted[0]['description']).toBe('teste');
      expect(emitted[0]['status']).toBe(1);
    });
  });

  describe('emissão de clear', () => {
    it('emite clear e reseta draftValues para vazio', async () => {
      await setup([TEXT_FIELD, SELECT_FIELD]);
      let clearCount = 0;
      component.clear.subscribe(() => clearCount++);

      component.setValue('description', 'abc');
      component.onClear();

      expect(clearCount).toBe(1);
      expect(component.getStringValue('description')).toBe('');
      expect(component.getStringValue('status')).toBe('');
    });

    it('reseta array para multiSelect no clear', async () => {
      await setup([MULTI_FIELD]);
      component.toggleMultiValue('tags', 'a');
      expect(component.getArrayValue('tags')).toContain('a');

      component.onClear();
      expect(component.getArrayValue('tags')).toEqual([]);
    });
  });

  describe('toggleMultiValue', () => {
    it('adiciona valor ao array', async () => {
      await setup([MULTI_FIELD]);
      component.toggleMultiValue('tags', 'a');
      expect(component.isChecked('tags', 'a')).toBeTrue();
    });

    it('remove valor existente do array', async () => {
      await setup([MULTI_FIELD]);
      component.toggleMultiValue('tags', 'a');
      component.toggleMultiValue('tags', 'a');
      expect(component.isChecked('tags', 'a')).toBeFalse();
    });
  });
});
