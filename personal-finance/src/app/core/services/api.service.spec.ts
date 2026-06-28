import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ApiService } from './api.service';
import { environment } from '../../../environments/environment';

const BASE = environment.apiUrl;

describe('ApiService', () => {
  let service: ApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), ApiService],
    });
    service  = TestBed.inject(ApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  // ── Categories ──────────────────────────────────────────────────────────────

  it('getCategories faz GET /categories', () => {
    service.getCategories().subscribe();
    httpMock.expectOne(`${BASE}/categories`).flush([]);
  });

  it('createCategory faz POST /categories com body', () => {
    const body = { name: 'Food', color: '#fff' };
    service.createCategory(body).subscribe();
    const req = httpMock.expectOne(`${BASE}/categories`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({});
  });

  it('updateCategory faz PUT /categories/:id', () => {
    service.updateCategory('cat-1', { name: 'X', color: '#000' }).subscribe();
    const req = httpMock.expectOne(`${BASE}/categories/cat-1`);
    expect(req.request.method).toBe('PUT');
    req.flush(null);
  });

  it('deleteCategory faz DELETE /categories/:id', () => {
    service.deleteCategory('cat-1').subscribe();
    const req = httpMock.expectOne(`${BASE}/categories/cat-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  // ── Periods ─────────────────────────────────────────────────────────────────

  it('getPeriods faz GET /periods', () => {
    service.getPeriods().subscribe();
    httpMock.expectOne(`${BASE}/periods`).flush([]);
  });

  it('createPeriod faz POST /periods', () => {
    service.createPeriod({ year: 2024, month: 1 }).subscribe();
    const req = httpMock.expectOne(`${BASE}/periods`);
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('togglePeriodActive faz PATCH /periods/:id/toggle-active', () => {
    service.togglePeriodActive('p-1').subscribe();
    const req = httpMock.expectOne(`${BASE}/periods/p-1/toggle-active`);
    expect(req.request.method).toBe('PATCH');
    req.flush(null);
  });

  it('deletePeriod faz DELETE /periods/:id', () => {
    service.deletePeriod('p-1').subscribe();
    const req = httpMock.expectOne(`${BASE}/periods/p-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('getPeriodSummary faz GET /periods/:id/summary', () => {
    service.getPeriodSummary('p-1').subscribe();
    httpMock.expectOne(`${BASE}/periods/p-1/summary`).flush({});
  });

  // ── Expenses ─────────────────────────────────────────────────────────────────

  it('getExpensesByPeriod faz GET /expenses?periodId=...', () => {
    service.getExpensesByPeriod('p-1').subscribe();
    const req = httpMock.expectOne(r => r.url === `${BASE}/expenses`);
    expect(req.request.params.get('periodId')).toBe('p-1');
    req.flush([]);
  });

  it('createExpense faz POST /expenses', () => {
    service.createExpense({} as any).subscribe();
    const req = httpMock.expectOne(`${BASE}/expenses`);
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('updateExpense faz PUT /expenses/:id', () => {
    service.updateExpense('e-1', {} as any).subscribe();
    const req = httpMock.expectOne(`${BASE}/expenses/e-1`);
    expect(req.request.method).toBe('PUT');
    req.flush(null);
  });

  it('markExpenseAsPaid faz PATCH /expenses/:id/pay', () => {
    service.markExpenseAsPaid('e-1', { paymentDate: '2024-01-01' }).subscribe();
    const req = httpMock.expectOne(`${BASE}/expenses/e-1/pay`);
    expect(req.request.method).toBe('PATCH');
    req.flush(null);
  });

  it('deleteExpense faz DELETE /expenses/:id', () => {
    service.deleteExpense('e-1').subscribe();
    const req = httpMock.expectOne(`${BASE}/expenses/e-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  // ── Incomes ──────────────────────────────────────────────────────────────────

  it('getIncomesByPeriod faz GET /incomes?periodId=...', () => {
    service.getIncomesByPeriod('p-1').subscribe();
    const req = httpMock.expectOne(r => r.url === `${BASE}/incomes`);
    expect(req.request.params.get('periodId')).toBe('p-1');
    req.flush([]);
  });

  it('createIncome faz POST /incomes', () => {
    service.createIncome({} as any).subscribe();
    const req = httpMock.expectOne(`${BASE}/incomes`);
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('deleteIncome faz DELETE /incomes/:id', () => {
    service.deleteIncome('i-1').subscribe();
    const req = httpMock.expectOne(`${BASE}/incomes/i-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  // ── Config — lookup tables ────────────────────────────────────────────────────

  it('getPaymentStatuses faz GET /config/payment-statuses', () => {
    service.getPaymentStatuses().subscribe();
    httpMock.expectOne(`${BASE}/config/payment-statuses`).flush([]);
  });

  it('createPaymentStatus faz POST /config/payment-statuses', () => {
    service.createPaymentStatus({ name: 'PS', description: '' }).subscribe();
    const req = httpMock.expectOne(`${BASE}/config/payment-statuses`);
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('updatePaymentStatus faz PUT /config/payment-statuses/:id', () => {
    service.updatePaymentStatus(1, { name: 'X', description: '' }).subscribe();
    const req = httpMock.expectOne(`${BASE}/config/payment-statuses/1`);
    expect(req.request.method).toBe('PUT');
    req.flush({});
  });

  it('deletePaymentStatus faz DELETE /config/payment-statuses/:id', () => {
    service.deletePaymentStatus(1).subscribe();
    const req = httpMock.expectOne(`${BASE}/config/payment-statuses/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('getSourceTypes faz GET /config/source-types', () => {
    service.getSourceTypes().subscribe();
    httpMock.expectOne(`${BASE}/config/source-types`).flush([]);
  });

  it('getFortnightTypes faz GET /config/fortnight-types', () => {
    service.getFortnightTypes().subscribe();
    httpMock.expectOne(`${BASE}/config/fortnight-types`).flush([]);
  });

  // ── Admin ─────────────────────────────────────────────────────────────────────

  it('getAdminUsers faz GET /admin/users', () => {
    service.getAdminUsers().subscribe();
    httpMock.expectOne(`${BASE}/admin/users`).flush([]);
  });

  it('assignRole faz POST /admin/users/:id/roles', () => {
    service.assignRole('u-1', { roleId: 1 }).subscribe();
    const req = httpMock.expectOne(`${BASE}/admin/users/u-1/roles`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('removeRole faz DELETE /admin/users/:id/roles/:roleId', () => {
    service.removeRole('u-1', 2).subscribe();
    const req = httpMock.expectOne(`${BASE}/admin/users/u-1/roles/2`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('toggleUserActive faz PATCH /admin/users/:id/toggle-active', () => {
    service.toggleUserActive('u-1').subscribe();
    const req = httpMock.expectOne(`${BASE}/admin/users/u-1/toggle-active`);
    expect(req.request.method).toBe('PATCH');
    req.flush(null);
  });

  it('resetUserPassword faz PATCH /admin/users/:id/reset-password', () => {
    service.resetUserPassword('u-1', { newPassword: 'abc' }).subscribe();
    const req = httpMock.expectOne(`${BASE}/admin/users/u-1/reset-password`);
    expect(req.request.method).toBe('PATCH');
    req.flush(null);
  });

  // ── Purge ─────────────────────────────────────────────────────────────────

  it('getEligiblePeriods faz GET /purge/eligible-periods', () => {
    service.getEligiblePeriods().subscribe();
    httpMock.expectOne(`${BASE}/purge/eligible-periods`).flush([]);
  });

  it('exportPurgeCsv faz GET /purge/:id/export com responseType blob', () => {
    service.exportPurgeCsv('p-1').subscribe();
    const req = httpMock.expectOne(`${BASE}/purge/p-1/export`);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['csv']));
  });

  it('executePurge faz POST /purge/:id', () => {
    service.executePurge('p-1', 'expurgo-p-1-2024_3.csv').subscribe();
    const req = httpMock.expectOne(`${BASE}/purge/p-1`);
    expect(req.request.method).toBe('POST');
    req.flush({ periodId: 'p-1', estimatedSpaceKb: 128 });
  });

  // ── #356 RED — executePurge deve enviar csvFileName no body ──────────────

  it('executePurge envia body com csvFileName', () => {
    // Cast necessário pois a assinatura atual ainda não aceita 2 argumentos (RED)
    (service.executePurge as (id: string, csv: string) => any)('p-1', 'expurgo-p-1-2025_3.csv').subscribe();
    const req = httpMock.expectOne(`${BASE}/purge/p-1`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ csvFileName: 'expurgo-p-1-2025_3.csv' });
    req.flush({ periodId: 'p-1', estimatedSpaceKb: 128 });
  });
});
