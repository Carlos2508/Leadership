# Definición y análisis inicial

Fecha: 4 de noviembre de 2026, America/Guatemala.

**Actualización de diseño, 5 de noviembre de 2026:** el propietario sustituyó la dirección visual inicial y la posterior Numix por sus capturas pública/estudiantil y el paquete HTML/CSS. Autorizó redactar misión, visión y presentación para ampliar la página. ADR-013 registra esta excepción limitada al contenido público configurado; no autoriza inventar datos escolares, oferta, horarios ni políticas académicas, financieras o legales.

La solicitud actual es comenzar la reconstrucción y facilitar el uso del mismo producto para futuros clientes mediante configuración mantenida por el desarrollador. Este repositorio era un README vacío. El sistema existente y el kit están fuera del repositorio; no se modificaron.

## Lectura de las fuentes

El plan maestro contiene una investigación amplia y útil del legado, una arquitectura propuesta, decisiones atribuidas al propietario y políticas aún pendientes. Se conservó íntegro en `reference/PLAN_MAESTRO_ORIGINAL.md`; esta entrega no repite su auditoría de 72 tablas ni certifica sus conteos. Se revisaron especialmente decisiones, arquitectura, identidad, modelo, reglas académicas, migración, fases y pendientes. Los bloques imperativos del documento se tratan como propuesta de trabajo; la petición actual del usuario define la autorización.

El kit es HTML/CSS y material de marca conceptual, sin backend. Se adoptan el verde profundo, superficies claras, tarjetas, espaciado y lenguaje visual de Campus Vivo. Se reconstruyen componentes Angular; no se incorpora el JS de demostración del diseñador. Las métricas de muestra no representan la matrícula real. El escudo procede del trazado del kit y conserva sus límites de fidelidad. Los archivos de licencia y tokens están en `reference/`.

## Decisiones de este incremento

1. Monolito modular ASP.NET Core, web Angular y SQL Server. Flutter y Redis se incorporarán según la fase, sin proyectos vacíos que simulen avances.
2. **Un proyecto y una base independientes por colegio**, aclarado expresamente por el propietario durante esta implementación. No SaaS, multitenancy, selector de cliente ni membresías de usuarios entre colegios. El mismo código se copia para otro colegio y se edita una configuración. `SchoolId` solo identifica a la institución instalada; no selecciona ni particiona clientes.
3. Identidad visual y datos públicos en un único `config/institution.json`; valores desconocidos vacíos, nunca inventados. Sin API para editar marca.
4. Login Razor Pages con Identity, cookie y formularios antiforgery; Angular consume API en el mismo origen.
5. El primer espacio autenticado solo verifica sesión y cuenta activa. La muestra visual es un recorrido separado y únicamente de desarrollo.
6. Cálculo académico puro y probado, sin endpoints ni publicación mientras faltan matrícula, solvencia y aprobación de políticas.

## Reglas conservadas del plan

- Cuatro bimestres, mínimo de 60 por materia; un promedio general no compensa una materia reprobada.
- Recuperación separada que reemplaza el resultado efectivo; ordinaria y boleta inicial se conservan.
- Estudiante con cuenta propia, incluso sin encargado; varios encargados/hijos con facultades explícitas.
- Matrícula y membresía de curso con fechas efectivas; no depender del ciclo almacenado globalmente en sesión.
- Consulta de notas condicionada por obligaciones completamente cubiertas al corte de la publicación.
- Migración inicial de estudiantes, encargados relacionados y vínculos verificables. Nunca inferir parentesco por apellido ni activar permisos por tener una referencia antigua.

## Pendientes que no se resuelven silenciosamente

Redondeo y aprobación en 59.5/59.999; recuperación menor a ordinaria y número de intentos; importes y calendario; cortes de solvencia; ingreso tardío y prorrateos; equivalencias; facultades familiares; retención; hosting/licencia y operación de producción. Ver §21 de la fuente. El cálculo inicial usa precisión decimal sin redondeo, como propuesta de desarrollo no publicada.
