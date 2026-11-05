# Finanzas y solvencia

Implementado en el quinto incremento (FIN-001…004 y contrato de solvencia). Decisión en `adr/ADR-006-FINANZAS-Y-SOLVENCIA.md`. La regla vigente: exigir todas las colegiaturas aplicables completamente cubiertas hasta el corte configurado en cada publicación. Un abono no equivale a obligación cubierta. No se codifica un mes fijo. La ausencia de cargos no es solvencia.

## Operación

1. **Configuración** (`/workspace/finance/setup?year=…`, `finance.charges`): conceptos (recurrente/único, cuenta para solvencia), calendario de cuotas del ciclo y cortes de solvencia («exigir cuotas hasta N»).
2. **Emitir cargos**: concepto, cuota, importe, vencimiento opcional y grados → vista previa (por emitir / ya emitidos / excluidos, total) → excluir con casilla → «Confirmar emisión». Repetir la emisión no duplica.
3. **Caja** (`/workspace/finance?student=…`, `finance.collect`): buscar estudiante, ver estado de cuenta, «Registrar pago» con propuesta del cargo más antiguo; montos editables; la suma debe ser exacta. Al confirmar se muestra el número de recibo.
4. **Recibo** (`/workspace/finance/receipts/{id}`): imprimible, construido solo con la instantánea de emisión.
5. **Ajustes y anulaciones** (`finance.charges`) y **reversiones** (`finance.reverse`) con motivo obligatorio y auditoría.
6. **Solvencia** (`/workspace/finance/clearance?year=…&cutoff=…`): resumen y lista por estudiante, calculado al momento.
7. **Encargado** con facultad `Finance`: `/workspace/my/students/{id}` muestra saldo, cargos y recibos.

## API

| Ruta | Requisito |
| --- | --- |
| GET `/api/v1/finance/years/{yearId}/setup`, `/concepts`, `/students/{id}/statement`, `/payments?from&to`, `/payments/{id}`, `/assets/{sha}` | `finance.read` |
| GET `/api/v1/finance/cutoffs/{id}/report?gradeId&status`, `/cutoffs/{id}/students/{studentId}` | `finance.read` |
| POST `/api/v1/finance/concepts`, `/concepts/{id}`, `/years/{yearId}/periods`, `/periods/{id}`, `/years/{yearId}/cutoffs` | `finance.charges` + CSRF |
| POST `/api/v1/finance/charges/preview`, `/charges/generate`, `/charges`, `/charges/{id}/adjustments`, `/charges/{id}/void` | `finance.charges` |
| POST `/api/v1/finance/payments` | `finance.collect` |
| POST `/api/v1/finance/payments/{id}/reverse` | `finance.reverse` |
| GET `/api/v1/me/students/{id}/statement`, `/receipts/{paymentId}`, `/receipt-assets/{sha}` | Cuenta activa + encargado vigente con facultad `Finance`; si no, 404 |

## Auditoría

`FeeConceptCreated/Updated`, `BillingPeriodAdded/Updated`, `ClearanceCutoffCreated`, `ChargesGenerated`, `ChargeCreated`, `ChargeAdjusted`, `ChargeVoided`, `PaymentRecorded`, `PaymentReversed`, en la misma transacción del cambio. Sin nombres de personas en el detalle.

## Pruebas

`FinanceSqlTests` (5): emisión con vista previa obligatoria e idempotente; pagos exactos, recibos consecutivos, idempotencia y un solo logo almacenado; solvencia `Clear/Blocked/PendingFinancialSetup/NotEnrolled` con exoneración, anulación y reversión; pagos concurrentes sin sobrepago; permisos de caja/financieros y facultad del encargado. `e2e/finance.spec.ts` recorre configuración, emisión, caja, recibo con logo, solvencia y vista del encargado en escritorio y móvil.

## Pendiente

Planes por versión, becas recurrentes, prorrateos e ingreso tardío automáticos; comprobantes subidos por familias; saldo a favor; cierre de caja y conciliación; pasarelas; factura fiscal; acceso del estudiante a su detalle financiero; conexión con publicación de notas.
