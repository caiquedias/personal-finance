import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { StatementImportComponent } from './statement-import.component';
import { ApiService } from '../../../../core/services/api.service';
import { CategoryResponse, ConfirmStatementImportResult, StatementPreviewResult } from '../../../../core/models/models';

function makeFile(name: string, size = 1024): File {
  return new File([new Uint8Array(size)], name, { type: 'application/pdf' });
}
function fileEvent(file: File): Event {
  return { target: { files: [file], value: '' } } as unknown as Event;
}

const CATS: CategoryResponse[] = [
  { id: 'cat-1', name: 'Mercado', color: '#fff', icon: null, userId: null, isGlobal: true, isActive: true },
  { id: 'cat-2', name: 'Lazer',   color: '#000', icon: null, userId: null, isGlobal: true, isActive: true },
];

const PREVIEW: StatementPreviewResult = {
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
  let c: StatementImportComponent;
  let api: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<ApiService>('ApiService',
      ['getCategories', 'previewStatementImport', 'confirmStatementImport']);
    api.getCategories.and.returnValue(of(CATS));
    api.previewStatementImport.and.returnValue(of(PREVIEW));
    api.confirmStatementImport.and.returnValue(of({
      periodsCreated: 1, periodsReused: 2, expensesCreated: 2, incomesCreated: 1 }));

    await TestBed.configureTestingModule({
      imports: [StatementImportComponent],
      providers: [{ provide: ApiService, useValue: api }, provideRouter([])],
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
      api.previewStatementImport.and.returnValue(of({ items: [], discardedByDateCount: 0 }));
      c.onFileSelected(fileEvent(makeFile('e.pdf')));
      c.preview();
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('.empty-state')?.textContent).toContain('Nenhum lançamento encontrado');
    });

    it('estado vazio informa descartados pela data de início', () => {
      api.previewStatementImport.and.returnValue(of({ items: [], discardedByDateCount: 4 }));
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
      const req = api.confirmStatementImport.calls.mostRecent().args[0];
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

  // ---- Issue #444: state unificado, stepper, card de processamento e ticker ----
  describe('state() — máquina de estados unificada', () => {
    const el = () => fixture.nativeElement as HTMLElement;
    const errPreview = () => api.previewStatementImport.and.returnValue(
      throwError(() => ({ status: 400, error: { message: 'Senha incorreta.' } })));

    it('inicial: empty', () => {
      expect(c.state()).toBe('empty');
    });

    it('arquivo selecionado: empty', () => {
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      expect(c.state()).toBe('empty');
    });

    it('preview em voo: proc', () => {
      api.previewStatementImport.and.returnValue(new Subject<StatementPreviewResult>());
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      c.preview();
      expect(c.state()).toBe('proc');
    });

    it('preview ok com itens: preview', () => {
      loadPreview();
      expect(c.state()).toBe('preview');
    });

    it('preview ok sem itens: empty e .empty-state visível', () => {
      api.previewStatementImport.and.returnValue(of({ items: [], discardedByDateCount: 0 }));
      c.onFileSelected(fileEvent(makeFile('e.pdf')));
      c.preview();
      fixture.detectChanges();
      expect(c.state()).toBe('empty');
      expect(el().querySelector('.empty-state')).not.toBeNull();
    });

    it('erro de preview: err', () => {
      errPreview();
      loadPreview();
      expect(c.state()).toBe('err');
    });

    it('save em voo: proc', () => {
      loadPreview();
      c.updateItem(2, { categoryId: 'cat-2' });
      api.confirmStatementImport.and.returnValue(new Subject<ConfirmStatementImportResult>());
      c.save();
      expect(c.state()).toBe('proc');
    });

    it('save ok: done', () => {
      loadPreview();
      c.updateItem(2, { categoryId: 'cat-2' });
      c.save();
      expect(c.state()).toBe('done');
    });

    it('erro de save com itens mantidos: preview, banner role="alert" e sem .has-error', () => {
      loadPreview();
      c.updateItem(2, { categoryId: 'cat-2' });
      api.confirmStatementImport.and.returnValue(
        throwError(() => ({ status: 400, error: { message: 'Data futura não permitida.' } })));
      c.save();
      fixture.detectChanges();
      expect(c.state()).toBe('preview');
      expect(el().querySelector('[role="alert"]')?.textContent).toContain('Data futura não permitida.');
      expect(el().querySelector('.has-error')).toBeNull();
      expect(el().querySelectorAll('.review-table tbody tr').length).toBe(3);
    });

    it('arquivo rejeitado: err, com dropzone e campos visíveis', () => {
      c.onFileSelected(fileEvent(makeFile('planilha.xlsx')));
      fixture.detectChanges();
      expect(c.state()).toBe('err');
      expect(el().querySelector('.pdf-dropzone')).not.toBeNull();
      expect(el().querySelector('input[type="password"]')).not.toBeNull();
      expect(el().querySelector('input[type="date"]')).not.toBeNull();
    });

    it('drop inválido: err', () => {
      c.onDrop({ preventDefault: () => {}, dataTransfer: { files: [makeFile('x.xlsx')] } } as unknown as DragEvent);
      expect(c.state()).toBe('err');
    });

    it('falha de getCategories: err, com dropzone e campos visíveis', async () => {
      api.getCategories.and.returnValue(throwError(() => new Error('falha')));
      const f2 = TestBed.createComponent(StatementImportComponent);
      f2.detectChanges();
      const el2 = f2.nativeElement as HTMLElement;
      expect(f2.componentInstance.state()).toBe('err');
      expect(el2.querySelector('.pdf-dropzone')).not.toBeNull();
      expect(el2.querySelector('input[type="password"]')).not.toBeNull();
    });

    it('precedência: done vence errorMessage', () => {
      c.result.set({ periodsCreated: 1, periodsReused: 0, expensesCreated: 1, incomesCreated: 0 });
      c.errorMessage.set('qualquer erro');
      expect(c.state()).toBe('done');
    });

    it('precedência: proc vence items > 0', () => {
      loadPreview();
      c.saving.set(true);
      expect(c.state()).toBe('proc');
    });

    it('precedência: proc vence done', () => {
      c.result.set({ periodsCreated: 1, periodsReused: 0, expensesCreated: 1, incomesCreated: 0 });
      c.loading.set(true);
      expect(c.state()).toBe('proc');
    });

    it('precedência: err vence preview apenas se não há itens (itens + erro => preview)', () => {
      loadPreview();
      c.errorMessage.set('erro');
      expect(c.state()).toBe('preview');
    });
  });

  describe('stepper e card de processamento — DOM', () => {
    const el = () => fixture.nativeElement as HTMLElement;
    const activeStep = () => el().querySelector('.stepper .step.active')?.textContent ?? '';

    it('renderiza 3 passos: Arquivo, Revisar lançamentos, Importar', () => {
      const steps = Array.from(el().querySelectorAll('.stepper .step')).map(s => s.textContent);
      expect(steps.length).toBe(3);
      expect(steps[0]).toContain('Arquivo');
      expect(steps[1]).toContain('Revisar lançamentos');
      expect(steps[2]).toContain('Importar');
    });

    it('passo ativo: Arquivo em empty', () => {
      expect(el().querySelectorAll('.stepper .step.active').length).toBe(1);
      expect(activeStep()).toContain('Arquivo');
    });

    it('passo ativo: Arquivo em err', () => {
      c.onFileSelected(fileEvent(makeFile('x.xlsx')));
      fixture.detectChanges();
      expect(activeStep()).toContain('Arquivo');
    });

    it('passo ativo: Revisar em preview', () => {
      loadPreview();
      expect(el().querySelectorAll('.stepper .step.active').length).toBe(1);
      expect(activeStep()).toContain('Revisar lançamentos');
    });

    it('passo ativo: Importar em proc do save', () => {
      loadPreview();
      c.updateItem(2, { categoryId: 'cat-2' });
      api.confirmStatementImport.and.returnValue(new Subject<ConfirmStatementImportResult>());
      c.save();
      fixture.detectChanges();
      expect(activeStep()).toContain('Importar');
    });

    it('passo ativo: Importar em done', () => {
      loadPreview();
      c.updateItem(2, { categoryId: 'cat-2' });
      c.save();
      fixture.detectChanges();
      expect(activeStep()).toContain('Importar');
    });

    it('sem card de processamento fora de proc', () => {
      expect(el().querySelector('.proc-card')).toBeNull();
      loadPreview();
      expect(el().querySelector('.proc-card')).toBeNull();
    });

    it('card "Lendo o extrato..." durante o preview', () => {
      api.previewStatementImport.and.returnValue(new Subject<StatementPreviewResult>());
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      c.preview();
      fixture.detectChanges();
      expect(el().querySelector('.proc-card')?.textContent).toContain('Lendo o extrato...');
    });

    it('card "Importando lançamentos..." durante o save', () => {
      loadPreview();
      c.updateItem(2, { categoryId: 'cat-2' });
      api.confirmStatementImport.and.returnValue(new Subject<ConfirmStatementImportResult>());
      c.save();
      fixture.detectChanges();
      expect(el().querySelector('.proc-card')?.textContent).toContain('Importando lançamentos...');
    });

    it('card contém checklist cosmético e percentual', () => {
      api.previewStatementImport.and.returnValue(new Subject<StatementPreviewResult>());
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      c.preview();
      fixture.detectChanges();
      const txt = el().querySelector('.proc-card')?.textContent ?? '';
      expect(txt).toContain('Arquivo enviado');
      expect(txt).toContain('PDF lido');
      expect(txt).toContain('Verificando duplicados');
      expect(txt).toContain('%');
    });

    it('contrato #443: botão preview segue visível com "Processando..." e desabilitado durante o preview', () => {
      api.previewStatementImport.and.returnValue(new Subject<StatementPreviewResult>());
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      c.preview();
      fixture.detectChanges();
      const btn = el().querySelector('.pdf-preview-btn') as HTMLButtonElement;
      expect(btn.textContent).toContain('Processando...');
      expect(btn.disabled).toBeTrue();
    });
  });

  describe('ticker de progresso simulado', () => {
    it('preview: avança em voo, nunca passa de 90 e salta para 100 no sucesso', fakeAsync(() => {
      const subj = new Subject<StatementPreviewResult>();
      api.previewStatementImport.and.returnValue(subj);
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      c.preview();
      expect(c.simulatedProgress()).toBe(0);
      tick(1000);
      const early = c.simulatedProgress();
      expect(early).toBeGreaterThan(0);
      expect(early).toBeLessThanOrEqual(90);
      tick(120000);
      expect(c.simulatedProgress()).toBeGreaterThanOrEqual(early);
      expect(c.simulatedProgress()).toBeLessThanOrEqual(90);
      subj.next(PREVIEW);
      subj.complete();
      expect(c.simulatedProgress()).toBe(100);
      tick(120000);
      expect(c.simulatedProgress()).toBe(100);
    }));

    it('preview: salta para 100 também em erro', fakeAsync(() => {
      const subj = new Subject<StatementPreviewResult>();
      api.previewStatementImport.and.returnValue(subj);
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      c.preview();
      tick(2000);
      subj.error({ status: 400, error: { message: 'Senha incorreta.' } });
      expect(c.simulatedProgress()).toBe(100);
      tick(120000);
      expect(c.simulatedProgress()).toBe(100);
    }));

    it('save: avança em voo até 90 e salta para 100 no sucesso', fakeAsync(() => {
      loadPreview();
      c.updateItem(2, { categoryId: 'cat-2' });
      const subj = new Subject<ConfirmStatementImportResult>();
      api.confirmStatementImport.and.returnValue(subj);
      c.save();
      expect(c.simulatedProgress()).toBe(0);
      tick(1000);
      expect(c.simulatedProgress()).toBeGreaterThan(0);
      tick(120000);
      expect(c.simulatedProgress()).toBeLessThanOrEqual(90);
      subj.next({ periodsCreated: 1, periodsReused: 0, expensesCreated: 1, incomesCreated: 0 });
      subj.complete();
      expect(c.simulatedProgress()).toBe(100);
    }));

    it('save: salta para 100 em erro e mantém itens', fakeAsync(() => {
      loadPreview();
      c.updateItem(2, { categoryId: 'cat-2' });
      const subj = new Subject<ConfirmStatementImportResult>();
      api.confirmStatementImport.and.returnValue(subj);
      c.save();
      tick(2000);
      subj.error({ status: 500, error: { message: 'Falha.' } });
      expect(c.simulatedProgress()).toBe(100);
      expect(c.items().length).toBe(3);
      tick(120000);
      expect(c.simulatedProgress()).toBe(100);
    }));

    it('zera a cada novo preview()', fakeAsync(() => {
      const first = new Subject<StatementPreviewResult>();
      api.previewStatementImport.and.returnValue(first);
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      c.preview();
      tick(5000);
      first.next(PREVIEW);
      first.complete();
      expect(c.simulatedProgress()).toBe(100);

      const second = new Subject<StatementPreviewResult>();
      api.previewStatementImport.and.returnValue(second);
      c.preview();
      expect(c.simulatedProgress()).toBe(0);
      second.next(PREVIEW);
      second.complete();
    }));

    it('zera a cada novo save()', fakeAsync(() => {
      loadPreview();
      c.updateItem(2, { categoryId: 'cat-2' });
      const first = new Subject<ConfirmStatementImportResult>();
      api.confirmStatementImport.and.returnValue(first);
      c.save();
      tick(5000);
      first.error({ status: 500, error: { message: 'Falha.' } });
      expect(c.simulatedProgress()).toBe(100);

      const second = new Subject<ConfirmStatementImportResult>();
      api.confirmStatementImport.and.returnValue(second);
      c.save();
      expect(c.simulatedProgress()).toBe(0);
      second.error({ status: 500, error: { message: 'Falha.' } });
    }));

    it('novo preview() em voo cancela o ticker anterior (progresso não acelera)', fakeAsync(() => {
      const first = new Subject<StatementPreviewResult>();
      api.previewStatementImport.and.returnValue(first);
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      c.preview();
      tick(100000);
      const second = new Subject<StatementPreviewResult>();
      api.previewStatementImport.and.returnValue(second);
      c.preview();
      expect(c.simulatedProgress()).toBe(0);
      tick(1000);
      // um único ticker ativo: após 1s o progresso fica abaixo do teto
      expect(c.simulatedProgress()).toBeLessThan(90);
      second.next(PREVIEW);
      second.complete();
      first.complete();
    }));

    it('sem timer pendente após fixture.destroy() com request em voo', fakeAsync(() => {
      api.previewStatementImport.and.returnValue(new Subject<StatementPreviewResult>());
      c.onFileSelected(fileEvent(makeFile('extrato.pdf')));
      c.preview();
      tick(1000);
      fixture.destroy();
      // fakeAsync falha o teste se restar timer periódico/pendente
    }));
  });

  describe('cards de resumo (#445)', () => {
    // Contrato dos novos computeds (ainda inexistentes no componente)
    interface SummaryApi {
      expenseItems(): unknown[];
      incomeItems(): unknown[];
      expenseSum(): number;
      incomeSum(): number;
      pendingCount(): number;
      warnCount(): number;
      attentionCount(): number;
      discardedSinceLabel(): string;
    }
    const s = () => c as unknown as SummaryApi;
    const el = () => fixture.nativeElement as HTMLElement;
    const cardValue = (key: string) =>
      el().querySelector(`.summary-card[data-card="${key}"] .summary-card-value`)?.textContent?.trim();

    describe('computeds', () => {
      it('valores iniciais sobre o PREVIEW', () => {
        loadPreview();
        expect(s().expenseItems().length).toBe(2);
        expect(s().incomeItems().length).toBe(1);
        expect(s().expenseSum()).toBe(150);
        expect(s().incomeSum()).toBe(5000);
        expect(s().pendingCount()).toBe(1);
        expect(s().warnCount()).toBe(1);
        expect(s().attentionCount()).toBe(1);
      });

      it('sem itens: tudo zerado', () => {
        expect(s().expenseSum()).toBe(0);
        expect(s().incomeSum()).toBe(0);
        expect(s().pendingCount()).toBe(0);
        expect(s().attentionCount()).toBe(0);
      });

      it('updateItem de amount recalcula a soma', () => {
        loadPreview();
        c.updateItem(0, { amount: 300 });
        expect(s().expenseSum()).toBe(350);
      });

      it('amount numérico soma sem concatenar', () => {
        loadPreview();
        c.updateItem(0, { amount: 0.1 });
        c.updateItem(2, { amount: 0.2 });
        expect(s().expenseSum()).toBeCloseTo(0.3, 5);
      });

      it('updateItem de categoryId zera pendentes', () => {
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-1' });
        expect(s().pendingCount()).toBe(0);
      });

      it('remover categoria de Expense aumenta pendentes', () => {
        loadPreview();
        c.updateItem(0, { categoryId: null });
        expect(s().pendingCount()).toBe(2);
      });

      it('Income sem categoria não conta como pendente', () => {
        loadPreview();
        expect(c.items()[1].categoryId).toBeNull();
        expect(s().pendingCount()).toBe(1);
      });

      it('Expense pendente -> Income sai de pendentes e das despesas', () => {
        loadPreview();
        c.updateItem(2, { kind: 'Income' });
        expect(s().pendingCount()).toBe(0);
        expect(s().expenseItems().length).toBe(1);
        expect(s().incomeItems().length).toBe(2);
        expect(s().expenseSum()).toBe(100);
        expect(s().incomeSum()).toBe(5050);
      });

      it('removeItem recalcula tudo', () => {
        loadPreview();
        c.removeItem(2);
        expect(s().expenseItems().length).toBe(1);
        expect(s().expenseSum()).toBe(100);
        expect(s().pendingCount()).toBe(0);
        expect(s().warnCount()).toBe(0);
        expect(s().attentionCount()).toBe(0);
      });

      it('attentionCount é união: item pendente e com aviso conta 1', () => {
        loadPreview();
        expect(s().pendingCount() + s().warnCount()).toBe(2);
        expect(s().attentionCount()).toBe(1);
      });

      it('attentionCount soma quando pendentes e avisos são disjuntos', () => {
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-1' }); // item 2 só com aviso
        c.updateItem(0, { categoryId: null });    // item 0 só pendente
        expect(s().pendingCount()).toBe(1);
        expect(s().warnCount()).toBe(1);
        expect(s().attentionCount()).toBe(2);
      });

      it('só duplicate conta como aviso', () => {
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-1', isLikelyInternalTransfer: false });
        expect(s().warnCount()).toBe(1);
        expect(s().attentionCount()).toBe(1);
      });

      it('só transferência interna conta como aviso', () => {
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-1', isLikelyDuplicate: false });
        expect(s().warnCount()).toBe(1);
        expect(s().attentionCount()).toBe(1);
      });

      it('discardedSinceLabel formata fromDate sem deslocar o dia', () => {
        c.fromDate.set('2026-01-01');
        expect(s().discardedSinceLabel()).toBe('01/01/2026');
      });

      it('discardedSinceLabel com fromDate vazio não exibe Invalid Date', () => {
        c.fromDate.set('');
        expect(s().discardedSinceLabel()).toBeDefined();
        expect(s().discardedSinceLabel()).not.toContain('Invalid');
        expect(s().discardedSinceLabel()).not.toContain('NaN');
      });
    });

    describe('DOM', () => {
      it('renderiza 4 cards de resumo', () => {
        loadPreview();
        expect(el().querySelectorAll('.summary-card').length).toBe(4);
      });

      it('cards refletem contagens do preview', () => {
        loadPreview();
        expect(cardValue('total')).toBe('3');
        expect(cardValue('attention')).toBe('1');
        expect(el().querySelector('.summary-card[data-card="expense"]')?.textContent).toContain('2');
        expect(el().querySelector('.summary-card[data-card="income"]')?.textContent).toContain('1');
      });

      it('cards atualizam em tempo real ao remover linha', () => {
        loadPreview();
        c.removeItem(2);
        fixture.detectChanges();
        expect(cardValue('total')).toBe('2');
        expect(cardValue('attention')).toBe('0');
      });

      it('não renderiza cards sem itens', () => {
        expect(el().querySelectorAll('.summary-card').length).toBe(0);
      });

      it('select de Categoria tem classe de pendente só em Expense sem categoria', () => {
        loadPreview();
        const rows = el().querySelectorAll('.review-table tbody tr');
        expect(rows[0].querySelector('select.select-pending')).toBeNull();      // Expense com categoria
        expect(rows[1].querySelector('select.select-pending')).toBeNull();      // Income sem categoria
        expect(rows[2].querySelector('select.select-pending')).not.toBeNull();  // Expense sem categoria
      });

      it('classe de pendente some ao escolher categoria', () => {
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-1' });
        fixture.detectChanges();
        expect(el().querySelector('select.select-pending')).toBeNull();
      });

      it('pills de Avisos exibem textos esperados', () => {
        loadPreview();
        const row = el().querySelectorAll('.review-table tbody tr')[2];
        expect(row.textContent).toContain('Transferência interna');
        expect(row.textContent).toContain('Possível duplicado');
      });

      it('input de valor restilizado ainda aciona updateItem e canSave reage', async () => {
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-1' });
        expect(c.canSave()).toBeTrue();
        const input = el().querySelectorAll('.review-table tbody tr')[0]
          .querySelector('input[type="number"]') as HTMLInputElement;
        input.value = '0';
        input.dispatchEvent(new Event('input'));
        fixture.detectChanges();
        await fixture.whenStable();
        expect(c.items()[0].amount).toBe(0);
        expect(typeof c.items()[0].amount).toBe('number');
        expect(c.canSave()).toBeFalse();
      });

      it('input de valor atualiza soma do card de despesas', async () => {
        loadPreview();
        const input = el().querySelectorAll('.review-table tbody tr')[0]
          .querySelector('input[type="number"]') as HTMLInputElement;
        input.value = '200';
        input.dispatchEvent(new Event('input'));
        fixture.detectChanges();
        await fixture.whenStable();
        expect(s().expenseSum()).toBe(250);
      });
    });
  });

  // ---- Issue #446: barra de ação fixa + tela de sucesso ----
  describe('barra de ação e tela de sucesso (#446)', () => {
    // Contrato novo (ainda inexistente no componente)
    interface ActionApi {
      targetPeriodLabel(): string;
      importAnother(): void;
    }
    const a = () => c as unknown as ActionApi;
    const el = () => fixture.nativeElement as HTMLElement;
    const setDates = (...dates: string[]) =>
      dates.forEach((d, i) => c.updateItem(i, { date: d }));

    describe('targetPeriodLabel', () => {
      it('lista vazia retorna texto vazio', () => {
        expect(a().targetPeriodLabel()).toBe('');
      });

      it('mesmo mês e mesma quinzena: período único (01/2026, 1ª quinzena)', () => {
        loadPreview(); // 10, 11, 12/01
        const label = a().targetPeriodLabel();
        expect(label).toContain('01/2026');
        expect(label).toContain('1ª quinzena');
        expect(label).not.toMatch(/\d+ períodos/);
      });

      it('dia 15 é primeira quinzena', () => {
        loadPreview();
        setDates('2026-01-15', '2026-01-15', '2026-01-15');
        expect(a().targetPeriodLabel()).toContain('1ª quinzena');
        expect(a().targetPeriodLabel()).not.toMatch(/\d+ períodos/);
      });

      it('dia 16 é segunda quinzena', () => {
        loadPreview();
        setDates('2026-01-16', '2026-01-16', '2026-01-16');
        const label = a().targetPeriodLabel();
        expect(label).toContain('2ª quinzena');
        expect(label).not.toContain('1ª quinzena');
      });

      it('virada 15/16 no mesmo mês conta 2 períodos', () => {
        loadPreview();
        setDates('2026-01-15', '2026-01-16', '2026-01-16');
        expect(a().targetPeriodLabel()).toMatch(/^2 períodos/);
      });

      it('meses distintos: "N períodos"', () => {
        loadPreview();
        setDates('2026-01-10', '2026-02-10', '2026-03-10');
        const label = a().targetPeriodLabel();
        expect(label).toMatch(/^3 períodos/);
        expect(label).toContain('01/2026');
        expect(label).toContain('02/2026');
        expect(label).toContain('03/2026');
      });

      it('virada de ano não desloca data (31/12 e 01/01)', () => {
        loadPreview();
        setDates('2025-12-31', '2026-01-01', '2026-01-01');
        const label = a().targetPeriodLabel();
        expect(label).toMatch(/^2 períodos/);
        expect(label).toContain('12/2025');
        expect(label).toContain('01/2026');
      });

      it('recalcula após updateItem de data', () => {
        loadPreview();
        expect(a().targetPeriodLabel()).not.toMatch(/\d+ períodos/);
        c.updateItem(0, { date: '2026-02-20' });
        expect(a().targetPeriodLabel()).toMatch(/^2 períodos/);
      });

      it('recalcula após removeItem', () => {
        loadPreview();
        c.updateItem(0, { date: '2026-02-20' });
        expect(a().targetPeriodLabel()).toMatch(/^2 períodos/);
        c.removeItem(0);
        expect(a().targetPeriodLabel()).not.toMatch(/\d+ períodos/);
        expect(a().targetPeriodLabel()).toContain('01/2026');
      });

      it('data inválida/vazia não gera Invalid Date nem NaN', () => {
        loadPreview();
        c.updateItem(0, { date: '' });
        expect(a().targetPeriodLabel()).not.toContain('Invalid');
        expect(a().targetPeriodLabel()).not.toContain('NaN');
      });
    });

    describe('barra de ação fixa — DOM', () => {
      it('não renderiza sem itens (empty)', () => {
        expect(el().querySelector('.action-bar')).toBeNull();
      });

      it('renderiza com itens em preview e exibe o período de destino', () => {
        loadPreview();
        const bar = el().querySelector('.action-bar');
        expect(bar).not.toBeNull();
        expect(bar!.textContent).toContain('01/2026');
      });

      it('botão exibe "Importar N lançamentos" ao vivo', () => {
        loadPreview();
        const btn = () => el().querySelector('.action-bar button.btn-primary') as HTMLButtonElement;
        expect(btn().textContent).toContain('Importar 3 lançamentos');
        c.removeItem(2);
        fixture.detectChanges();
        expect(btn().textContent).toContain('Importar 2 lançamentos');
      });

      it('mostra "N sem categoria" e botão desabilitado enquanto há pendentes', () => {
        loadPreview();
        const bar = el().querySelector('.action-bar') as HTMLElement;
        expect(bar.textContent).toContain('1 sem categoria');
        expect((bar.querySelector('button.btn-primary') as HTMLButtonElement).disabled).toBeTrue();
      });

      it('sem pendentes: botão habilitado e sem aviso de sem categoria', () => {
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-2' });
        fixture.detectChanges();
        const bar = el().querySelector('.action-bar') as HTMLElement;
        expect(bar.textContent).not.toContain('sem categoria');
        expect((bar.querySelector('button.btn-primary') as HTMLButtonElement).disabled).toBeFalse();
      });

      it('clique no botão chama save() uma vez com payload sem campo novo', () => {
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-2' });
        fixture.detectChanges();
        (el().querySelector('.action-bar button.btn-primary') as HTMLButtonElement).click();
        expect(api.confirmStatementImport).toHaveBeenCalledTimes(1);
        const req = api.confirmStatementImport.calls.mostRecent().args[0];
        expect(Object.keys(req)).toEqual(['items']);
        expect(Object.keys(req.items[0]).sort()).toEqual(
          ['amount', 'categoryId', 'date', 'description', 'kind', 'sourceType']);
      });

      it('duplo clique não dispara segundo POST', () => {
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-2' });
        fixture.detectChanges();
        const btn = el().querySelector('.action-bar button.btn-primary') as HTMLButtonElement;
        btn.click();
        fixture.detectChanges();
        el().querySelector<HTMLButtonElement>('.action-bar button.btn-primary')?.click();
        c.save();
        expect(api.confirmStatementImport).toHaveBeenCalledTimes(1);
      });

      it('erro no save mantém barra e itens para nova tentativa', () => {
        api.confirmStatementImport.and.returnValue(
          throwError(() => ({ status: 500, error: { message: 'Falha' } })));
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-2' });
        c.save();
        fixture.detectChanges();
        expect(c.items().length).toBe(3);
        expect(el().querySelector('.action-bar')).not.toBeNull();
        expect(c.canSave()).toBeTrue();
      });

      it('barra some em done', () => {
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-2' });
        c.save();
        fixture.detectChanges();
        expect(c.state()).toBe('done');
        expect(el().querySelector('.action-bar')).toBeNull();
      });

      it('barra some em proc (saving)', () => {
        loadPreview();
        c.saving.set(true);
        fixture.detectChanges();
        expect(c.state()).toBe('proc');
        expect(el().querySelector('.action-bar')).toBeNull();
      });
    });

    describe('tela de sucesso — DOM', () => {
      const doSave = () => {
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-2' });
        c.save();
        fixture.detectChanges();
      };
      const card = (k: string) =>
        el().querySelector(`.success-card[data-card="${k}"] .success-card-value`)?.textContent?.trim();
      const navTarget = (action: string) => {
        const router = TestBed.inject(Router);
        const nav = spyOn(router, 'navigateByUrl').and.resolveTo(true);
        const navigate = spyOn(router, 'navigate').and.resolveTo(true);
        el().querySelector<HTMLElement>(`[data-action="${action}"]`)!.click();
        return nav.calls.count() ? String(nav.calls.mostRecent().args[0])
                                 : JSON.stringify(navigate.calls.mostRecent()?.args[0]);
      };

      it('renderiza tela de sucesso com role="status" e 3 cards', () => {
        doSave();
        expect(el().querySelector('.success-screen[role="status"]')).not.toBeNull();
        expect(el().querySelectorAll('.success-card').length).toBe(3);
      });

      it('exibe totais corretos do resultado', () => {
        doSave();
        expect(card('expenses')).toBe('2');
        expect(card('incomes')).toBe('1');
        const periods = el().querySelector('.success-card[data-card="periods"]')!.textContent!;
        expect(periods).toContain('1');
        expect(periods).toContain('2');
      });

      it('não exibe mais o bloco .summary antigo nem a tabela', () => {
        doSave();
        expect(el().querySelector('.summary')).toBeNull();
        expect(el().querySelector('.review-table')).toBeNull();
      });

      it('exibe as 3 ações', () => {
        doSave();
        expect(el().querySelector('[data-action="view-period"]')).not.toBeNull();
        expect(el().querySelector('[data-action="view-expenses"]')).not.toBeNull();
        expect(el().querySelector('[data-action="import-another"]')).not.toBeNull();
      });

      it('"Ver período" navega para /periods', () => {
        doSave();
        expect(navTarget('view-period')).toContain('/periods');
      });

      it('"Ver despesas" navega para /expenses', () => {
        doSave();
        expect(navTarget('view-expenses')).toContain('/expenses');
      });

      it('"Importar outro extrato" reseta tudo e volta a empty', () => {
        doSave();
        c.errorMessage.set('resíduo');
        el().querySelector<HTMLElement>('[data-action="import-another"]')!.click();
        fixture.detectChanges();
        expect(c.result()).toBeNull();
        expect(c.items().length).toBe(0);
        expect(c.selectedFile()).toBeNull();
        expect(c.previewed()).toBeFalse();
        expect(c.discardedCount()).toBe(0);
        expect(c.errorMessage()).toBeNull();
        expect(c.simulatedProgress()).toBe(0);
        expect(c.state()).toBe('empty');
        expect(el().querySelector('.empty-state')).not.toBeNull();
        expect(el().querySelector('.success-screen')).toBeNull();
        expect(el().querySelector('.action-bar')).toBeNull();
      });

      it('importAnother() via método também reseta o estado', () => {
        doSave();
        a().importAnother();
        expect(c.state()).toBe('empty');
        expect(c.result()).toBeNull();
      });

      it('após reset é possível novo preview e save (novo POST)', () => {
        doSave();
        a().importAnother();
        loadPreview();
        c.updateItem(2, { categoryId: 'cat-2' });
        c.save();
        expect(api.confirmStatementImport).toHaveBeenCalledTimes(2);
      });
    });
  });
});
