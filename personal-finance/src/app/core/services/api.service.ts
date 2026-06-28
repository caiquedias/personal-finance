import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CategoryResponse, CreateCategoryRequest, UpdateCategoryRequest,
  PeriodResponse, CreatePeriodRequest, PeriodSummary, RecurringExpenseResponse,
  ExpenseResponse, CreateExpenseRequest, UpdateExpenseRequest, MarkAsPaidRequest, ExpenseOrderItem,
  BatchExpenseItemRequest, CreateExpensesBatchRequest,
  PagedResult, ExpenseFilterParams, IncomeFilterParams,
  IncomeResponse, CreateIncomeRequest,
  LookupItem,
  CreatePaymentStatusRequest, CreateSourceTypeRequest, CreateFortnightTypeRequest,
  UpdatePaymentStatusRequest, UpdateSourceTypeRequest, UpdateFortnightTypeRequest,
  AdminUserResponse, AdminUserFilterParams, AssignRoleRequest, ResetPasswordRequest,
  CreateUserByAdminRequest, UpdateUserByAdminRequest,
  ExpensesReport,
  EligiblePeriodResponse, PurgeResultResponse, PurgeRecordResponse,
} from '../models/models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  // ── Categories ────────────────────────────────────────────────────────────

  getCategories(): Observable<CategoryResponse[]> {
    return this.http.get<CategoryResponse[]>(`${this.base}/categories`);
  }

  getCategoryById(id: string): Observable<CategoryResponse> {
    return this.http.get<CategoryResponse>(`${this.base}/categories/${id}`);
  }

  createCategory(data: CreateCategoryRequest): Observable<CategoryResponse> {
    return this.http.post<CategoryResponse>(`${this.base}/categories`, data);
  }

  updateCategory(id: string, data: UpdateCategoryRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/categories/${id}`, data);
  }

  deleteCategory(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/categories/${id}`);
  }

  // ── Periods ───────────────────────────────────────────────────────────────

  getPeriods(): Observable<PeriodResponse[]> {
    return this.http.get<PeriodResponse[]>(`${this.base}/periods`);
  }

  getPeriodById(id: string): Observable<PeriodResponse> {
    return this.http.get<PeriodResponse>(`${this.base}/periods/${id}`);
  }

  createPeriod(data: CreatePeriodRequest): Observable<PeriodResponse> {
    return this.http.post<PeriodResponse>(`${this.base}/periods`, data);
  }

  togglePeriodActive(id: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/periods/${id}/toggle-active`, {});
  }

  deletePeriod(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/periods/${id}`);
  }

  getPeriodSummary(id: string): Observable<PeriodSummary> {
    return this.http.get<PeriodSummary>(`${this.base}/periods/${id}/summary`);
  }

  getRecurringExpenses(periodId: string): Observable<RecurringExpenseResponse[]> {
    return this.http.get<RecurringExpenseResponse[]>(`${this.base}/periods/${periodId}/recurring-expenses`);
  }

  replicateExpenses(periodId: string, expenseIds: string[]): Observable<void> {
    return this.http.post<void>(`${this.base}/periods/${periodId}/replicate-expenses`, expenseIds);
  }

  // ── Expenses ──────────────────────────────────────────────────────────────

  getExpensesByPeriod(periodId: string, filters?: ExpenseFilterParams): Observable<PagedResult<ExpenseResponse>> {
    let params = new HttpParams().set('periodId', periodId);
    if (filters?.pageNumber != null)    params = params.set('pageNumber',    filters.pageNumber);
    if (filters?.pageSize != null)      params = params.set('pageSize',      filters.pageSize);
    if (filters?.description)           params = params.set('description',   filters.description);
    if (filters?.categoryId)            params = params.set('categoryId',    filters.categoryId);
    if (filters?.paymentStatus != null) params = params.set('paymentStatus', filters.paymentStatus);
    if (filters?.fortnightType != null) params = params.set('fortnightType', filters.fortnightType);
    if (filters?.sourceType != null)    params = params.set('sourceType',    filters.sourceType);
    if (filters?.sortColumn != null)    params = params.set('sortColumn',    filters.sortColumn);
    if (filters?.sortDirection != null) params = params.set('sortDirection', filters.sortDirection);
    return this.http.get<PagedResult<ExpenseResponse>>(`${this.base}/expenses`, { params });
  }

  getExpenseById(id: string): Observable<ExpenseResponse> {
    return this.http.get<ExpenseResponse>(`${this.base}/expenses/${id}`);
  }

  createExpense(data: CreateExpenseRequest): Observable<ExpenseResponse> {
    return this.http.post<ExpenseResponse>(`${this.base}/expenses`, data);
  }

  updateExpense(id: string, data: UpdateExpenseRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/expenses/${id}`, data);
  }

  markExpenseAsPaid(id: string, data: MarkAsPaidRequest): Observable<void> {
    return this.http.patch<void>(`${this.base}/expenses/${id}/pay`, data);
  }

  cancelExpense(id: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/expenses/${id}/cancel`, {});
  }

  markExpenseAsPartial(id: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/expenses/${id}/partial`, {});
  }

  deleteExpense(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/expenses/${id}`);
  }

  saveExpenseOrder(items: ExpenseOrderItem[]): Observable<void> {
    return this.http.post<void>(`${this.base}/expenses/order`, items);
  }

  batchPayExpenses(ids: string[]): Observable<void> {
    return this.http.patch<void>(`${this.base}/expenses/batch/pay`, { expenseIds: ids });
  }

  batchCancelExpenses(ids: string[]): Observable<void> {
    return this.http.patch<void>(`${this.base}/expenses/batch/cancel`, { expenseIds: ids });
  }

  batchDeleteExpenses(ids: string[]): Observable<void> {
    return this.http.delete<void>(`${this.base}/expenses/batch`, { body: { expenseIds: ids } });
  }

  createExpensesBatch(data: CreateExpensesBatchRequest): Observable<ExpenseResponse[]> {
    return this.http.post<ExpenseResponse[]>(`${this.base}/expenses/batch/create`, data);
  }

  // ── Incomes ───────────────────────────────────────────────────────────────

  getIncomesByPeriod(periodId: string, filters?: IncomeFilterParams): Observable<PagedResult<IncomeResponse>> {
    let params = new HttpParams().set('periodId', periodId);
    if (filters?.pageNumber != null)    params = params.set('pageNumber',    filters.pageNumber);
    if (filters?.pageSize != null)      params = params.set('pageSize',      filters.pageSize);
    if (filters?.description)           params = params.set('description',   filters.description);
    if (filters?.fortnightType != null) params = params.set('fortnightType', filters.fortnightType);
    return this.http.get<PagedResult<IncomeResponse>>(`${this.base}/incomes`, { params });
  }

  getIncomeById(id: string): Observable<IncomeResponse> {
    return this.http.get<IncomeResponse>(`${this.base}/incomes/${id}`);
  }

  createIncome(data: CreateIncomeRequest): Observable<IncomeResponse> {
    return this.http.post<IncomeResponse>(`${this.base}/incomes`, data);
  }

  deleteIncome(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/incomes/${id}`);
  }

  // ── Config — lookup tables ─────────────────────────────────────────────────

  getPaymentStatuses(): Observable<LookupItem[]> {
    return this.http.get<LookupItem[]>(`${this.base}/config/payment-statuses`);
  }

  createPaymentStatus(data: CreatePaymentStatusRequest): Observable<LookupItem> {
    return this.http.post<LookupItem>(`${this.base}/config/payment-statuses`, data);
  }

  updatePaymentStatus(id: number, data: UpdatePaymentStatusRequest): Observable<LookupItem> {
    return this.http.put<LookupItem>(`${this.base}/config/payment-statuses/${id}`, data);
  }

  deletePaymentStatus(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/config/payment-statuses/${id}`);
  }

  getSourceTypes(): Observable<LookupItem[]> {
    return this.http.get<LookupItem[]>(`${this.base}/config/source-types`);
  }

  createSourceType(data: CreateSourceTypeRequest): Observable<LookupItem> {
    return this.http.post<LookupItem>(`${this.base}/config/source-types`, data);
  }

  updateSourceType(id: number, data: UpdateSourceTypeRequest): Observable<LookupItem> {
    return this.http.put<LookupItem>(`${this.base}/config/source-types/${id}`, data);
  }

  deleteSourceType(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/config/source-types/${id}`);
  }

  getFortnightTypes(): Observable<LookupItem[]> {
    return this.http.get<LookupItem[]>(`${this.base}/config/fortnight-types`);
  }

  createFortnightType(data: CreateFortnightTypeRequest): Observable<LookupItem> {
    return this.http.post<LookupItem>(`${this.base}/config/fortnight-types`, data);
  }

  updateFortnightType(id: number, data: UpdateFortnightTypeRequest): Observable<LookupItem> {
    return this.http.put<LookupItem>(`${this.base}/config/fortnight-types/${id}`, data);
  }

  deleteFortnightType(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/config/fortnight-types/${id}`);
  }

  // ── Admin ─────────────────────────────────────────────────────────────────

  getAdminUsers(filters?: AdminUserFilterParams): Observable<PagedResult<AdminUserResponse>> {
    let params = new HttpParams();
    if (filters?.pageNumber != null) params = params.set('pageNumber', filters.pageNumber);
    if (filters?.pageSize != null)   params = params.set('pageSize',   filters.pageSize);
    if (filters?.name)               params = params.set('name',       filters.name);
    if (filters?.email)              params = params.set('email',      filters.email);
    if (filters?.isActive != null)   params = params.set('isActive',   filters.isActive);
    return this.http.get<PagedResult<AdminUserResponse>>(`${this.base}/admin/users`, { params });
  }

  toggleUserActive(userId: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/admin/users/${userId}/toggle-active`, {});
  }

  assignRole(userId: string, data: AssignRoleRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/admin/users/${userId}/roles`, data);
  }

  removeRole(userId: string, roleId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/admin/users/${userId}/roles/${roleId}`);
  }

  resetUserPassword(userId: string, data: ResetPasswordRequest): Observable<void> {
    return this.http.patch<void>(`${this.base}/admin/users/${userId}/reset-password`, data);
  }

  createAdminUser(data: CreateUserByAdminRequest): Observable<AdminUserResponse> {
    return this.http.post<AdminUserResponse>(`${this.base}/admin/users`, data);
  }

  updateAdminUser(userId: string, data: UpdateUserByAdminRequest): Observable<AdminUserResponse> {
    return this.http.put<AdminUserResponse>(`${this.base}/admin/users/${userId}`, data);
  }

  // ── Reports ────────────────────────────────────────────────────────────────

  getExpensesReport(year: number, month?: number): Observable<ExpensesReport> {
    let params = new HttpParams().set('year', year);
    if (month != null) params = params.set('month', month);
    return this.http.get<ExpensesReport>(`${this.base}/reports/expenses`, { params });
  }

  // ── Purge ─────────────────────────────────────────────────────────────────

  getEligiblePeriods(): Observable<EligiblePeriodResponse[]> {
    return this.http.get<EligiblePeriodResponse[]>(`${this.base}/purge/eligible-periods`);
  }

  exportPurgeCsv(periodId: string): Observable<Blob> {
    return this.http.get(`${this.base}/purge/${periodId}/export`, { responseType: 'blob' });
  }

  executePurge(periodId: string, csvFileName: string): Observable<PurgeResultResponse> {
    return this.http.post<PurgeResultResponse>(`${this.base}/purge/${periodId}`, { csvFileName });
  }

  getPurgeRecords(): Observable<PurgeRecordResponse[]> {
    return this.http.get<PurgeRecordResponse[]>(`${this.base}/purge/records`);
  }

  deletePurgeRecord(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/purge/records/${id}`);
  }
}
