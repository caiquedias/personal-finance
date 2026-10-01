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

    it('exibe estado vazio quando o extrato não tem lançamentos', () => {
      api.previewStatementImport.and.returnValue(of({ items: [], discardedByDateCount: 0 } as any));
      c.onFileSelected(fileEvent(makeFile('e.pdf')));
      c.preview();
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('.empty-state')?.textContent).toContain('Nenhum lançamento encontrado');
    });

    it('estado vazio informa descartados pela data de início', () => {
      api.previewStatementImport.and.returnValue(of({ items: [], discardedByDateCount: 4 } as any));
      c.onFileSelected(fileEvent(makeFile('e.pdf')));
      c.preview();
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('.empty-state')?.textContent).toContain('4 lançamento(s) descartado(s)');
    });

    it('não exibe estado vazio antes do preview nem quando há itens', () => {
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('.empty-state')).toBeNull();
      loadPreview();
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('.empty-state')).toBeNull();
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

    it('canSave é false após save com sucesso e o resumo permanece', () => {
      c.save();
      expect(c.result()).not.toBeNull();
      expect(c.canSave()).toBeFalse();
    });

    it('segundo save após sucesso não dispara outro POST de confirm', () => {
      c.save();
      c.save();
      expect(api.confirmStatementImport).toHaveBeenCalledTimes(1);
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

    it('erro de save com itens mantém banner visível e não aplica estado err', () => {
      api.confirmStatementImport.and.returnValue(
        throwError(() => ({ status: 400, error: { message: 'Data futura não permitida.' } })));
      c.save();
      fixture.detectChanges();
      const el: HTMLElement = fixture.nativeElement;
      expect(el.querySelector('[role="alert"]')?.textContent).toContain('Data futura não permitida.');
      expect(el.querySelector('.has-error')).toBeNull();
      expect(el.querySelector('.pdf-preview-btn')?.textContent).not.toContain('Tentar novamente');
    });
  });

  // ---- Issue #443: casca visual (estados empty/err) e drag-and-drop ----
  describe('casca visual — DOM', () => {
    const el = () => fixture.nativeElement as HTMLElement;
    const previewBtn = () => el().querySelector('.pdf-preview-btn') as HTMLButtonElement;

    it('renderiza dropzone .pdf-dropzone sem arquivo e nenhuma .drop-zone', () => {
      expect(el().querySelector('.pdf-dropzone')).not.toBeNull();
      expect(el().querySelector('.drop-zone')).toBeNull();
    });

    it('não exibe card de arquivo sem arquivo selecionado', () => {
      expect(el().querySelector('.pdf-file-card')).toBeNull();
    });

    it('card do arquivo exibe o nome quando há arquivo selecionado', () => {
      c.onFileSelected(fileEvent(makeFile('meu-extrato.pdf')));
      fixture.detectChanges();
      expect(el().querySelector('.pdf-file-card')?.textContent).toContain('meu-extrato.pdf');
    });

    it('botão de preview fica desabilitado sem arquivo', () => {
      expect(previewBtn().disabled).toBeTrue();
      expect(previewBtn().textContent).toContain('Pré-visualizar');
    });

    it('botão de preview habilita com arquivo', () => {
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      fixture.detectChanges();
      expect(previewBtn().disabled).toBeFalse();
    });

    it('botão mostra "Processando..." e desabilita em loading', () => {
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      c.loading.set(true);
      fixture.detectChanges();
      expect(previewBtn().textContent).toContain('Processando...');
      expect(previewBtn().disabled).toBeTrue();
    });

    it('banner role="alert" exibe "Senha incorreta." após erro 400 de preview', () => {
      api.previewStatementImport.and.returnValue(
        throwError(() => ({ status: 400, error: { message: 'Senha incorreta.' } })));
      loadPreview();
      expect(el().querySelector('[role="alert"]')?.textContent).toContain('Senha incorreta.');
    });

    it('sem erro não há banner nem estado err', () => {
      expect(el().querySelector('[role="alert"]')).toBeNull();
      expect(el().querySelector('.has-error')).toBeNull();
    });

    it('estado err com arquivo: classe .has-error e botão "Tentar novamente"', () => {
      api.previewStatementImport.and.returnValue(
        throwError(() => ({ status: 400, error: { message: 'Senha incorreta.' } })));
      loadPreview();
      expect(el().querySelector('.has-error')).not.toBeNull();
      expect(previewBtn().textContent).toContain('Tentar novamente');
    });

    it('mantém campos de senha e data de início', () => {
      expect(el().querySelector('input[type="password"]')).not.toBeNull();
      expect(el().querySelector('input[type="date"]')).not.toBeNull();
    });
  });

  describe('drag-and-drop', () => {
    function dragEvent(files?: File[]): DragEvent {
      return {
        preventDefault: jasmine.createSpy('preventDefault'),
        dataTransfer: files ? { files } : null,
      } as unknown as DragEvent;
    }

    it('isDragging inicia false', () => {
      expect(c.isDragging()).toBeFalse();
    });

    it('onDragOver seta isDragging e chama preventDefault', () => {
      const ev = dragEvent();
      c.onDragOver(ev);
      expect(c.isDragging()).toBeTrue();
      expect(ev.preventDefault).toHaveBeenCalled();
    });

    it('onDragLeave volta isDragging para false', () => {
      c.onDragOver(dragEvent());
      c.onDragLeave(dragEvent());
      expect(c.isDragging()).toBeFalse();
    });

    it('classe dragging é aplicada na dropzone durante o arraste', () => {
      c.onDragOver(dragEvent());
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('.pdf-dropzone.dragging')).not.toBeNull();
      c.onDragLeave(dragEvent());
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('.pdf-dropzone.dragging')).toBeNull();
    });

    it('onDrop com PDF válido seta selectedFile, limpa erro e isDragging', () => {
      c.errorMessage.set('antigo');
      c.onDragOver(dragEvent());
      const ev = dragEvent([makeFile('solto.pdf')]);
      c.onDrop(ev);
      expect(ev.preventDefault).toHaveBeenCalled();
      expect(c.selectedFile()!.name).toBe('solto.pdf');
      expect(c.errorMessage()).toBeNull();
      expect(c.isDragging()).toBeFalse();
    });

    it('onDrop com não-PDF rejeita com a mesma mensagem de onFileSelected', () => {
      c.onDrop(dragEvent([makeFile('planilha.xlsx')]));
      expect(c.selectedFile()).toBeNull();
      expect(c.errorMessage()).toBe('Apenas arquivos .pdf são aceitos.');
      expect(c.isDragging()).toBeFalse();
    });

    it('onDrop com arquivo > 10 MB rejeita com a mesma mensagem de onFileSelected', () => {
      c.onDrop(dragEvent([makeFile('grande.pdf', 10 * 1024 * 1024 + 1)]));
      expect(c.selectedFile()).toBeNull();
      expect(c.errorMessage()).toBe('O arquivo excede o limite de 10 MB.');
    });

    it('onDrop sem arquivos é no-op (mantém arquivo atual) e zera isDragging', () => {
      c.onFileSelected(fileEvent(makeFile('atual.pdf')));
      c.onDragOver(dragEvent());
      c.onDrop(dragEvent([]));
      expect(c.selectedFile()!.name).toBe('atual.pdf');
      expect(c.errorMessage()).toBeNull();
      expect(c.isDragging()).toBeFalse();
    });

    it('onDrop sem dataTransfer não lança e é no-op', () => {
      expect(() => c.onDrop(dragEvent())).not.toThrow();
      expect(c.selectedFile()).toBeNull();
    });
  });
});
