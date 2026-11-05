# ADR-004: prototipo móvil en Flutter con datos ficticios

Estado: adoptado como prototipo; no es la app de producción.

## Contexto

El propietario pidió un prototipo Flutter completo, bien diseñado y navegable, con todos los módulos y datos ficticios. El backend todavía no expone personas, matrícula, tareas, notas ni finanzas (ver `PROGRESS.md`). El plan maestro (§15.5) recomienda construir la app móvil cuando la API esté estable; este prototipo no adelanta esa integración: sirve para validar experiencia, navegación y diseño.

## Decisión

- Proyecto independiente en `mobile/` (Flutter estable, Dart 3.13, Riverpod 3, go_router, intl). No modifica la API ni Angular.
- Capas: `domain` (modelos e interfaces), `data/demo` (servidor simulado en memoria con datos sintéticos deterministas), `features` (estado y pantallas). Las pantallas solo conocen interfaces; una implementación HTTP podrá sustituir a la demo.
- Marca desde el único `config/institution.json`, copiada por `tool/sync_institution.dart` a un asset ignorado por Git (proyección pública, sin `Documents`). Sin valores predeterminados de marca ante errores.
- Cinco perfiles de experiencia (estudiante, familia, docente, administración, caja). Son navegación, no autorización.
- Reglas conservadas: notas pendientes no son cero; la app no recalcula aprobación; notas bloqueadas por solvencia no se envían al cliente; un comprobante no habilita notas hasta validarse; pagos con clave de idempotencia; códigos de activación de un solo uso entregados por canal verificado; vínculos familiares explícitos.
- Datos ficticios solo dentro del prototipo; no se reutilizan como repositorio de producción ni se usan datos reales.

## Consecuencias

- La experiencia puede revisarse en Android, iOS y web antes de tener API. Las reglas del servidor simulado son ilustrativas: las definitivas siguen pendientes de política (redondeo, cortes, convenios).
- Para producción faltan OIDC + PKCE, almacenamiento seguro, cliente API, caché por usuario, archivos y notificaciones reales, y pruebas en dispositivos físicos.
- No declarar terminada la fase móvil (MOB) por este prototipo.
