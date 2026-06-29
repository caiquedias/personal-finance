import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { Router, RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { PurgeComponent } from './purge.component';
import { ApiService } from '../../../../core/services/api.service';
import { EligiblePeriodResponse, PurgeResultResponse, PurgeRecordResponse } from '../../../../core/models/models';

// ── fixtures ─────────────────────────────────────────────────────────────────

const PERIOD_1: EligiblePeriodResponse = {
  periodId:     'p-1',
  year:         2024,
  month:        3,
  totalIncome:  3000,
  totalExpense: 2500,
  itemCount:    18,
};

const PERIOD_2: EligiblePeriodResponse = {
  periodId:     'p-2',
  year:         2024,
  month:        4,
  totalIncome:  4000,
  totalExpense: 3000,
  itemCount:    22,
};

const PURGE_RESULT: PurgeResultResponse = {
  periodId:         'p-1',
  estimatedSpaceKb: 128,
};

const RECORD_1: PurgeRecordResponse = {
  id:           'r-1',
  year:         2024,
  month:        1,
  purgedAt:     '2024-02-10T12:00:00Z',
  totalIncome:  5000,
  totalExpense: 4200,
  itemCount:    30,
};

const RECORD_2: PurgeRecordResponse = {
  id:           'r-2',
  year:         2023,
  month:        12,
  purgedAt:     '2024-01-05T08:30:00Z',
  totalIncome:  6000,
  totalExpense: 5100,
  itemCount:    45,
};

// ── extended spy type (inclui métodos MOD-03 ainda não existentes no ApiService real) ──

type ApiServiceSpy = jasmine.SpyObj<ApiService> & {
  getPurgeRecords:  jasmine.Spy;
  deletePurgeRecord: jasmine.Spy;
};

// ── suite ─────────────────────────────────────────────────────────────────────

describe('PurgeComponent', () => {
  let fixture: ComponentFixture<PurgeComponent>;
  let component: PurgeComponent;
  let apiSpy: ApiServiceSpy;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('ApiService', [
      'getEligiblePeriods',
      'exportPurgeCsv',
      'executePurge',
      'getPurgeRecords',
      'deletePurgeRecord',
    ]) as ApiServiceSpy;
    apiSpy.getEligiblePeriods.and.returnValue(of([PERIOD_1, PERIOD_2]));
    apiSpy.getPurgeRecords.and.returnValue(of([RECORD_1, RECORD_2]));

    await TestBed.configureTestingModule({
      imports: [PurgeComponent, RouterModule.forRoot([]), NoopAnimationsModule],
      providers: [{ provide: ApiService, useValue: apiSpy }],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(PurgeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── criação ────────────────────────────────────────────────────────────────

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  // ── listagem de períodos elegíveis ─────────────────────────────────────────

  describe('ngOnInit — carrega períodos elegíveis', () => {
    it('carrega períodos na inicialização', fakeAsync(() => {
      expect(apiSpy.getEligiblePeriods).toHaveBeenCalled();
      tick();
      expect(component.eligiblePeriods().length).toBe(2);
    }));

    it('contém o período correto com totalIncome, totalExpense e itemCount', fakeAsync(() => {
      tick();
      const p = component.eligiblePeriods()[0];
      expect(p.periodId).toBe('p-1');
      expect(p.totalIncome).toBe(3000);
      expect(p.totalExpense).toBe(2500);
      expect(p.itemCount).toBe(18);
    }));

    it('define apiError quando getEligiblePeriods falha', fakeAsync(() => {
      apiSpy.getEligiblePeriods.and.returnValue(
        throwError(() => ({ error: { message: 'Erro ao carregar.' } }))
      );
      component.ngOnInit();
      tick();
      expect(component.apiError()).toBeTruthy();
    }));
  });

  // ── download CSV / blob ────────────────────────────────────────────────────

  describe('downloadCsv()', () => {
    it('chama exportPurgeCsv com o periodId correto', fakeAsync(() => {
      const blob = new Blob(['csv'], { type: 'text/csv' });
      apiSpy.exportPurgeCsv.and.returnValue(of(blob));

      component.downloadCsv(PERIOD_1);
      tick();

      expect(apiSpy.exportPurgeCsv).toHaveBeenCalledWith('p-1');
    }));

    it('confirmação permanece desabilitada antes do download', () => {
      // Sem chamar downloadCsv, purgeConfirmed deve ser false
      expect(component.purgeConfirmed()).toBeFalse();
    });

    it('define downloadError quando exportPurgeCsv falha e purgeConfirmed permanece false', fakeAsync(() => {
      apiSpy.exportPurgeCsv.and.returnValue(
        throwError(() => ({ error: { message: 'Falha no download.' } }))
      );
      component.downloadCsv(PERIOD_1);
      tick();
      expect(component.purgeConfirmed()).toBeFalse();
    }));

    it('should create an anchor element and trigger download', fakeAsync(() => {
      // Blob retornado pela API
      const blob = new Blob(['csv,data'], { type: 'text/csv' });
      apiSpy.exportPurgeCsv.and.returnValue(of(blob));

      // Spy em URL.createObjectURL e URL.revokeObjectURL
      const fakeUrl = 'blob:fake-url';
      spyOn(URL, 'createObjectURL').and.returnValue(fakeUrl);
      spyOn(URL, 'revokeObjectURL');

      // Cria um <a> falso para capturar a chamada a document.createElement
      const fakeAnchor = document.createElement('a');
      spyOn(fakeAnchor, 'click');
      spyOn(document, 'createElement').and.callFake((tag: string) => {
        if (tag === 'a') return fakeAnchor;
        return document.createElement(tag);
      });

      component.downloadCsv(PERIOD_1);
      tick();

      // createObjectURL deve ter recebido o blob
      expect(URL.createObjectURL).toHaveBeenCalledWith(blob);

      // O elemento <a> deve ter href apontando para a URL do blob
      expect(fakeAnchor.href).toBe(fakeUrl);

      // O atributo download deve conter o periodId ou extensão .csv
      const downloadAttr = fakeAnchor.getAttribute('download') ?? '';
      expect(downloadAttr.length).toBeGreaterThan(0);
      const hasCsvOrPeriodId =
        downloadAttr.includes('.csv') || downloadAttr.includes(PERIOD_1.periodId);
      expect(hasCsvOrPeriodId).toBeTrue();

      // O clique deve ter sido acionado programaticamente
      expect(fakeAnchor.click).toHaveBeenCalled();

      // A URL temporária deve ser revogada após o download
      expect(URL.revokeObjectURL).toHaveBeenCalledWith(fakeUrl);
    }));
  });

  // ── modal de confirmação ───────────────────────────────────────────────────

  describe('openConfirmModal()', () => {
    it('abre o modal de confirmação com o período selecionado', () => {
      component.openConfirmModal(PERIOD_1);
      expect(component.confirmModalOpen()).toBeTrue();
      expect(component.selectedPeriod()?.periodId).toBe('p-1');
    });

    it('reseta purgeConfirmed ao abrir novo modal', () => {
      component.purgeConfirmed.set(true);
      component.openConfirmModal(PERIOD_1);
      expect(component.purgeConfirmed()).toBeFalse();
    });
  });

  describe('closeConfirmModal()', () => {
    it('fecha o modal sem executar o expurgo', fakeAsync(() => {
      component.openConfirmModal(PERIOD_1);
      component.closeConfirmModal();

      expect(component.confirmModalOpen()).toBeFalse();
      expect(apiSpy.executePurge).not.toHaveBeenCalled();
    }));

    it('POST não é chamado ao fechar modal sem confirmar', fakeAsync(() => {
      apiSpy.exportPurgeCsv.and.returnValue(of(new Blob(['csv'])));

      component.openConfirmModal(PERIOD_1);
      component.downloadCsv(PERIOD_1);
      tick();

      // Fecha sem confirmar
      component.closeConfirmModal();
      tick();

      expect(apiSpy.executePurge).not.toHaveBeenCalled();
    }));
  });

  // ── executar expurgo ───────────────────────────────────────────────────────

  describe('confirmPurge()', () => {
    beforeEach(fakeAsync(() => {
      apiSpy.exportPurgeCsv.and.returnValue(of(new Blob(['csv'])));
      component.eligiblePeriods.set([PERIOD_1, PERIOD_2]);
      component.openConfirmModal(PERIOD_1);
      component.downloadCsv(PERIOD_1);
      tick();
    }));

    it('chama executePurge somente após confirmação no modal', fakeAsync(() => {
      apiSpy.executePurge.and.returnValue(of(PURGE_RESULT));
      component.confirmPurge();
      tick();
      expect(apiSpy.executePurge).toHaveBeenCalledWith('p-1', 'expurgo-p-1-2024_3.csv');
    }));

    it('POST não é chamado diretamente sem confirmação no modal', () => {
      // Sem chamar confirmPurge, executePurge não deve ser chamado
      expect(apiSpy.executePurge).not.toHaveBeenCalled();
    });

    it('remove o período expurgado da lista após sucesso', fakeAsync(() => {
      apiSpy.executePurge.and.returnValue(of(PURGE_RESULT));
      component.confirmPurge();
      tick();

      const remaining = component.eligiblePeriods();
      expect(remaining.find(p => p.periodId === 'p-1')).toBeUndefined();
      expect(remaining.length).toBe(1);
      expect(remaining[0].periodId).toBe('p-2');
    }));

    it('exibe estimatedSpaceKb após expurgo bem-sucedido', fakeAsync(() => {
      apiSpy.executePurge.and.returnValue(of(PURGE_RESULT));
      component.confirmPurge();
      tick();

      expect(component.purgeResult()?.estimatedSpaceKb).toBe(128);
    }));

    it('fecha o modal após expurgo bem-sucedido', fakeAsync(() => {
      apiSpy.executePurge.and.returnValue(of(PURGE_RESULT));
      component.confirmPurge();
      tick();

      expect(component.confirmModalOpen()).toBeFalse();
    }));

    it('define apiError quando executePurge falha', fakeAsync(() => {
      apiSpy.executePurge.and.returnValue(
        throwError(() => ({ error: { message: 'Falha no expurgo.' } }))
      );
      component.confirmPurge();
      tick();

      expect(component.apiError()).toBeTruthy();
      expect(component.confirmModalOpen()).toBeFalse();
    }));

    it('recarrega purgeRecords via getPurgeRecords após expurgo bem-sucedido', fakeAsync(() => {
      const novosRecords = [RECORD_1, RECORD_2];
      apiSpy.executePurge.and.returnValue(of(PURGE_RESULT));
      apiSpy.getPurgeRecords.and.returnValue(of(novosRecords));

      component.confirmPurge();
      tick();

      expect(apiSpy.getPurgeRecords).toHaveBeenCalled();
      expect(component.purgeRecords()).toEqual(novosRecords);
    }));
  });

  // ── GAP 1 — Angular Animations obrigatórias ───────────────────────────────

  describe('GAP 1 — animations: backdropAnim e modalAnim', () => {
    it('declara trigger backdropAnim no metadata do componente', () => {
      // Verifica que o @Component possui animations com trigger "backdropAnim"
      const animations: any[] = (PurgeComponent as any).ɵcmp?.data?.animation ?? [];
      const names = animations.map((a: any) => a?.name ?? '');
      expect(names).toContain('backdropAnim');
    });

    it('declara trigger modalAnim no metadata do componente', () => {
      // Verifica que o @Component possui animations com trigger "modalAnim"
      const animations: any[] = (PurgeComponent as any).ɵcmp?.data?.animation ?? [];
      const names = animations.map((a: any) => a?.name ?? '');
      expect(names).toContain('modalAnim');
    });

    it('elemento do overlay tem atributo ng-trigger-backdropAnim quando modal está aberto', () => {
      component.openConfirmModal(PERIOD_1);
      fixture.detectChanges();
      const overlay = fixture.nativeElement.querySelector('.modal-overlay');
      expect(overlay).not.toBeNull();
      // Deve possuir a classe CSS gerada pelas Angular Animations
      expect(overlay.classList.contains('ng-trigger-backdropAnim')).toBeTrue();
    });

    it('elemento do modal tem atributo ng-trigger-modalAnim quando modal está aberto', () => {
      component.openConfirmModal(PERIOD_1);
      fixture.detectChanges();
      const modal = fixture.nativeElement.querySelector('.modal');
      expect(modal).not.toBeNull();
      // Deve possuir a classe CSS gerada pelas Angular Animations
      expect(modal.classList.contains('ng-trigger-modalAnim')).toBeTrue();
    });
  });

  // ── GAP 2 — Texto destrutivo exato no modal ────────────────────────────────

  describe('GAP 2 — texto destrutivo no modal de confirmação', () => {
    it('exibe aviso destrutivo exato com mês e ano no modal', () => {
      component.openConfirmModal(PERIOD_1);
      fixture.detectChanges();

      // PERIOD_1: year=2024, month=3 → "Março/2024"
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain(
        'Confirmo que o arquivo CSV foi salvo com sucesso. Desejo excluir permanentemente os dados de'
      );
    });

    it('texto destrutivo inclui nome do mês e ano do período selecionado', () => {
      component.openConfirmModal(PERIOD_1);
      fixture.detectChanges();

      const confirmText = fixture.nativeElement.querySelector('.modal-confirm-text');
      expect(confirmText).not.toBeNull();
      expect(confirmText.textContent).toContain('2024');
    });
  });

  // ── GAP 3 — Botão "Confirmar" com classe btn-danger ────────────────────────

  describe('GAP 3 — botão de confirmação tem classe btn-danger', () => {
    it('botão que chama confirmPurge() tem classe btn-danger', () => {
      component.openConfirmModal(PERIOD_1);
      fixture.detectChanges();

      const dangerBtn = fixture.nativeElement.querySelector('button.btn-danger');
      expect(dangerBtn).not.toBeNull();
    });

    it('botão btn-danger dispara confirmPurge ao ser clicado', fakeAsync(() => {
      apiSpy.exportPurgeCsv.and.returnValue(of(new Blob(['csv'])));
      apiSpy.executePurge.and.returnValue(of(PURGE_RESULT));

      component.openConfirmModal(PERIOD_1);
      component.downloadCsv(PERIOD_1);
      tick();
      fixture.detectChanges();

      const dangerBtn: HTMLButtonElement = fixture.nativeElement.querySelector('button.btn-danger');
      expect(dangerBtn).not.toBeNull();
      dangerBtn.click();
      tick();

      expect(apiSpy.executePurge).toHaveBeenCalledWith('p-1', 'expurgo-p-1-2024_3.csv');
    }));
  });

  // ── edge cases: botão confirm desabilitado ─────────────────────────────────

  describe('botão de confirmação — estado de habilitação', () => {
    it('purgeConfirmed inicia como false (botão desabilitado)', () => {
      expect(component.purgeConfirmed()).toBeFalse();
    });

    it('purgeConfirmed reseta para false ao abrir novo período no modal', fakeAsync(() => {
      apiSpy.exportPurgeCsv.and.returnValue(of(new Blob(['csv'])));
      component.openConfirmModal(PERIOD_1);
      component.downloadCsv(PERIOD_1);
      tick();
      expect(component.purgeConfirmed()).toBeTrue();

      // Abre modal para outro período
      component.openConfirmModal(PERIOD_2);
      expect(component.purgeConfirmed()).toBeFalse();
    }));
  });

  // ── MOD-03: Listagem de PurgeRecords ──────────────────────────────────────

  describe('ngOnInit — carrega registros de expurgos (purgeRecords)', () => {
    it('chama getPurgeRecords na inicialização', fakeAsync(() => {
      expect(apiSpy.getPurgeRecords).toHaveBeenCalled();
    }));

    it('armazena os records retornados pelo backend', fakeAsync(() => {
      tick();
      expect(component.purgeRecords().length).toBe(2);
    }));

    it('exibe year, month, purgedAt, totalIncome, totalExpense e itemCount na tabela', fakeAsync(() => {
      tick();
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      // RECORD_1: year=2024, month=1 (Janeiro), purgedAt contém "2024-02-10"
      expect(text).toContain('2024');
      expect(text).toContain('5.000');  // totalIncome formatado com DecimalPipe
      expect(text).toContain('4.200');  // totalExpense
      expect(text).toContain('30');     // itemCount
    }));

    it('define apiError quando getPurgeRecords falha', fakeAsync(() => {
      apiSpy.getPurgeRecords.and.returnValue(
        throwError(() => ({ error: { message: 'Erro ao carregar registros.' } }))
      );
      component.ngOnInit();
      tick();
      expect(component.apiError()).toBeTruthy();
    }));

    it('exibe mensagem "Nenhum registro de expurgo encontrado." quando lista está vazia', fakeAsync(() => {
      apiSpy.getPurgeRecords.and.returnValue(of([]));
      component.ngOnInit();
      tick();
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Nenhum registro de expurgo encontrado.');
    }));
  });

  // ── MOD-03: PurgeWarningBannerComponent nos imports ─── (removido em #367) ──
  // PurgeWarningBannerComponent foi removido dos imports e template em #367.

  // ── MOD-03: Modal de delete de metadados ─────────────────────────────────

  describe('openDeleteRecordModal()', () => {
    it('abre o modal de delete ao clicar em "Excluir" para um record específico', fakeAsync(() => {
      tick();
      fixture.detectChanges();
      component.openDeleteRecordModal(RECORD_1);
      expect(component.deleteRecordModalOpen()).toBeTrue();
      expect(component.selectedRecord()?.id).toBe('r-1');
    }));

    it('exibe o modal de delete no DOM quando deleteRecordModalOpen é true', fakeAsync(() => {
      tick();
      component.openDeleteRecordModal(RECORD_1);
      fixture.detectChanges();
      const modal = fixture.nativeElement.querySelector('.modal-delete-record');
      expect(modal).not.toBeNull();
    }));
  });

  describe('closeDeleteRecordModal()', () => {
    it('fecha o modal sem chamar deletePurgeRecord', fakeAsync(() => {
      tick();
      component.openDeleteRecordModal(RECORD_1);
      component.closeDeleteRecordModal();
      expect(component.deleteRecordModalOpen()).toBeFalse();
      expect(apiSpy.deletePurgeRecord).not.toHaveBeenCalled();
    }));
  });

  describe('confirmDeleteRecord()', () => {
    beforeEach(fakeAsync(() => {
      tick();
      component.openDeleteRecordModal(RECORD_1);
    }));

    it('chama deletePurgeRecord com o id correto ao confirmar', fakeAsync(() => {
      apiSpy.deletePurgeRecord.and.returnValue(of(void 0));
      component.confirmDeleteRecord();
      tick();
      expect(apiSpy.deletePurgeRecord).toHaveBeenCalledWith('r-1');
    }));

    it('remove o record da lista após delete bem-sucedido', fakeAsync(() => {
      apiSpy.deletePurgeRecord.and.returnValue(of(void 0));
      component.confirmDeleteRecord();
      tick();
      const remaining = component.purgeRecords();
      expect(remaining.find((r: PurgeRecordResponse) => r.id === 'r-1')).toBeUndefined();
      expect(remaining.length).toBe(1);
    }));

    it('fecha o modal após delete bem-sucedido', fakeAsync(() => {
      apiSpy.deletePurgeRecord.and.returnValue(of(void 0));
      component.confirmDeleteRecord();
      tick();
      expect(component.deleteRecordModalOpen()).toBeFalse();
    }));

    it('define apiError quando deletePurgeRecord falha', fakeAsync(() => {
      apiSpy.deletePurgeRecord.and.returnValue(
        throwError(() => ({ error: { message: 'Falha ao deletar.' } }))
      );
      component.confirmDeleteRecord();
      tick();
      expect(component.apiError()).toBeTruthy();
    }));

    it('fecha o modal quando deletePurgeRecord falha', fakeAsync(() => {
      apiSpy.deletePurgeRecord.and.returnValue(
        throwError(() => ({ error: { message: 'Falha ao deletar.' } }))
      );
      component.confirmDeleteRecord();
      tick();
      expect(component.deleteRecordModalOpen()).toBeFalse();
    }));

    it('item permanece na lista quando deletePurgeRecord falha', fakeAsync(() => {
      apiSpy.deletePurgeRecord.and.returnValue(
        throwError(() => ({ error: { message: 'Falha ao deletar.' } }))
      );
      component.confirmDeleteRecord();
      tick();
      expect(component.purgeRecords().find((r: PurgeRecordResponse) => r.id === 'r-1')).toBeDefined();
    }));
  });

  // ── MOD-03: Texto do modal de delete ─────────────────────────────────────

  describe('modal de delete — conteúdo e botão danger', () => {
    beforeEach(fakeAsync(() => {
      tick();
      component.openDeleteRecordModal(RECORD_1);
      fixture.detectChanges();
    }));

    it('texto do modal explica que os dados do banco já foram deletados', () => {
      const text = fixture.nativeElement.textContent as string;
      // Verificar que contém alguma variação da frase sobre dados já deletados
      const hasDadosDeletados =
        text.toLowerCase().includes('dados') &&
        (text.toLowerCase().includes('deletados') || text.toLowerCase().includes('excluídos') || text.toLowerCase().includes('removidos'));
      expect(hasDadosDeletados).toBeTrue();
    });

    it('modal de delete possui botão btn-danger para confirmar', () => {
      const modal = fixture.nativeElement.querySelector('.modal-delete-record');
      expect(modal).not.toBeNull();
      const dangerBtn = modal.querySelector('button.btn-danger');
      expect(dangerBtn).not.toBeNull();
    });

    it('clique no btn-danger do modal de delete chama confirmDeleteRecord', fakeAsync(() => {
      apiSpy.deletePurgeRecord.and.returnValue(of(void 0));
      const modal = fixture.nativeElement.querySelector('.modal-delete-record');
      const dangerBtn: HTMLButtonElement = modal.querySelector('button.btn-danger');
      expect(dangerBtn).not.toBeNull();
      dangerBtn.click();
      tick();
      expect(apiSpy.deletePurgeRecord).toHaveBeenCalledWith('r-1');
    }));
  });

  // ── MOD-03: Angular Animations no modal de delete ────────────────────────

  describe('GAP 4 — animations no modal de delete (backdropAnim + modalAnim)', () => {
    it('elemento .modal-overlay do delete tem ng-trigger-backdropAnim', fakeAsync(() => {
      tick();
      component.openDeleteRecordModal(RECORD_1);
      fixture.detectChanges();
      // O overlay do modal de delete deve usar @backdropAnim
      const overlays = fixture.nativeElement.querySelectorAll('.modal-overlay');
      // Pelo menos um overlay com ng-trigger-backdropAnim
      const hasAnim = Array.from(overlays).some(
        (el: any) => el.classList.contains('ng-trigger-backdropAnim')
      );
      expect(hasAnim).toBeTrue();
    }));

    it('elemento .modal-delete-record tem ng-trigger-modalAnim', fakeAsync(() => {
      tick();
      component.openDeleteRecordModal(RECORD_1);
      fixture.detectChanges();
      const modal = fixture.nativeElement.querySelector('.modal-delete-record');
      expect(modal).not.toBeNull();
      expect(modal.classList.contains('ng-trigger-modalAnim')).toBeTrue();
    }));
  });

  // ── cards de períodos elegíveis — botão CSV ───────────────────────────────

  describe('cards de períodos elegíveis — botão CSV', () => {
    it('clique no botão CSV de um card chama exportPurgeCsv com o periodId correto', fakeAsync(() => {
      apiSpy.exportPurgeCsv.and.returnValue(of(new Blob(['csv'])));
      component.eligiblePeriods.set([PERIOD_1, PERIOD_2]);
      fixture.detectChanges();

      const csvBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-csv-card');
      expect(csvBtn).not.toBeNull('.btn-csv-card não encontrado — elemento ainda não implementado no template');

      csvBtn.click();
      tick();

      expect(apiSpy.exportPurgeCsv).toHaveBeenCalledWith('p-1');
    }));
  });

  // ── #356 RED — confirmPurge envia csvFileName no body ────────────────────

  describe('#356 RED — confirmPurge envia csvFileName', () => {
    it('confirmPurge envia csvFileName no formato correto', fakeAsync(() => {
      // Arrange: período com periodId='p-1', year=2025, month=3
      const period: EligiblePeriodResponse = {
        periodId:     'p-1',
        year:         2025,
        month:        3,
        totalIncome:  3000,
        totalExpense: 2500,
        itemCount:    18,
      };

      const blob = new Blob(['csv'], { type: 'text/csv' });
      apiSpy.exportPurgeCsv.and.returnValue(of(blob));
      apiSpy.executePurge.and.returnValue(of(PURGE_RESULT));

      spyOn(URL, 'createObjectURL').and.returnValue('blob:fake');
      spyOn(URL, 'revokeObjectURL');
      const fakeAnchor = document.createElement('a');
      spyOn(fakeAnchor, 'click');
      spyOn(document, 'createElement').and.callFake((tag: string) => {
        if (tag === 'a') return fakeAnchor;
        return document.createElement(tag);
      });

      component.openConfirmModal(period);
      component.downloadCsv(period);
      tick();

      // Act
      component.confirmPurge();
      tick();

      // Assert: executePurge chamado com ('p-1', 'expurgo-p-1-2025_3.csv')
      // Cast necessário pois a assinatura atual ainda não aceita 2 argumentos (RED)
      expect(apiSpy.executePurge as jasmine.Spy).toHaveBeenCalledWith('p-1', 'expurgo-p-1-2025_3.csv');
    }));
  });

  // ── #356 — nome do arquivo CSV com year_month ────────────────────────────

  describe('downloadCsv() — nome do arquivo', () => {
    it('downloadCsv define nome do arquivo com periodId, year e month', fakeAsync(() => {
      // Arrange
      const blob = new Blob(['csv,data'], { type: 'text/csv' });
      apiSpy.exportPurgeCsv.and.returnValue(of(blob));

      const fakeUrl = 'blob:fake-url-year-month';
      spyOn(URL, 'createObjectURL').and.returnValue(fakeUrl);
      spyOn(URL, 'revokeObjectURL');

      const fakeAnchor = document.createElement('a');
      spyOn(fakeAnchor, 'click');
      spyOn(document, 'createElement').and.callFake((tag: string) => {
        if (tag === 'a') return fakeAnchor;
        return document.createElement(tag);
      });

      // Act — period com periodId='p-1', year=2025, month=3
      const period: EligiblePeriodResponse = {
        periodId:     'p-1',
        year:         2025,
        month:        3,
        totalIncome:  3000,
        totalExpense: 2500,
        itemCount:    18,
      };
      component.downloadCsv(period);
      tick();

      // Assert — nome deve ser "expurgo-p-1-2025_3.csv"
      const downloadAttr = fakeAnchor.getAttribute('download') ?? '';
      expect(downloadAttr).toBe('expurgo-p-1-2025_3.csv');
    }));
  });

  // ── Reviewer #356 — lógica do botão Confirmar e checkbox ──────────────────

  describe('Reviewer #356 — lógica do botão Confirmar e checkbox', () => {
    it('botão Confirmar fica habilitado após downloadCsv pois purgeConfirmed é setado como true', fakeAsync(() => {
      apiSpy.exportPurgeCsv.and.returnValue(of(new Blob(['csv'])));

      component.openConfirmModal(PERIOD_1);
      component.downloadCsv(PERIOD_1);
      tick();
      fixture.detectChanges();

      const dangerBtn: HTMLButtonElement = fixture.nativeElement.querySelector('button.btn-danger');
      expect(dangerBtn).not.toBeNull();
      // downloadCsv seta purgeConfirmed=true → botão deve estar habilitado
      expect(dangerBtn.disabled).toBeFalse();
    }));

    it('downloadCsv bem-sucedido seta purgeConfirmed como true', fakeAsync(() => {
      apiSpy.exportPurgeCsv.and.returnValue(of(new Blob(['csv'])));

      expect(component.purgeConfirmed()).toBeFalse();

      component.downloadCsv(PERIOD_1);
      tick();

      expect(component.purgeConfirmed()).toBeTrue();
    }));
  });

  // ── #368 — botão de navegação para Análise de CSV ────────────────────────

  describe('#368 — botão de navegação para Análise de CSV', () => {
    it('renderiza um botão dentro de app-header para navegar para análise de CSV', fakeAsync(() => {
      tick();
      fixture.detectChanges();
      // O PurgeComponent deve projetar um botão via ng-content dentro de <app-header>
      // Com NO_ERRORS_SCHEMA, o conteúdo projetado fica como filho de app-header no DOM
      const appHeader = fixture.nativeElement.querySelector('app-header');
      expect(appHeader).withContext('app-header deve estar presente no DOM').not.toBeNull();
      const btnAnalise: HTMLElement | null | undefined =
        appHeader.querySelector('.btn-analise') ??
        appHeader.querySelector('[data-testid="btn-analise"]') ??
        Array.from(appHeader.querySelectorAll('button') as NodeListOf<HTMLElement>)
          .find((btn: HTMLElement) => btn.textContent?.toLowerCase().includes('análise')) ??
        null;
      expect(btnAnalise).withContext('deve existir um botão de Análise dentro de app-header via ng-content — PurgeComponent ainda não projeta o botão').not.toBeNull();
    }));

    it('clicar no botão de análise chama router.navigate com [\'purge\', \'analysis\']', fakeAsync(() => {
      tick();
      fixture.detectChanges();
      const router = TestBed.inject(Router);
      spyOn(router, 'navigate');

      const appHeader = fixture.nativeElement.querySelector('app-header');
      expect(appHeader).withContext('app-header deve estar presente').not.toBeNull();
      const btnAnalise: HTMLButtonElement | null =
        appHeader.querySelector('.btn-analise') ??
        appHeader.querySelector('[data-testid="btn-analise"]') ??
        Array.from(appHeader.querySelectorAll('button') as NodeListOf<HTMLElement>)
          .find((btn: HTMLElement) => btn.textContent?.toLowerCase().includes('análise')) ??
        null;
      expect(btnAnalise).withContext('botão de Análise deve existir para ser clicado — PurgeComponent ainda não projeta o botão').not.toBeNull();

      if (btnAnalise) {
        btnAnalise.click();
        tick();
        expect(router.navigate).toHaveBeenCalledWith(['purge', 'analysis']);
      }
    }));
  });

  // ── #367 RED — Layout: app-header, remoção do banner e CSS do modal ────────

  describe('#367 — header compartilhado (app-header)', () => {
    it('renderiza o elemento app-header no template', fakeAsync(() => {
      tick();
      fixture.detectChanges();
      // PurgeComponent deve usar <app-header> em vez de div.page-header manual
      const appHeader = fixture.nativeElement.querySelector('app-header');
      expect(appHeader).withContext('app-header deve estar no DOM — PurgeComponent ainda usa div.page-header manual').not.toBeNull();
    }));

    it('NÃO renderiza div.page-header manual', fakeAsync(() => {
      tick();
      fixture.detectChanges();
      // div.page-header deve ser removida quando app-header for introduzido
      const pageHeader = fixture.nativeElement.querySelector('.page-header');
      expect(pageHeader).withContext('div.page-header manual não deve mais existir no template após introdução do app-header').toBeNull();
    }));

    it('app-header recebe o atributo title com o valor "Expurgo de Períodos"', fakeAsync(() => {
      tick();
      fixture.detectChanges();
      const appHeader = fixture.nativeElement.querySelector('app-header');
      expect(appHeader).not.toBeNull();
      // Angular projeta inputs via atributo no DOM ao usar NO_ERRORS_SCHEMA
      const titleAttr = appHeader.getAttribute('title') ?? appHeader.getAttribute('ng-reflect-title') ?? '';
      expect(titleAttr).toContain('Expurgo');
    }));
  });

  describe('#367 — remoção do PurgeWarningBannerComponent', () => {
    it('NÃO renderiza app-purge-warning-banner na seção de records', fakeAsync(() => {
      tick();
      fixture.detectChanges();
      // O typewriter banner deve ser removido do template
      const banner = fixture.nativeElement.querySelector('app-purge-warning-banner');
      expect(banner).withContext('app-purge-warning-banner deve ser removido do template do PurgeComponent — issue #367').toBeNull();
    }));

    it('PurgeWarningBannerComponent NÃO está nos imports do PurgeComponent', () => {
      // Verifica que PurgeWarningBannerComponent não é mais declarado como import
      const rawDeps: any = (PurgeComponent as any).ɵcmp?.dependencies;
      // dependencies pode ser função (lazy) ou array direto em Angular 21
      const deps: any[] = Array.isArray(rawDeps) ? rawDeps
        : (typeof rawDeps === 'function' ? rawDeps() : []);
      const hasBanner = deps.some((dep: any) => {
        const sel: string = dep?.ɵcmp?.selectors?.[0]?.[1] ?? dep?.ɵdir?.selectors?.[0]?.[1] ?? '';
        return sel === 'app-purge-warning-banner';
      });
      expect(hasBanner).withContext('PurgeWarningBannerComponent não deve mais estar nos imports do PurgeComponent após issue #367').toBeFalse();
    });
  });

  describe('#367 — CSS do modal: overflow e posicionamento do sonic-frame', () => {
    it('.modal deve ter overflow: visible (não hidden)', fakeAsync(() => {
      component.openConfirmModal(PERIOD_1);
      tick();
      fixture.detectChanges();

      const modal: HTMLElement = fixture.nativeElement.querySelector('.modal');
      expect(modal).not.toBeNull();

      // O CSS do componente é scoped — computedStyle reflete o estilo aplicado
      const computed = getComputedStyle(modal);
      expect(computed.overflow).withContext('.modal deve ter overflow: visible para que o sonic-frame pixel-art possa extravasar o box').toBe('visible');
    }));

    it('.sonic-frame deve ter posicionamento com valores negativos (externo ao box do modal)', fakeAsync(() => {
      component.openConfirmModal(PERIOD_1);
      tick();
      fixture.detectChanges();

      const sonicFrame: HTMLElement = fixture.nativeElement.querySelector('.sonic-frame');
      expect(sonicFrame).not.toBeNull();

      const computed = getComputedStyle(sonicFrame);

      // Pelo menos um dos lados deve ter valor negativo (margin-based external positioning)
      const leftVal   = parseFloat(computed.left)   || 0;
      const rightVal  = parseFloat(computed.right)  || 0;
      const topVal    = parseFloat(computed.top)    || 0;
      const bottomVal = parseFloat(computed.bottom) || 0;

      const hasNegativePositioning =
        leftVal < 0 || rightVal < 0 || topVal < 0 || bottomVal < 0;

      expect(hasNegativePositioning).withContext(
        '.sonic-frame deve ter pelo menos um valor de posicionamento negativo (left/right/top/bottom) para ficar externo ao box do modal'
      ).toBeTrue();
    }));
  });
});
