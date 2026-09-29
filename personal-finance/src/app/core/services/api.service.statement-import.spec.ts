import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ApiService } from './api.service';
import { environment } from '../../../environments/environment';

const BASE = environment.apiUrl;

// Métodos ainda não existem no ApiService — cast para any evita erro de compilação no Red
describe('ApiService — import de extrato PDF', () => {
  let service: any;
  let httpMock: HttpTestingController;
  const file = new File([new Uint8Array(10)], 'extrato.pdf', { type: 'application/pdf' });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), ApiService],
    });
    service  = TestBed.inject(ApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('previewStatementImport faz POST multipart em /import/statement/preview com todos os campos', () => {
    service.previewStatementImport(file, 'abc', '2026-01-15').subscribe();
    const req = httpMock.expectOne(`${BASE}/import/statement/preview`);
    expect(req.request.method).toBe('POST');
    const body = req.request.body as FormData;
    expect(body instanceof FormData).toBeTrue();
    expect((body.get('file') as File).name).toBe('extrato.pdf');
    expect(body.get('password')).toBe('abc');
    expect(body.get('fromDate')).toBe('2026-01-15');
    req.flush({ items: [], discardedByDateCount: 0 });
  });

  it('previewStatementImport omite password e fromDate quando não informados', () => {
    service.previewStatementImport(file).subscribe();
    const req = httpMock.expectOne(`${BASE}/import/statement/preview`);
    const body = req.request.body as FormData;
    expect(body.has('file')).toBeTrue();
    expect(body.has('password')).toBeFalse();
    expect(body.has('fromDate')).toBeFalse();
    req.flush({ items: [], discardedByDateCount: 0 });
  });

  it('previewStatementImport omite password e fromDate quando vazios', () => {
    service.previewStatementImport(file, '', '').subscribe();
    const req = httpMock.expectOne(`${BASE}/import/statement/preview`);
    const body = req.request.body as FormData;
    expect(body.has('password')).toBeFalse();
    expect(body.has('fromDate')).toBeFalse();
    req.flush({ items: [], discardedByDateCount: 0 });
  });

  it('confirmStatementImport faz POST JSON em /import/statement/confirm', () => {
    const payload = { items: [{ date: '2026-01-10', description: 'x', amount: 5, kind: 'Expense', categoryId: 'c1', sourceType: 'Personal' }] };
    service.confirmStatementImport(payload).subscribe();
    const req = httpMock.expectOne(`${BASE}/import/statement/confirm`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ periodsCreated: 1, periodsReused: 0, expensesCreated: 1, incomesCreated: 0 });
  });
});
