import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ImportComponent } from './import.component';

describe('ImportComponent — abas', () => {
  let fixture: any;
  let c: any;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ImportComponent, RouterModule.forRoot([])],
      providers: [provideHttpClient(), provideHttpClientTesting()],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();
    fixture = TestBed.createComponent(ImportComponent);
    c = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('aba inicial é "legacy" e não renderiza o import de extrato', () => {
    expect(c.activeTab()).toBe('legacy');
    expect(fixture.nativeElement.querySelector('app-statement-import')).toBeNull();
    expect(fixture.nativeElement.querySelector('.drop-zone')).not.toBeNull();
  });

  it('setTab("statement") renderiza app-statement-import e oculta o legado', () => {
    c.setTab('statement');
    fixture.detectChanges();
    expect(c.activeTab()).toBe('statement');
    expect(fixture.nativeElement.querySelector('app-statement-import')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.drop-zone')).toBeNull();
  });

  it('voltar para "legacy" restaura o fluxo legado', () => {
    c.setTab('statement');
    fixture.detectChanges();
    c.setTab('legacy');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.drop-zone')).not.toBeNull();
  });

  it('exibe dois botões de aba (Legado e Extrato PDF)', () => {
    const tabs = fixture.nativeElement.querySelectorAll('[role="tab"]');
    expect(tabs.length).toBe(2);
  });
});
