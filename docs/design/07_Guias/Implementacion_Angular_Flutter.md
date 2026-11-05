# ESPECIFICACIONES DE IMPLEMENTACIÓN

## Arquitectura recomendada
- Frontend web: Angular con estilos CSS de variables y componentes propios.
- App móvil: Flutter con ThemeData y componentes optimizados para interacción táctil.
- Contratos compartidos: mismos nombres de color, escala, tipografía y criterios de accesibilidad.
- Separación por roles y permisos verificados en backend. Mostrar u ocultar menús **no es** control de acceso.

## Jerarquía y contenidos
- Estudiante: priorizar actividades próximas, progreso por materia, logros, calendario y comunicaciones.
- Familia: priorizar hijos vinculados, asistencia, notas, avisos y pagos; evitar jerga técnica.
- Docente: priorizar horario, tareas de revisión, grupos y evaluaciones.
- Administrador: priorizar indicadores accionables, matrícula, supervisión y operaciones.

## Variables UI
`--forest`, `--primary`, `--green`, `--gold`, `--mint`, `--cream`, `--ink`, `--muted`, `--border`; ver `tokens.css`, `tokens.json`.

## Espaciado
Base 4 px: 4, 8, 12, 16, 24, 32, 48. Contenedores adaptativos; rejillas de 12 columnas desktop si la app lo requiere; una columna móvil con progresión gradual.

## Breakpoints orientativos
480px, 768px, 1024px, 1440px. Validar por contenido y dispositivos; no por marcas.

## Tipografía
Inter para UI y tablas; Inter Display opcional para grandes titulares. Tamaños base: desktop 14–16 px; móvil 14–16 px; tablas 12–14 px según densidad. No reducir la nota o texto crítico por debajo de legibilidad funcional.

## Iconografía y estados
Usar un solo set de íconos (Lucide, por ejemplo) con estilo consistente; los emojis de las demos son marcadores visuales y deben reemplazarse por íconos propios antes de la publicación.

## Impresión
Para rediseñar el reporte de calificaciones: CSS `@page` y layout específico A4/Carta; aislar datos personales; evitar `zoom` forzado con mínimos que pueda causar páginas extra; verificar saltos y fondos en los navegadores reales.

## Seguridad y privacidad
No incluir información académica de menores en capturas públicas. Aplicar control de acceso por rol, auditoría, consentimiento cuando corresponda, y políticas de almacenamiento/retención según el contexto legal aplicable.

## Arte oficial
El SVG trazado no sustituye el logotipo maestro oficial. Se requiere validación de identidad, sobre todo para impresión de gran formato y documentación formal.
