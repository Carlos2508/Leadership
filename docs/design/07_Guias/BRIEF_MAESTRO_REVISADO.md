# BRIEF MAESTRO REVISADO · Leadership Mixco — Campus Vivo

## Propósito
Desarrollar una plataforma educativa integral que fomente pertenencia, motivación, aprendizaje autónomo y comunicación accesible, además de cubrir necesidades administrativas. El portal **no es solo un ERP escolar**: sus usuarios principales incluyen estudiantes, familias, docentes y personal del colegio.

## Identidad
Conservar el escudo circular de Colegio Leadership Mixco y el lema «¡Piensa En Grande!». Distinguir el escudo oficial del isotipo simplificado conceptual. Paleta ancla: verde profundo `#073B25`, verde primario `#0B5835`, dorado `#ECA42E`, blanco `#FFFFFF`; apoyos semánticos de menta, azul, lila y coral según contexto (clases, calendarios, alertas). Evitar tratar todas las pantallas como paneles administrativos.

## Dirección UX recomendada: Campus Vivo
- Lenguaje cercano pero institucional, acorde con usuarios de distintas edades.
- Composición con espacios respirables, encabezados expresivos y tarjetas visuales que muestren clases y metas.
- Escritura en segunda persona para la vista estudiantil; tono claro y empático para familias.
- Seguimiento de aprendizaje y tareas en primer nivel de la interfaz.
- Gráficos de progreso comprensibles y actividades con iconografía por asignatura.
- En docente: grupos, revisiones y asistencia. En administración: indicadores, matrículas y reportes.

## Arquitectura funcional propuesta
**Estudiante:** Inicio, Mis clases, Actividades, Notas, Agenda, Logros, Avisos, Perfil.  
**Familia:** Inicio de hijos, Rendimiento, Asistencia, Calendario, Comunicados, Pagos.  
**Docente:** Mis grupos, Planificación, Calificaciones, Tareas, Asistencia, Comunicaciones.  
**Administración:** Estudiantes, Matrícula, Académico, Docentes, Reportes, Avisos.  
**Finanzas:** Colegiaturas, Pagos, Saldos, Comprobantes, Reportes.

## Los 20 mockups incluidos
### Web escritorio · 10
1. Acceso institucional. 2. Dashboard administrativo. 3. Estudiantes. 4. Calificaciones. 5. Finanzas. 6. Docentes. 7. Portal estudiantil. 8. Mis cursos. 9. Mis notas. 10. Familias.

### Web responsive · 5
11. Login móvil. 12. Inicio estudiantil móvil. 13. Consulta de hijos. 14. Notas móviles. 15. Calendario/comunicados.

### Referencia de app nativa · 5
16. Bienvenida. 17. Inicio personalizado. 18. Rendimiento. 19. Tareas/calendario. 20. Notificaciones. Son vistas conceptuales en HTML que deben implementarse en Flutter.

## Criterios UI/UX
- Desktop: escritorio amplio y lectura rápida; navegación lateral para tareas complejas.
- Navegador móvil: foco en consulta; navegación inferior y tarjetas legibles; evitar tablas horizontalmente interminables.
- App nativa: patrones de accesibilidad móvil, progresión, notificaciones y acciones rápidas.
- Escala espacial 4/8; tarjetas 16–30 px de radio; botones ≥44 px de alto táctil (objetivo recomendado).
- Estados: normal, hover, focus, disabled, empty, loading, error y success.
- Usar datos ficticios en las presentaciones; evitar fotos reales no autorizadas de menores.

## Tareas antes de implementar
1. Aprobar con dirección la identidad del escudo y el permiso para su reproducción.
2. Obtener el archivo vectorial maestro oficial si existe.
3. Definir roles, permisos, términos y datos escolares reales.
4. Refinar cada vista para los distintos niveles educativos y edades.
5. Aplicar accesibilidad WCAG 2.2 AA, pruebas con estudiantes/familias y requisitos de seguridad.
6. Construir componentes Angular y Flutter con contratos de diseño, no copiar HTML de demostración sin adaptar.

## Resultado
Mantener el sistema de diseño coherente, preservar la marca, y diseñar primero las experiencias reales de quienes aprenden, enseñan y acompañan. No sustituir los prototipos por un panel puramente administrativo.
