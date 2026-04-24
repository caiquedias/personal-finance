// ── Auth ──────────────────────────────────────────────────────────────────────

export interface RegisterRequest {
  name:     string;
  email:    string;
  password: string;
}

export interface LoginRequest {
  email:    string;
  password: string;
}

export interface LoginResponse {
  token: string;
  name:  string;
  email: string;
}

export interface UserResponse {
  id:       string;
  name:     string;
  email:    string;
  isActive: boolean;
}

// ── Lookup ────────────────────────────────────────────────────────────────────

export interface LookupItem {
  id:           number;
  name:         string;
  description?: string;
  isSystemSeed: boolean;
}

// ── Category ──────────────────────────────────────────────────────────────────

export interface CategoryResponse {
  id:       string;
  userId:   string | null;
  name:     string;
  color:    string;
  icon:     string | null;
  isGlobal: boolean;
  isActive: boolean;
}

export interface CreateCategoryRequest {
  name:     string;
  color:    string;
  icon?:    string;
  isGlobal?: boolean;
}

export interface UpdateCategoryRequest {
  name:  string;
  color: string;
  icon?: string;
}

// ── Period ────────────────────────────────────────────────────────────────────

export interface PeriodResponse {
  id:       string;
  userId:   string;
  year:     number;
  month:    number;
  isActive: boolean;
}

export interface CreatePeriodRequest {
  year:  number;
  month: number;
}

export interface PeriodSummary {
  periodId:             string;
  userId:               string;
  year:                 number;
  month:                number;
  totalIncome:          number;
  totalExpense:         number;
  totalPaid:            number;
  totalOwed:            number;
  totalFirstFortnight:  number;
  totalSecondFortnight: number;
  balance:              number;
}

// ── Expense ───────────────────────────────────────────────────────────────────

export enum SourceType    { Parental = 1, Personal = 2 }
export enum FortnightType { First = 1,    Second = 2   }
export enum PaymentStatus { Pending = 1,  Paid = 2, Cancelled = 3, Partial = 4 }

export interface ExpenseResponse {
  id:             string;
  periodId:       string;
  userId:         string;
  categoryId:     string;
  sourceType:     SourceType;
  fortnightType:  FortnightType;
  paymentStatus:  PaymentStatus;
  description:    string;
  amount:         number;
  dueDate:        string;  // ISO date string
  paymentDate:    string | null;
  notes:          string | null;
  isActive:       boolean;
}

export interface CreateExpenseRequest {
  periodId:      string;
  categoryId:    string;
  sourceType:    SourceType;
  fortnightType: FortnightType;
  description:   string;
  amount:        number;
  dueDate:       string;
  notes?:        string;
}

export interface UpdateExpenseRequest {
  categoryId:    string;
  sourceType:    SourceType;
  fortnightType: FortnightType;
  description:   string;
  amount:        number;
  dueDate:       string;
  notes?:        string;
  status:        PaymentStatus;
}

export interface MarkAsPaidRequest {
  paymentDate: string;
}

// ── Pagination ────────────────────────────────────────────────────────────────

export interface PagedResult<T> {
  items:      T[];
  totalCount: number;
  pageNumber: number;
  pageSize:   number;
}

export interface ExpenseFilterParams {
  pageNumber?:    number;
  pageSize?:      number;
  description?:   string;
  categoryId?:    string;
  paymentStatus?: PaymentStatus;
  fortnightType?: FortnightType;
  sourceType?:    SourceType;
}

export interface IncomeFilterParams {
  pageNumber?:    number;
  pageSize?:      number;
  description?:   string;
  fortnightType?: FortnightType;
}

// ── Income ────────────────────────────────────────────────────────────────────

export interface IncomeResponse {
  id:            string;
  periodId:      string;
  userId:        string;
  fortnightType: FortnightType;
  description:   string;
  amount:        number;
  receivedAt:    string;
  notes:         string | null;
  isActive:      boolean;
}

export interface CreateIncomeRequest {
  periodId:      string;
  fortnightType: FortnightType;
  description:   string;
  amount:        number;
  receivedAt:    string;
  notes?:        string;
}

// ── Admin ─────────────────────────────────────────────────────────────────────

export interface AdminUserFilterParams {
  pageNumber?: number;
  pageSize?:   number;
  name?:       string;
  email?:      string;
  isActive?:   boolean;
}

export interface AdminUserResponse {
  id:        string;
  name:      string;
  email:     string;
  isActive:  boolean;
  isDeleted: boolean;
  createdAt: string;
  roles:     string[];
}

export interface AssignRoleRequest       { roleId: number; }
export interface ResetPasswordRequest    { newPassword: string; }
export interface CreateUserByAdminRequest { name: string; email: string; password: string; }
export interface UpdateUserByAdminRequest { name: string; }
export interface CreatePaymentStatusRequest { name: string; description: string; }
export interface CreateSourceTypeRequest    { name: string; }
export interface CreateFortnightTypeRequest { name: string; }
export interface UpdatePaymentStatusRequest { name: string; description: string; }
export interface UpdateSourceTypeRequest    { name: string; }
export interface UpdateFortnightTypeRequest { name: string; }

// ── Helpers ───────────────────────────────────────────────────────────────────

export interface ApiError {
  status:  number;
  error:   string;
  message: string;
  traceId: string;
}

export const MONTH_NAMES = [
  'Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
  'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro'
];

export const PAYMENT_STATUS_LABELS: Record<PaymentStatus, string> = {
  [PaymentStatus.Pending]:   'Pendente',
  [PaymentStatus.Paid]:      'Pago',
  [PaymentStatus.Cancelled]: 'Cancelado',
  [PaymentStatus.Partial]:   'Parcial',
};

export const SOURCE_TYPE_LABELS: Record<SourceType, string> = {
  [SourceType.Parental]: 'Parental',
  [SourceType.Personal]: 'Própria',
};

export const FORTNIGHT_TYPE_LABELS: Record<FortnightType, string> = {
  [FortnightType.First]:  '1ª Quinzena',
  [FortnightType.Second]: '2ª Quinzena',
};

// ── Reports ───────────────────────────────────────────────────────────────────

export interface ExpenseByCategoryItem {
  categoryId:    string;
  categoryName:  string;
  categoryColor: string;
  total:         number;
}

export interface ExpensesReport {
  year:  number;
  month: number | null;
  items: ExpenseByCategoryItem[];
}
