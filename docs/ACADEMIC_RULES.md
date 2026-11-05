# Reglas académicas

La biblioteca `src/Modules/Assessment` contiene un cálculo puro por materia, todavía interno. Desde el séptimo incremento existen la captura de notas de período, sus revisiones y la publicación por período con corte de solvencia (ver ADR-007); **no se publican promedios anuales ni redondeos**.

Con exactamente cuatro bimestres numéricos válidos (0 a 100), la ordinaria es su suma dividida entre cuatro. Se compara con 60 por materia. Un cero es una nota; un bimestre `null` mantiene el resultado pendiente, sin convertirlo a cero. Las notas inválidas y los arreglos de tres/cinco bimestres se rechazan.

Una `AuthorizedRecovery` representa una resolución ya validada por un futuro caso de uso. Su nota reemplaza la efectiva y conserva la ordinaria; exige un identificador de resolución. El cálculo no afirma que conocer un GUID otorgue autorización. Cuando exista publicación, el servidor resolverá la autorización, estado e historial de esa resolución.

La comparación sin redondeo es una propuesta de desarrollo de la fuente (§11), pendiente de aprobación para publicar. Lo mismo aplica al resultado inferior en recuperación: el cálculo acepta una resolución explícita; aún no existe flujo que autorice/publicite esa decisión. No habilitar promoción o boletas desde este cálculo aislado.

Pruebas: cuatro bimestres, umbral exacto, promedio 59.75 sin redondeo, cero, pendientes, límites inválidos, recuperación superior/inferior, conservación ordinaria y rechazo de recuperación con materia aprobada/incompleta.

Siguiente diseño: política versionada, estados de componente, matrícula efectiva y equivalencias, publicación transaccional, corte de solvencia y snapshots de boletas. No copiar la regla a Angular ni Flutter.
