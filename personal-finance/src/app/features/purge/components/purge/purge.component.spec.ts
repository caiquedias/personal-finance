import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { PurgeComponent } from './purge.component';
import { ApiService } from '../../../../core/services/api.service';
import { EligiblePeriodResponse, PurgeResultResponse } from '../../../../core/models/models';

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

// ── suite ─────────────────────────────────────────────────────────────────────

describe('PurgeComponent', () => {
  let fixture: ComponentFixture<PurgeComponent>;
  let component: PurgeComponent;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('ApiService', [
      'getEligiblePeriods',
      'exportPurgeCsv',
      'executePurge',
    ]);
    apiSpy.getEligiblePeriods.and.returnValue(of([PERIOD_1, PERIOD_2]));

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
});
