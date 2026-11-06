import { Component, ElementRef, ViewChild, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { distinctUntilChanged, map } from 'rxjs';
import { AccessApi, PageResult } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { PersonSummary, formatDateOnly } from '../core/people-api';
import { todayIn } from '../core/academic-api';
import {
  ChargeLine,
  PaymentSummary,
  ReceiptView,
  Statement,
  adjustmentLabels,
  chargeStatusLabels,
  methodLabels,
  money,
  operationKey,
} from '../core/finance-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { CyclePipe } from '../shared/cycle.pipe';

type Mode = 'pay' | 'adjust' | 'void' | 'reverse';

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink, CyclePipe],
  templateUrl: './finance-cashier.html',
})
export class FinanceCashier {
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private statementRequest = 0;
  private todayRequest = 0;
  @ViewChild('cashDialog') private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;

  readonly results = signal<PersonSummary[]>([]);
  readonly statement = signal<Statement | null>(null);
  readonly searching = signal(false);
  readonly searched = signal(false);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly statementError = signal('');
  readonly selectedStudent = signal<string | null>(null);
  readonly notice = signal('');
  readonly dialogError = signal('');
  readonly mode = signal<Mode>('pay');
  readonly lastReceipt = signal<ReceiptView | null>(null);
  readonly selectedCharge = signal<ChargeLine | null>(null);
  readonly selectedPayment = signal<PaymentSummary | null>(null);
  readonly canCollect = computed(() => this.session.can('finance.collect'));
  readonly canCharge = computed(() => this.session.can('finance.charges'));
  readonly canReverse = computed(() => this.session.can('finance.reverse'));
  readonly openCharges = computed(() => (this.statement()?.charges ?? []).filter((x) => x.balance > 0));
  readonly methods = Object.entries(methodLabels);
  readonly statusLabels = chargeStatusLabels;
  readonly adjustmentKinds = Object.entries(adjustmentLabels);
  readonly methodLabels = methodLabels;

  search = '';
  pay = { payer: '', method: 'Cash', reference: '', date: '', note: '', key: '' };
  amounts: Record<string, number | null> = {};
  adjust = { kind: 'Scholarship', amount: null as number | null };
  reason = '';

  readonly today = signal<PaymentSummary[]>([]);
  readonly todayLoading = signal(false);
  readonly todayError = signal('');
  readonly todayTotal = computed(() => this.today().filter((x) => !x.reversed).reduce((sum, x) => sum + x.amount, 0));

  constructor() {
    this.route.queryParamMap.pipe(
      map(params => params.get('student')),
      distinctUntilChanged(),
      takeUntilDestroyed(),
    ).subscribe(id => {
      this.selectedStudent.set(id);
      this.statementRequest++;
      this.statement.set(null);
      this.statementError.set('');
      this.loading.set(false);
      this.error.set('');
      this.notice.set('');
      this.lastReceipt.set(null);
      this.selectedCharge.set(null);
      this.selectedPayment.set(null);
      this.amounts = {};
      this.dialogError.set('');
      this.dialog?.nativeElement.close();
      if (id) void this.loadStatement(id);
    });
    void this.loadToday();
  }
  /** The day's receipts (institution date) so the cashier can reprint or review without searching. */
  async loadToday(): Promise<void> {
    const request = ++this.todayRequest;
    this.todayLoading.set(true);
    this.todayError.set('');
    this.today.set([]);
    try {
      const receipts = await this.api.get<PaymentSummary[]>('/api/v1/finance/payments');
      if (request === this.todayRequest) this.today.set(receipts);
    } catch (error) {
      if (request === this.todayRequest) this.todayError.set(this.api.message(error));
    } finally {
      if (request === this.todayRequest) this.todayLoading.set(false);
    }
  }

  money(value: number): string {
    return money(value, this.statement()?.currency ?? this.school.locale.currency, this.school.locale.culture);
  }
  date(value: string | null): string {
    return formatDateOnly(value, this.school.locale.culture);
  }

  async find(): Promise<void> {
    if (!this.search.trim()) return;
    const search = this.search.trim();
    this.searching.set(true);
    this.searched.set(false);
    this.results.set([]);
    this.error.set('');
    try {
      const query = new URLSearchParams({ search, role: 'student', status: 'all', page: '1' });
      const result = await this.api.get<PageResult<PersonSummary>>(`/api/v1/people?${query}`);
      if (search === this.search.trim()) {
        this.results.set(result.items);
        this.searched.set(true);
      }
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.searching.set(false);
    }
  }

  async choose(person: PersonSummary): Promise<void> {
    this.results.set([]);
    this.searched.set(false);
    await this.router.navigate([], { queryParams: { student: person.id } });
  }

  async retryStatement(): Promise<void> {
    const id = this.selectedStudent();
    if (id) await this.loadStatement(id);
  }

