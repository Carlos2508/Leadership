import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
import { InstitutionService } from '../core/institution';
import { formatDateOnly } from '../core/people-api';
import { ReceiptView as Receipt, methodLabels, money } from '../core/finance-api';
import { Icon } from '../shared/icon';

/**
 * Printable receipt rendered only from the immutable snapshot stored at issue time (institution data and logo copy),
 * never from the current configuration. Used by staff and by guardians with the finance faculty.
 */
@Component({
  imports: [RouterLink, Icon],
  template: `
    <div class="receipt-page">
      <div class="receipt-toolbar no-print">
        <a class="back-link" [routerLink]="backLink()"><app-icon name="arrow" />Volver</a>
        @if (receipt()) {
          <span class="toolbar-actions">
            <a class="button outline" [href]="pdf()" target="_blank" rel="noopener">Descargar PDF</a>
            <button class="button" (click)="print()">Imprimir</button>
          </span>
        }
      </div>
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      } @else if (receipt(); as r) {
        <article class="receipt" aria-labelledby="receipt-title">
          @if (r.reversed) {
            <p class="receipt-reversed" role="note">
              PAGO REVERTIDO el {{ instant(r.reversedAt) }}. Este recibo no acredita pago vigente.
            </p>
          }
          <header class="receipt-header">
            <img [src]="logo()" alt="" />
            <div>
              <strong class="receipt-institution">{{ r.receipt.institutionName }}</strong>
              @if (r.receipt.legalName) {
                <span>{{ r.receipt.legalName }}</span>
              }
              @if (r.receipt.taxId) {
                <span>NIT {{ r.receipt.taxId }}</span>
              }
              @if (r.receipt.address) {
                <span>{{ r.receipt.address }}</span>
              }
              @if (r.receipt.phone || r.receipt.email) {
                <span>{{ r.receipt.phone }} {{ r.receipt.email }}</span>
              }
            </div>
            <div class="receipt-number">
              <h1 id="receipt-title">Recibo de pago</h1>
              <strong>No. {{ r.receipt.number }}</strong>
            </div>
          </header>
          <dl class="receipt-meta">
            <div><dt>Recibido de</dt><dd>{{ r.receipt.payerName }}</dd></div>
            <div><dt>Fecha de pago</dt><dd>{{ date(r.receipt.receivedOn) }}</dd></div>
            <div><dt>Medio</dt><dd>{{ methods[r.receipt.method] }}{{ r.receipt.reference ? ' · ' + r.receipt.reference : '' }}</dd></div>
            <div><dt>Emitido</dt><dd>{{ instant(r.receipt.issuedAt) }}{{ studentId() ? '' : ' · ' + r.receipt.issuedBy }}</dd></div>
          </dl>
          <table class="receipt-lines">
            <caption class="sr-only">Obligaciones cubiertas por este pago</caption>
            <thead>
              <tr><th scope="col">Detalle</th><th scope="col" class="numeric">Monto</th></tr>
            </thead>
            <tbody>
              @for (line of r.receipt.lines; track $index) {
                <tr>
                  <td>
                    <strong>{{ line.description }}</strong>
                    <small>{{ line.studentName }} · {{ line.studentCode }}</small>
                  </td>
                  <td class="numeric">{{ format(line.amount) }}</td>
                </tr>
              }
            </tbody>
            <tfoot>
              <tr><th scope="row">Total</th><td class="numeric"><strong>{{ format(r.receipt.amount) }}</strong></td></tr>
            </tfoot>
          </table>
          <p class="receipt-note">
            Comprobante interno de pago. No es una factura fiscal.
          </p>
          @if (r.receipt.footer) {
            <footer class="receipt-footer">{{ r.receipt.footer }}</footer>
          }
        </article>
      } @else {
        <p class="loading-state" role="status">Cargando recibo…</p>
      }
    </div>
  `,
})
export class ReceiptView {
  readonly id = input.required<string>();
  /** When present, the receipt is read through the guardian self-service route for this student. */
  readonly studentId = input<string | undefined>();
  readonly api = inject(AccessApi);
  readonly school = inject(InstitutionService).current;
  readonly receipt = signal<Receipt | null>(null);
  readonly error = signal('');
  readonly methods = methodLabels;
  readonly backLink = computed(() =>
    this.studentId() ? ['/workspace/my/students', this.studentId()!] : ['/workspace/finance'],
  );
  readonly logo = computed(() => {
    const sha = this.receipt()?.receipt.logoSha256;
    const student = this.studentId();
    return student ? `/api/v1/me/students/${student}/receipt-assets/${sha}` : `/api/v1/finance/assets/${sha}`;
  });

  readonly pdf = computed(() => {
    const student = this.studentId();
    return student
      ? `/api/v1/me/students/${student}/receipts/${this.id()}/receipt.pdf`
      : `/api/v1/finance/payments/${this.id()}/receipt.pdf`;
  });

  constructor() {
    queueMicrotask(() => void this.load());
  }
  async load(): Promise<void> {
    const student = this.studentId();
    try {
      this.receipt.set(
        await this.api.get<Receipt>(
          student
            ? `/api/v1/me/students/${student}/receipts/${this.id()}`
            : `/api/v1/finance/payments/${this.id()}`,
        ),
      );
    } catch (error) {
      this.error.set(this.api.message(error));
    }
  }
  format(value: number): string {
    return money(value, this.receipt()?.receipt.currency ?? this.school.locale.currency, this.school.locale.culture);
  }
  date(value: string): string {
    return formatDateOnly(value, this.school.locale.culture);
  }
  instant(value: string | null): string {
    return this.api.date(value);
  }
  print(): void {
    window.print();
  }
}
