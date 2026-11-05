import { Component, ElementRef, ViewChild, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AccessApi, PageResult } from '../core/access-api';
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
  readonly student = input<string | undefined>();
  readonly api = inject(AccessApi);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  @ViewChild('cashDialog') private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;

  readonly results = signal<PersonSummary[]>([]);
  readonly statement = signal<Statement | null>(null);
  readonly searching = signal(false);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
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

  constructor() {
    queueMicrotask(() => {
      const id = this.student();
      if (id) void this.loadStatement(id);
    });
  }

  money(value: number): string {
    return money(value, this.statement()?.currency ?? this.school.locale.currency, this.school.locale.culture);
  }
  date(value: string | null): string {
    return formatDateOnly(value, this.school.locale.culture);
  }

  async find(): Promise<void> {
    if (!this.search.trim()) return;
    this.searching.set(true);
    this.error.set('');
    try {
      const query = new URLSearchParams({ search: this.search, role: 'student', status: 'all', page: '1' });
      this.results.set((await this.api.get<PageResult<PersonSummary>>(`/api/v1/people?${query}`)).items);
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.searching.set(false);
    }
  }

  async choose(person: PersonSummary): Promise<void> {
    this.results.set([]);
    this.notice.set('');
    this.lastReceipt.set(null);
    await this.router.navigate([], { queryParams: { student: person.id } });
    await this.loadStatement(person.id);
  }

  async loadStatement(id: string): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      this.statement.set(await this.api.get<Statement>(`/api/v1/finance/students/${id}/statement`));
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
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
      this.notice.set(message);
      await this.loadStatement(statement.studentId);
      requestAnimationFrame(() => this.noticeRef?.nativeElement.focus());
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
