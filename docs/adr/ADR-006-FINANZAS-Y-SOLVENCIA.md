# ADR-006: obligaciones, pagos, recibos y solvencia por corte

Estado: implementado en el quinto incremento. Fecha: 5 de noviembre de 2026, America/Guatemala.

## Contexto

La regla confirmada: no mostrar notas a familias/estudiantes sin cubrir las colegiaturas obligatorias hasta el corte de cada publicación. Importes, calendario, becas, ingreso tardío, convenios y políticas familiares **no están definidos** y no se inventan.

## Decisión

- **Conceptos** (`finance.FeeConcepts`): recurrente por cuota o único; el colegio marca cuáles cuentan para solvencia.
- **Calendario de cuotas por ciclo** (`finance.BillingPeriods`): secuencia, nombre y vencimiento definidos por el colegio. Nunca se deriva del «mes actual».
- **Cargos** (`finance.Charges`): obligación por inscripción con `ObligationKey = inscripción:concepto:cuota|once:ocurrencia`, única entre cargos no anulados. Dos lotes no pueden cobrar dos veces la misma obligación. Importe `decimal(19,4)`, moneda de `InstitutionOptions`, hasta dos decimales.
- **Emisión masiva con vista previa del servidor**: destinatarios = inscripciones activas por ubicación vigente (grados opcionales) con exclusiones; la confirmación envía el hash de la vista previa y se rechaza (`preview-changed`) si cambiaron destinatarios o cargos existentes.
- **Ajustes** (`ChargeAdjustments`): beca, descuento o exoneración con monto, motivo y autor; no se edita el importe. Una exoneración total deja la cuota cubierta sin pago.
- **Anulación** con motivo, solo sin pagos vigentes aplicados; libera la clave para reemitir.
- **Pagos** (`Payments` + `PaymentAllocations`): solo pagos confirmados registrados por caja, con aplicación explícita por cargo. La suma aplicada debe igualar el monto recibido: **no se guarda saldo a favor** sin regla aprobada. Clave de idempotencia por formulario: un reintento devuelve el mismo recibo.
- **Recibo** numerado con la secuencia SQL `finance.ReceiptNumbers` y una **instantánea JSON inmutable** (datos institucionales, pagador, líneas, emisor). El logo de comprobantes se copia por contenido (SHA-256) a `finance.DocumentAssets`; cambiar la marca no altera recibos emitidos. El recibo indica que no es factura fiscal. Pueden existir saltos de numeración si una transacción se revierte después de tomar el número.
- **Reversión** con permiso propio y motivo: conserva pago, aplicaciones y recibo (marcado revertido) y deja de cubrir obligaciones.
- **Concurrencia**: operaciones de dinero bajo `sp_getapplock` transaccional `SchoolPlatform.Finance` y rowversion por cargo/pago. Tres pagos simultáneos por el saldo completo producen un solo éxito.
- **Contexto de la web**: configuración y solvencia siguen el ciclo explícito de la URL, incluso con historial, reintentos y respuestas fuera de orden. Un ciclo inexistente o un corte de otro ciclo genera un error; no se sustituye por otro. Un fallo de consulta retira los datos anteriores y no se interpreta como falta de cargos/cortes/pagos. Ver `QA_FLUJOS_2026-11-05.md`.
- **Solvencia** (`ClearanceService`, sin caché): para un corte (`finance.ClearanceCutoffs`, «cuotas 1..N»), cada cuota requerida debe tener al menos un cargo no anulado de un concepto de solvencia para la inscripción; todos esos cargos (y los únicos de solvencia vencidos hasta la última cuota) deben tener saldo cero. Resultados: `Clear`, `Blocked`, `PendingFinancialSetup` (calendario incompleto, sin conceptos recurrentes de solvencia o cuotas sin emitir) y `NotEnrolled`. Un saldo vacío nunca es solvencia. Se evalúa por estudiante y matrícula; la deuda de un hermano no afecta.
- **Permisos**: `finance.read`, `finance.charges`, `finance.collect`, `finance.reverse`. Perfiles «Caja» y «Administración financiera»; Administración institucional incluye todo.
- **Autoservicio**: un encargado con la facultad `Finance` consulta estado de cuenta y recibos de ese estudiante. El acceso del propio estudiante a su detalle financiero queda **pendiente de política**; se le indica consultar con su encargado o caja.

## Pendientes conscientes

- Planes/versiones por grado, becas recurrentes, prorrateos e ingreso tardío automático: el colegio hoy los resuelve explícitamente (emitir/no emitir/exonerar con motivo).
- Comprobantes subidos por familias (PendingReview), pasarelas, cierre de caja y conciliación por medio.
- Saldo a favor/anticipos y su aplicación.
- Factura fiscal (FEL): fuera de alcance; no se simula.
- Conexión con publicación de notas: `PublicationBatch` deberá referenciar un `ClearanceCutoff` y consultar `ClearanceService` en cada lectura familiar/estudiantil.
