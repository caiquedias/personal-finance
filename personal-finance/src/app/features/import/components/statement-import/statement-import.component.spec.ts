import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { StatementImportComponent } from './statement-import.component';
import { ApiService } from '../../../../core/services/api.service';

function makeFile(name: string, size = 1024): File {
  return new File([new Uint8Array(size)], name, { type: 'application/pdf' });
}
function fileEvent(file: File): Event {
  return { target: { files: [file], value: '' } } as unknown as Event;
}

const CATS = [
  { id: 'cat-1', name: 'Mercado', color: '#fff', icon: null, userId: null, isGlobal: true, isActive: true },
  { id: 'cat-2', name: 'Lazer',   color: '#000', icon: null, userId: null, isGlobal: true, isActive: true },
];

const PREVIEW = {
  discardedByDateCount: 3,
  items: [
    { date: '2026-01-10', description: 'Supermercado', amount: 100, kind: 'Expense', suggestedCategoryId: 'cat-1',
      isLikelyInternalTransfer: false, isLikelyDuplicate: false, duplicateOfId: null },
    { date: '2026-01-11', description: 'Salario', amount: 5000, kind: 'Income', suggestedCategoryId: null,
      isLikelyInternalTransfer: false, isLikelyDuplicate: false, duplicateOfId: null },
    { date: '2026-01-12', description: 'PIX proprio', amount: 50, kind: 'Expense', suggestedCategoryId: null,
      isLikelyInternalTransfer: true, isLikelyDuplicate: true, duplicateOfId: 'dup-1' },
  ],
};

/*
 * Contrato esperado do componente (públicos):
 *  signals: selectedFile, password, fromDate, categories, items, discardedCount,
 *           errorMessage, result, saving
 *  métodos: onFileSelected(event), preview(), removeItem(i), updateItem(i, patch), canSave(), save()
 *  item:    { date, description, amount, kind, categoryId, sourceType, isLikelyInternalTransfer, isLikelyDuplicate }
 */
