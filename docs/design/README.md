# COLEGIO LEADERSHIP MIXCO · DESIGN KIT

**Versión:** 1.0 conceptual · noviembre de 2026  
**Concepto visual recomendado:** Campus Vivo  
**Estado:** Prototipos listos para evaluar e implementar visualmente. No constituyen software de producción.

## Abrir el kit

1. Descomprimí el ZIP.
2. Abrí `index.html` en Chrome, Edge o un servidor local, conservando toda la estructura de carpetas.
3. Revisá `08_Previews` para consultar imágenes PNG de cada pantalla sin abrir los prototipos.
4. Consultá `07_Guias/Manual_de_marca_y_UX.pdf` y `02_Sistema_Diseno/componentes.html` para lineamientos.

## Carpetas

| Carpeta | Contenido | Uso |
| --- | --- | --- |
| `01_Marca` | Escudo SVG vectorizado, logos PNG transparentes 512/1024/2048/4096, composiciones, variantes, favicon, íconos | Web, móvil, impresión; verificar marca antes de producción |
| `02_Sistema_Diseno` | CSS, JSON de tokens, SCSS Angular, Flutter theme, galería de componentes, JS demo | Implementación |
| `03_Web_Escritorio` | **10 prototipos** HTML editables: acceso, administración, estudiantes, notas, finanzas, docentes, portal estudiantil, cursos, notas estudiantiles, familias | Navegador escritorio |
| `04_Web_Movil_Responsive` | **5 prototipos** HTML: acceso, inicio del estudiante, portal familiar, notas, calendario | Navegador móvil |
| `05_App_Movil` | **5 vistas visuales en HTML**: bienvenida, inicio, rendimiento, tareas y notificaciones | Referencia de interfaz Flutter; **no es APK ni código Flutter funcional** |
| `06_Direcciones_Visuales` | Tres propuestas **de portal estudiantil**, no dashboards administrativos | Comparación de dirección creativa |
| `07_Guias` | Manual PDF, especificaciones, instrucciones, brief, recomendaciones y límites de uso | Diseño / desarrollo |
| `08_Previews` | **23 PNG individuales** (20 pantallas + 3 alternativas visuales) y láminas generales | Revisión visual |

## Logos y fidelidad

El único logo oficial compartido fue una imagen rasterizada con fondo verde y el lema. A partir de ella se construyeron recursos de trabajo:

- `logo_original_recortado_transparente.png` conserva los píxeles originales del escudo con transparencia exterior estimada; es **raster**, no vector.
- `logo_escudo_vectorizado.svg` contiene **paths SVG reales** creados por trazado automático de los colores de la imagen fuente; **no** incrusta PNG/JPEG. Es editable y escalable, pero **no equivale al arte maestro oficial**: las curvas y letras pueden presentar irregularidades por la baja resolución original.
- `logo_escudo_transparente_4096.png` es exportación de alta resolución del SVG trazado. 4096 píxeles **no** garantiza mayor detalle que el origen.
- `logo_horizontal.svg` y `logo_vertical.svg` combinan el escudo trazado con tipografía SVG editable. Para intercambios sin fuentes instaladas, convertir sus objetos de texto a contornos al exportar a impresión.
- `icono_app_simple.svg` es una **reinterpretación conceptual simplificada**, no la marca oficial. Usar solo con aprobación institucional.
- `logo_monocromatico_negro.svg`, `logo_blanco_sobre_verde.svg` y `logo_una_tinta_dorada.svg` son reinterpretaciones de una tinta obtenidas a partir de la señal negra original.
- Los colores son **propuestos y muestreados**, no pantones ni valores CMYK certificados.

## En Angular

Copiá `01_Marca` a `src/assets/leadership/` y `tokens.css` a `src/styles/leadership/`; importá los tokens y las reglas según tu arquitectura. Usá componentes Angular propios, tomando `componentes.html` y los prototipos como referencia; **los HTML no son componentes Angular listos para compilar**.

```scss
@use './styles/leadership/angular_variables.scss' as leadership;
```

```html
<img src="assets/leadership/logo_escudo_transparente_512.png" alt="Colegio Leadership Mixco" />
```

## En Flutter

Copiá `icono_app_1024.png`, `favicon_512.png` o el ícono conceptual aprobado a assets, y adaptá `02_Sistema_Diseno/flutter_theme.dart` a tu proyecto; **el archivo solo define un ThemeData de referencia**.

## Accesibilidad

Contrastes calculados sobre las muestras sRGB: verde profundo sobre blanco **12.64:1**; verde primario sobre blanco **8.53:1**; dorado sobre blanco **2.12:1** (**no apto para texto normal**); verde profundo sobre dorado **5.97:1**. Evitar usar dorado como color de textos pequeños sobre fondo blanco. El token `--muted` fue ajustado a `#607461` (aprox. **5.04:1** sobre blanco); otros textos secundarios y estados aún requieren una auditoría completa antes del lanzamiento.

## Privacidad, técnica y alcance

- Los datos de las capturas son **ficticios y solo ilustrativos**; se excluyó el expediente real de la plantilla de notas compartida.
- No hay backend, autenticación, registro persistente, autorización real, facturación, integración con RENAP, APIs ni formularios de producción.
- No se implementaron diseños funcionales nativos Flutter ni librería Figma; los prototipos editables son HTML/CSS/SVG/JSON.
- Las vistas web son ejemplos visuales: antes de lanzar se requieren WCAG 2.2 AA, pruebas responsive, pruebas con usuarios, revisión de permisos y seguridad de datos escolares.
- **Se recomienda usar Campus Vivo para estudiantes y familias, manteniendo secciones de administración de menor densidad visual**. El sistema de diseño debe compartirse sin exponer los mismos datos ni permisos a todos los perfiles.
