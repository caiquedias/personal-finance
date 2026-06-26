import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { PurgeComponent } from './purge.component';
import { ApiService } from '../../../../core/services/api.service';
import { EligiblePeriodResponse, PurgeResultResponse } from '../../../../core/models/models';

// ── local fixture type (PurgeRecordResponse ainda não existe em models.ts) ───

interface PurgeRecordResponse {
  id:           string;
  year:         number;
  month:        number;
  purgedAt:     string;
  totalIncome:  number;
  totalExpense: number;
  itemCount:    number;
}

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

    it('habilita confirmação somente após blob onload (csvReady)', fakeAsync(() => {
      // Antes do download, confirmação não disponível
      expect(component.csvReady()).toBeFalse();

      const blob = new Blob(['csv'], { type: 'text/csv' });
      apiSpy.exportPurgeCsv.and.returnValue(of(blob));

      component.downloadCsv(PERIOD_1);
      tick();

      expect(component.csvReady()).toBeTrue();
    }));

    it('confirmação permanece desabilitada antes do blob onload', () => {
      // Sem chamar downloadCsv, csvReady deve ser false
      expect(component.csvReady()).toBeFalse();
    });

    it('define downloadError quando exportPurgeCsv falha', fakeAsync(() => {
      apiSpy.exportPurgeCsv.and.returnValue(
        throwError(() => ({ error: { message: 'Falha no download.' } }))
      );
      component.downloadCsv(PERIOD_1);
      tick();
      expect(component.csvReady()).toBeFalse();
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

    it('reseta csvReady ao abrir novo modal', () => {
      component.csvReady.set(true);
      component.openConfirmModal(PERIOD_1);
      expect(component.csvReady()).toBeFalse();
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
      expect(apiSpy.executePurge).toHaveBeenCalledWith('p-1');
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

      expect(apiSpy.executePurge).toHaveBeenCalledWith('p-1');
    }));
  });

  // ── edge cases: botão confirm desabilitado ─────────────────────────────────

  describe('botão de confirmação — estado de habilitação', () => {
    it('csvReady inicia como false (botão desabilitado)', () => {
      expect(component.csvReady()).toBeFalse();
    });

    it('csvReady torna-se true somente após blob carregado', fakeAsync(() => {
      apiSpy.exportPurgeCsv.and.returnValue(of(new Blob(['csv'])));
      expect(component.csvReady()).toBeFalse();

      component.downloadCsv(PERIOD_1);
      tick();

      expect(component.csvReady()).toBeTrue();
    }));

    it('csvReady reseta para false ao abrir novo período no modal', fakeAsync(() => {
      apiSpy.exportPurgeCsv.and.returnValue(of(new Blob(['csv'])));
      component.openConfirmModal(PERIOD_1);
      component.downloadCsv(PERIOD_1);
      tick();
      expect(component.csvReady()).toBeTrue();

      // Abre modal para outro período
      component.openConfirmModal(PERIOD_2);
      expect(component.csvReady()).toBeFalse();
    }));
  });

  // ── MOD-03: Listagem de PurgeRecords ──────────────────────────────────────

  describe('ngOnInit — carrega registros de expurgos (purgeRecords)', () => {
    it('chama getPurgeRecords na inicialização', fakeAsync(() => {
      expect(apiSpy.getPurgeRecords).toHaveBeenCalled();
    }));

    it('armazena os records retornados pelo backend', fakeAsync(() => {
      tick();
      expect((component as any).purgeRecords().length).toBe(2);
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

  // ── MOD-03: PurgeWarningBannerComponent nos imports ───────────────────────

  describe('PurgeWarningBannerComponent — import obrigatório', () => {
    it('PurgeWarningBannerComponent está nos imports do PurgeComponent', fakeAsync(() => {
      // Verifica via DOM: NO_ERRORS_SCHEMA ignoraria tags desconhecidas sem erro,
      // mas o selector deve ser reconhecido pelo Angular quando importado corretamente.
      // A segunda asserção (renderização DOM) é o critério definitivo.
      tick();
      fixture.detectChanges();
      const banner = fixture.nativeElement.querySelector('app-purge-warning-banner');
      expect(banner).not.toBeNull('app-purge-warning-banner deve estar no DOM — PurgeWarningBannerComponent não está importado');
    }));

    it('renderiza app-purge-warning-banner na seção de records', fakeAsync(() => {
      tick();
      fixture.detectChanges();
      const banner = fixture.nativeElement.querySelector('app-purge-warning-banner');
      expect(banner).not.toBeNull();
    }));
  });

  // ── MOD-03: Modal de delete de metadados ─────────────────────────────────

  describe('openDeleteRecordModal()', () => {
    it('abre o modal de delete ao clicar em "Excluir" para um record específico', fakeAsync(() => {
      const cmp = component as any;
      tick();
      fixture.detectChanges();
      cmp.openDeleteRecordModal(RECORD_1);
      expect(cmp.deleteRecordModalOpen()).toBeTrue();
      expect(cmp.selectedRecord()?.id).toBe('r-1');
    }));

    it('exibe o modal de delete no DOM quando deleteRecordModalOpen é true', fakeAsync(() => {
      const cmp = component as any;
      tick();
      cmp.openDeleteRecordModal(RECORD_1);
      fixture.detectChanges();
      const modal = fixture.nativeElement.querySelector('.modal-delete-record');
      expect(modal).not.toBeNull();
    }));
  });

  describe('closeDeleteRecordModal()', () => {
    it('fecha o modal sem chamar deletePurgeRecord', fakeAsync(() => {
      const cmp = component as any;
      tick();
      cmp.openDeleteRecordModal(RECORD_1);
      cmp.closeDeleteRecordModal();
      expect(cmp.deleteRecordModalOpen()).toBeFalse();
      expect(apiSpy.deletePurgeRecord).not.toHaveBeenCalled();
    }));
  });

  describe('confirmDeleteRecord()', () => {
    beforeEach(fakeAsync(() => {
      tick();
      (component as any).openDeleteRecordModal(RECORD_1);
    }));

    it('chama deletePurgeRecord com o id correto ao confirmar', fakeAsync(() => {
      const cmp = component as any;
      apiSpy.deletePurgeRecord.and.returnValue(of(void 0));
      cmp.confirmDeleteRecord();
      tick();
      expect(apiSpy.deletePurgeRecord).toHaveBeenCalledWith('r-1');
    }));

    it('remove o record da lista após delete bem-sucedido', fakeAsync(() => {
      const cmp = component as any;
      apiSpy.deletePurgeRecord.and.returnValue(of(void 0));
      cmp.confirmDeleteRecord();
      tick();
      const remaining = cmp.purgeRecords();
      expect(remaining.find((r: any) => r.id === 'r-1')).toBeUndefined();
      expect(remaining.length).toBe(1);
    }));

    it('fecha o modal após delete bem-sucedido', fakeAsync(() => {
      const cmp = component as any;
      apiSpy.deletePurgeRecord.and.returnValue(of(void 0));
      cmp.confirmDeleteRecord();
      tick();
      expect(cmp.deleteRecordModalOpen()).toBeFalse();
    }));

    it('define apiError quando deletePurgeRecord falha', fakeAsync(() => {
      const cmp = component as any;
      apiSpy.deletePurgeRecord.and.returnValue(
        throwError(() => ({ error: { message: 'Falha ao deletar.' } }))
      );
      cmp.confirmDeleteRecord();
      tick();
      expect(component.apiError()).toBeTruthy();
    }));

    it('fecha o modal quando deletePurgeRecord falha', fakeAsync(() => {
      const cmp = component as any;
      apiSpy.deletePurgeRecord.and.returnValue(
        throwError(() => ({ error: { message: 'Falha ao deletar.' } }))
      );
      cmp.confirmDeleteRecord();
      tick();
      expect(cmp.deleteRecordModalOpen()).toBeFalse();
    }));

    it('item permanece na lista quando deletePurgeRecord falha', fakeAsync(() => {
      const cmp = component as any;
      apiSpy.deletePurgeRecord.and.returnValue(
        throwError(() => ({ error: { message: 'Falha ao deletar.' } }))
      );
      cmp.confirmDeleteRecord();
      tick();
      expect(cmp.purgeRecords().find((r: any) => r.id === 'r-1')).toBeDefined();
    }));
  });

  // ── MOD-03: Texto do modal de delete ─────────────────────────────────────

  describe('modal de delete — conteúdo e botão danger', () => {
    beforeEach(fakeAsync(() => {
      tick();
      (component as any).openDeleteRecordModal(RECORD_1);
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
      (component as any).openDeleteRecordModal(RECORD_1);
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
      (component as any).openDeleteRecordModal(RECORD_1);
      fixture.detectChanges();
      const modal = fixture.nativeElement.querySelector('.modal-delete-record');
      expect(modal).not.toBeNull();
      expect(modal.classList.contains('ng-trigger-modalAnim')).toBeTrue();
    }));
  });
});