  async loadStatement(id: string): Promise<void> {
    if (id !== this.selectedStudent()) return;
    const request = ++this.statementRequest;
    this.loading.set(true);
    this.statementError.set('');
    this.statement.set(null);
    try {
      const statement = await this.api.get<Statement>(`/api/v1/finance/students/${id}/statement`);
      if (request === this.statementRequest) this.statement.set(statement);
    } catch (error) {
      if (request === this.statementRequest) this.statementError.set(this.api.message(error));
    } finally {
      if (request === this.statementRequest) this.loading.set(false);
    }
  }

  allocated(): number {
    return Object.values(this.amounts).reduce<number>((sum, x) => sum + (Number(x) || 0), 0);
  }

  openPay(): void {
    this.mode.set('pay');
    this.dialogError.set('');
    this.pay = {
      payer: '',
      method: 'Cash',
      reference: '',
      date: todayIn(this.school.locale.timeZone),
      note: '',
      key: operationKey(),
    };
    // Proposal: oldest obligations first. The cashier can change each amount before confirming.
    this.amounts = {};
    for (const charge of this.openCharges()) this.amounts[charge.id] = null;
    const first = this.openCharges()[0];
    if (first) this.amounts[first.id] = first.balance;
    this.dialog.nativeElement.showModal();
  }
  fill(charge: ChargeLine): void {
    this.amounts[charge.id] = this.amounts[charge.id] ? null : charge.balance;
  }
  openAdjust(charge: ChargeLine): void {
    this.selectedCharge.set(charge);
    this.adjust = { kind: 'Scholarship', amount: null };
    this.open('adjust');
  }
  openVoid(charge: ChargeLine): void {
    this.selectedCharge.set(charge);
    this.open('void');
  }
  openReverse(payment: PaymentSummary): void {
    this.selectedPayment.set(payment);
    this.open('reverse');
  }
  private open(mode: Mode): void {
    this.mode.set(mode);
    this.reason = '';
    this.dialogError.set('');
    this.dialog.nativeElement.showModal();
  }
  closeDialog(): void {
    if (!this.busy()) this.dialog.nativeElement.close();
  }
  onCancel(event: Event): void {
    if (this.busy()) event.preventDefault();
    else this.closeDialog();
  }

  title(): string {
    return {
      pay: 'Registrar pago',
      adjust: 'Beca, descuento o exoneración',
      void: 'Anular cargo',
      reverse: 'Revertir pago',
    }[this.mode()];
  }

  async submit(): Promise<void> {
    const statement = this.statement();
    if (!statement || this.busy()) return;
    this.dialogError.set('');
    this.busy.set(true);
    try {
      let message = '';
      switch (this.mode()) {
        case 'pay': {
          const allocations = Object.entries(this.amounts)
            .filter(([, amount]) => Number(amount) > 0)
            .map(([chargeId, amount]) => ({ chargeId, amount: Number(amount) }));
          if (!allocations.length) {
            this.dialogError.set('Indica el monto que se aplica a al menos un cargo.');
            return;
          }
          const receipt = await this.api.post<ReceiptView>('/api/v1/finance/payments', {
            idempotencyKey: this.pay.key,
            payerName: this.pay.payer,
            method: this.pay.method,
            reference: this.pay.reference || null,
            receivedOn: this.pay.date,
            note: this.pay.note || null,
            amount: Math.round(this.allocated() * 100) / 100,
            allocations,
          });
          this.lastReceipt.set(receipt);
          message = `Pago registrado. Recibo No. ${receipt.receipt.number}.`;
          break;
        }
        case 'adjust': {
          const charge = this.selectedCharge()!;
          await this.api.post(`/api/v1/finance/charges/${charge.id}/adjustments`, {
            version: charge.version,
            kind: this.adjust.kind,
            amount: this.adjust.amount,
            reason: this.reason,
          });
          message = 'El ajuste quedó registrado con su motivo.';
          break;
        }
        case 'void': {
          const charge = this.selectedCharge()!;
          await this.api.post(`/api/v1/finance/charges/${charge.id}/void`, {
            version: charge.version,
            reason: this.reason,
          });
          message = 'El cargo se anuló. Se conserva en el historial.';
          break;
        }
        case 'reverse': {
          const payment = this.selectedPayment()!;
          await this.api.post(`/api/v1/finance/payments/${payment.id}/reverse`, {
            version: payment.version,
            reason: this.reason,
          });
          message = `El pago con recibo No. ${payment.receiptNumber} fue revertido; los cargos vuelven a tener saldo.`;
          break;
        }
      }
      this.busy.set(false);
      this.dialog.nativeElement.close();
      await this.loadStatement(statement.studentId);
      void this.loadToday();
      const receipt = this.mode() === 'pay' ? this.lastReceipt() : null;
      if (receipt) {
        // The cashier usually prints right away: offer the receipt in the same confirmation.
        if (await this.alerts.done('Pago registrado', `Recibo No. ${receipt.receipt.number} · ${this.money(receipt.receipt.amount)}`, 'Ver e imprimir recibo'))
          await this.router.navigate(['/workspace/finance/receipts', receipt.paymentId]);
      } else this.alerts.success(message);
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
