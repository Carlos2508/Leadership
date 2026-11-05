export interface ConceptView {
  id: string;
  code: string;
  name: string;
  isRecurring: boolean;
  countsForClearance: boolean;
  isActive: boolean;
  version: string;
}
export interface BillingPeriodView {
  id: string;
  sequence: number;
  label: string;
  dueOn: string;
  charges: number;
}
export interface CutoffView {
  id: string;
  name: string;
  throughSequence: number;
  createdAt: string;
}
export interface FinanceSetup {
  concepts: ConceptView[];
  periods: BillingPeriodView[];
  cutoffs: CutoffView[];
  currency: string;
}
export interface ChargeLine {
  id: string;
  academicYearId: string;
  year: string;
  concept: string;
  countsForClearance: boolean;
  periodSequence: number | null;
  period: string | null;
  description: string;
  amount: number;
  adjustments: number;
  paid: number;
  balance: number;
  dueOn: string;
  status: 'Pending' | 'Partial' | 'Paid' | 'Waived' | 'Voided';
  version: string;
}
export interface PaymentSummary {
  id: string;
  receiptNumber: number;
  receivedOn: string;
  amount: number;
  method: string;
  payerName: string;
  reversed: boolean;
  version: string;
}
export interface Statement {
  studentId: string;
  studentName: string;
  studentCode: string;
  currency: string;
  charges: ChargeLine[];
  payments: PaymentSummary[];
  totalBalance: number;
}
export interface ReceiptLine {
  studentName: string;
  studentCode: string;
  concept: string;
  period: string | null;
  description: string;
  amount: number;
}
export interface ReceiptSnapshot {
  number: number;
  institutionName: string;
  legalName: string;
  taxId: string;
  address: string;
  phone: string;
  email: string;
  logoSha256: string;
  footer: string;
  payerName: string;
  receivedOn: string;
  issuedAt: string;
  method: string;
  reference: string;
  currency: string;
  amount: number;
  lines: ReceiptLine[];
  issuedBy: string;
}
export interface ReceiptView {
  paymentId: string;
  receipt: ReceiptSnapshot;
  reversed: boolean;
  reversedAt: string | null;
  reversalReason: string | null;
}
export interface GenerationRow {
  enrollmentId: string;
  studentId: string;
  studentName: string;
  studentCode: string;
  grade: string | null;
  section: string | null;
  outcome: 'Create' | 'Exists' | 'Excluded';
}
export interface GenerationPreview {
  previewHash: string;
  rows: GenerationRow[];
  toCreate: number;
  existing: number;
  excluded: number;
  total: number;
  currency: string;
}
export interface ClearanceRow {
  studentId: string;
  studentName: string;
  studentCode: string;
  grade: string | null;
  section: string | null;
  status: ClearanceStatus;
  pendingBalance: number;
  missingPeriods: number;
}
export type ClearanceStatus = 'Clear' | 'Blocked' | 'PendingFinancialSetup' | 'NotEnrolled';
export interface ClearanceReport {
  cutoffId: string;
  cutoff: string;
  clear: number;
  blocked: number;
  pendingSetup: number;
  rows: ClearanceRow[];
}

export const methodLabels: Record<string, string> = {
  Cash: 'Efectivo',
  Transfer: 'Transferencia',
  Deposit: 'Depósito',
  Card: 'Tarjeta',
  Check: 'Cheque',
  Other: 'Otro',
};
export const chargeStatusLabels: Record<string, string> = {
  Pending: 'Pendiente',
  Partial: 'Abonado',
  Paid: 'Pagado',
  Waived: 'Exonerado',
  Voided: 'Anulado',
};
export const clearanceLabels: Record<ClearanceStatus, string> = {
  Clear: 'Solvente',
  Blocked: 'Con saldo pendiente',
  PendingFinancialSetup: 'Configuración incompleta',
  NotEnrolled: 'Sin inscripción',
};
export const adjustmentLabels: Record<string, string> = {
  Scholarship: 'Beca',
  Discount: 'Descuento',
  Waiver: 'Exoneración',
};

export function money(value: number, currency: string, culture: string): string {
  return new Intl.NumberFormat(culture, { style: 'currency', currency }).format(value);
}

/** Random key so a retried submission of the same payment form is recognized by the server. */
export function operationKey(): string {
  return crypto.randomUUID().replaceAll('-', '');
}
