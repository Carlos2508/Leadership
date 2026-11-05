# Finanzas y solvencia pendientes

No hay tablas, cargos, pagos ni solvencia implementados. Las vistas visuales y datos de muestra no generan obligaciones ni recibos.

La regla de la fuente es exigir todas las colegiaturas aplicables completamente cubiertas hasta el corte configurado en cada publicación. Un abono no equivale a obligación cubierta. No codificar marzo como mes fijo. No interpretar ausencia de pagos importados como solvencia positiva ni como deuda.

Antes del portal académico se necesitan: conceptos y planes versionados; obligaciones por estudiante/matrícula; generación masiva con preview/idempotencia; pagos y aplicaciones; reversiones auditadas; recibos; política de becas/ajustes; contrato de solvencia por estudiante y publicación. El corte, fechas efectivas, ingreso tardío, prorrateos y convenios requieren política explícita.

No habilitar una consulta de notas o boleta pública/familiar que ignore este control por estar completo su diseño visual. Los logos/pies institucionales de recibos se toman de `InstitutionOptions`, y la emisión conservará su snapshot para que cambios futuros de marca no reescriban comprobantes históricos.