describe('StatementImportComponent', () => {
  let fixture: ComponentFixture<StatementImportComponent>;
  let c: any;
  let api: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<ApiService>('ApiService',
      ['getCategories', 'previewStatementImport', 'confirmStatementImport']);
    api.getCategories.and.returnValue(of(CATS as any));
    api.previewStatementImport.and.returnValue(of(PREVIEW as any));
    api.confirmStatementImport.and.returnValue(of({
      periodsCreated: 1, periodsReused: 2, expensesCreated: 2, incomesCreated: 1 } as any));

    await TestBed.configureTestingModule({
      imports: [StatementImportComponent],
      providers: [{ provide: ApiService, useValue: api }],
    }).compileComponents();

    fixture = TestBed.createComponent(StatementImportComponent);
    c = fixture.componentInstance;
    fixture.detectChanges();
  });

  function loadPreview() {
    c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
    c.fromDate.set('2026-01-01');
    c.preview();
    fixture.detectChanges();
  }

  it('carrega categorias na inicialização', () => {
    expect(api.getCategories).toHaveBeenCalled();
    expect(c.categories().length).toBe(2);
  });

  describe('upload', () => {
    it('rejeita arquivo que não é .pdf', () => {
      c.onFileSelected(fileEvent(makeFile('planilha.xlsx')));
      expect(c.selectedFile()).toBeNull();
      expect(c.errorMessage()).toBeTruthy();
    });

    it('rejeita arquivo maior que 10 MB', () => {
      c.onFileSelected(fileEvent(makeFile('grande.pdf', 10 * 1024 * 1024 + 1)));
      expect(c.selectedFile()).toBeNull();
      expect(c.errorMessage()).toBeTruthy();
    });

    it('aceita .pdf válido', () => {
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      expect(c.selectedFile()!.name).toBe('extrato.pdf');
      expect(c.errorMessage()).toBeNull();
    });

    it('preview não chama a API sem arquivo', () => {
      c.preview();
      expect(api.previewStatementImport).not.toHaveBeenCalled();
    });
  });

  describe('preview', () => {
    it('chama API com arquivo, senha e data de início', () => {
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      c.password.set('123');
      c.fromDate.set('2026-01-01');
      c.preview();
      expect(api.previewStatementImport).toHaveBeenCalledWith(
        jasmine.any(File), '123', '2026-01-01');
    });

    it('popula itens mapeando suggestedCategoryId para categoryId', () => {
      loadPreview();
      const items = c.items();
      expect(items.length).toBe(3);
      expect(items[0].categoryId).toBe('cat-1');
      expect(items[1].categoryId).toBeNull();
      expect(items[0].kind).toBe('Expense');
      expect(items[1].kind).toBe('Income');
    });

    it('expõe discardedByDateCount', () => {
      loadPreview();
      expect(c.discardedCount()).toBe(3);
    });

    it('preserva flags de transferência interna e duplicata nas linhas', () => {
      loadPreview();
      expect(c.items()[2].isLikelyInternalTransfer).toBeTrue();
      expect(c.items()[2].isLikelyDuplicate).toBeTrue();
      expect(c.items().length).toBe(3);
    });

    it('exibe err.error.message em erro 400 (ex.: senha incorreta)', () => {
      api.previewStatementImport.and.returnValue(
        throwError(() => ({ status: 400, error: { message: 'Senha incorreta.' } })));
      loadPreview();
      expect(c.errorMessage()).toBe('Senha incorreta.');
      expect(c.items().length).toBe(0);
    });
  });

  describe('edição de linhas', () => {
    beforeEach(() => loadPreview());

    it('removeItem remove a linha pelo índice', () => {
      c.removeItem(0);
      expect(c.items().length).toBe(2);
      expect(c.items()[0].description).toBe('Salario');
    });

    it('updateItem altera campos da linha', () => {
      c.updateItem(0, { amount: 250, description: 'Novo', categoryId: 'cat-2' });
      expect(c.items()[0].amount).toBe(250);
      expect(c.items()[0].description).toBe('Novo');
      expect(c.items()[0].categoryId).toBe('cat-2');
    });
  });

  describe('validações (canSave)', () => {
    beforeEach(() => {
      loadPreview();
      c.updateItem(2, { categoryId: 'cat-1' });
    });

    it('habilita salvar com linhas válidas', () => {
      expect(c.canSave()).toBeTrue();
    });

    it('bloqueia Expense sem categoria', () => {
      c.updateItem(0, { categoryId: null });
      expect(c.canSave()).toBeFalse();
    });

    it('Income sem categoria é válido', () => {
      expect(c.items()[1].categoryId).toBeNull();
      expect(c.canSave()).toBeTrue();
    });

    it('bloqueia valor <= 0', () => {
      c.updateItem(0, { amount: 0 });
      expect(c.canSave()).toBeFalse();
    });

    it('bloqueia descrição vazia', () => {
      c.updateItem(0, { description: '   ' });
      expect(c.canSave()).toBeFalse();
    });

    it('bloqueia descrição com mais de 200 caracteres', () => {
      c.updateItem(0, { description: 'a'.repeat(201) });
      expect(c.canSave()).toBeFalse();
    });

    it('aceita descrição com exatamente 200 caracteres', () => {
      c.updateItem(0, { description: 'a'.repeat(200) });
      expect(c.canSave()).toBeTrue();
    });

    it('bloqueia data futura', () => {
      const d = new Date(); d.setDate(d.getDate() + 5);
      const iso = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
      c.updateItem(0, { date: iso });
      expect(c.canSave()).toBeFalse();
    });

    it('bloqueia lista vazia', () => {
      c.removeItem(2); c.removeItem(1); c.removeItem(0);
      expect(c.canSave()).toBeFalse();
    });

    it('save não chama API quando inválido', () => {
      c.updateItem(0, { amount: -1 });
      c.save();
      expect(api.confirmStatementImport).not.toHaveBeenCalled();
    });
  });

  describe('save', () => {
    beforeEach(() => {
      loadPreview();
      c.updateItem(2, { categoryId: 'cat-2' });
    });

    it('envia payload com kind string, data yyyy-MM-dd e sourceType padrão', () => {
      c.save();
      const req = api.confirmStatementImport.calls.mostRecent().args[0] as any;
      expect(req.items.length).toBe(3);
      const exp = req.items[0];
      expect(exp.kind).toBe('Expense');
      expect(typeof exp.kind).toBe('string');
      expect(exp.date).toBe('2026-01-10');
      expect(exp.categoryId).toBe('cat-1');
      expect(exp.sourceType).toBe('Personal');
      expect(typeof exp.sourceType).toBe('string');
      expect(req.items[1].kind).toBe('Income');
      expect(req.items[1].date).toBe('2026-01-11');
    });

    it('exibe resumo do resultado em sucesso', () => {
      c.save();
      expect(c.result()).toEqual({ periodsCreated: 1, periodsReused: 2, expensesCreated: 2, incomesCreated: 1 });
      expect(c.saving()).toBeFalse();
    });

    it('exibe err.error.message em erro 400 do confirm e mantém itens', () => {
      api.confirmStatementImport.and.returnValue(
        throwError(() => ({ status: 400, error: { message: 'Data futura não permitida.' } })));
      c.save();
      expect(c.errorMessage()).toBe('Data futura não permitida.');
      expect(c.result()).toBeNull();
      expect(c.items().length).toBe(3);
      expect(c.saving()).toBeFalse();
    });
  });
});
