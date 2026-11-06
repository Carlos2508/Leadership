-- BORRADOR para la futura extracción autorizada por el propietario.
-- No se ejecutó contra el legado. No es un importador ni exporta archivos.
-- Revisar MIGRATION_2027.md y metadatos antes de seleccionar datos personales.
-- Usar conexión dedicada, catálogo correcto y credencial SELECT sin escrituras.
-- No pegar resultados personales en Git, tickets, terminales grabadas ni CI.

-- Metadatos: comprobar versión, catálogo, columnas y motores primero.
SELECT VERSION() AS server_version, DATABASE() AS selected_catalog;

SELECT TABLE_NAME, ENGINE
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('schoolyear', 'student', 'parents', 'studentrelation', 'classes', 'section')
ORDER BY TABLE_NAME;

SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND (
    (TABLE_NAME = 'schoolyear' AND COLUMN_NAME IN ('schoolyearID', 'schoolyear', 'schoolyeartitle'))
    OR (TABLE_NAME = 'student' AND COLUMN_NAME IN (
      'studentID', 'schoolyearID', 'name', 'dob', 'state', 'registerNO', 'phone', 'email',
      'parentID', 'classesID', 'sectionID', 'modify_date'))
    OR (TABLE_NAME = 'parents' AND COLUMN_NAME IN (
      'parentsID', 'schoolyearID', 'name', 'father_name', 'mother_name', 'phone', 'email', 'modify_date'))
    OR (TABLE_NAME = 'studentrelation' AND COLUMN_NAME IN (
      'studentrelationID', 'srstudentID', 'srschoolyearID', 'srclassesID', 'srsectionID'))
    OR (TABLE_NAME = 'classes' AND COLUMN_NAME IN ('classesID', 'classes', 'classes_numeric'))
    OR (TABLE_NAME = 'section' AND COLUMN_NAME IN ('sectionID', 'section', 'classesID', 'category'))
  )
ORDER BY TABLE_NAME, ORDINAL_POSITION;

-- Continuar solo tras comprobar que todas las tablas están cubiertas por
-- la instantánea InnoDB, sin DDL concurrente. Para otro motor: revisar estrategia.
-- Todas las consultas siguientes se ejecutan en LA MISMA conexión/transacción.
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
START TRANSACTION WITH CONSISTENT SNAPSHOT, READ ONLY;

-- El ID de origen no es el año natural ni el GUID de destino.
-- Sustituir NULL por el schoolyearID elegido explícitamente al extraer de verdad.
-- NULL devuelve catálogos de ciclos pero CERO candidatos personales.
SET @source_schoolyear_id = NULL;

SELECT schoolyearID AS source_year_id, schoolyear AS source_year_label,
       schoolyeartitle AS source_year_title
FROM schoolyear
ORDER BY schoolyearID;

-- Control del alcance. Todos son candidatos; la continuidad 2027 se revisa después.
SELECT COUNT(*) AS candidate_students,
       COALESCE(SUM(COALESCE(s.parentID, 0) = 0), 0) AS without_parent_reference,
       COALESCE(SUM(COALESCE(s.parentID, 0) <> 0 AND p.parentsID IS NULL), 0) AS missing_parent_row,
       COALESCE(SUM(p.parentsID IS NOT NULL AND p.schoolyearID <> s.schoolyearID), 0) AS parent_from_other_year,
       COUNT(DISTINCT p.parentsID) AS referenced_family_rows
FROM student AS s
LEFT JOIN parents AS p ON p.parentsID = s.parentID
WHERE @source_schoolyear_id IS NOT NULL AND s.schoolyearID = @source_schoolyear_id;

-- Una fila por estudiante de origen. No filtrar por active: es acceso legado,
-- no confirmación de matrícula 2027. No extraer username/password/usertypeID/dni.
SELECT s.studentID AS legacy_student_id, s.schoolyearID AS source_year_id,
       s.name AS raw_full_name, s.dob AS raw_birth_date,
       s.state AS institutional_code_candidate, s.registerNO AS register_number_candidate,
       s.phone AS raw_phone, s.email AS raw_email,
       s.parentID AS legacy_family_id, s.classesID AS source_grade_id,
       NULLIF(s.sectionID, 0) AS source_section_id, s.modify_date AS source_modified_at
FROM student AS s
WHERE @source_schoolyear_id IS NOT NULL AND s.schoolyearID = @source_schoolyear_id
ORDER BY s.studentID;

-- Solo familias referenciadas por candidatos. No restringir el ciclo del padre:
-- las referencias cruzadas se reportan, no se pierden por un filtro silencioso.
-- Una fila familiar NO equivale necesariamente a un adulto identificado.
SELECT p.parentsID AS legacy_family_id, p.schoolyearID AS source_family_year_id,
       p.name AS raw_record_name, p.father_name AS raw_father_name,
       p.mother_name AS raw_mother_name, p.phone AS raw_shared_phone,
       p.email AS raw_shared_email, p.modify_date AS source_modified_at
FROM parents AS p
WHERE @source_schoolyear_id IS NOT NULL
  AND EXISTS (
    SELECT 1 FROM student AS s
    WHERE s.schoolyearID = @source_schoolyear_id AND s.parentID = p.parentsID
  )
ORDER BY p.parentsID;

-- Catálogos acotados al padrón fuente. No determinan la ubicación destino.
SELECT c.classesID AS source_grade_id, c.classes AS source_grade_name,
       c.classes_numeric AS source_grade_order
FROM classes AS c
WHERE @source_schoolyear_id IS NOT NULL
  AND EXISTS (
    SELECT 1 FROM student AS s
    WHERE s.schoolyearID = @source_schoolyear_id AND s.classesID = c.classesID
  )
ORDER BY c.classesID;

SELECT sec.sectionID AS source_section_id, sec.section AS source_section_name,
       sec.classesID AS source_grade_id, sec.category AS source_category
FROM section AS sec
WHERE @source_schoolyear_id IS NOT NULL
  AND EXISTS (
    SELECT 1 FROM student AS s
    WHERE s.schoolyearID = @source_schoolyear_id AND s.sectionID = sec.sectionID
  )
ORDER BY sec.sectionID;

-- Datos estructurales auxiliares, sin srname. Comparar duplicados/discrepancias
-- fuera del servidor sin multiplicar alumnos por un JOIN uno-a-varios.
SELECT r.studentrelationID AS legacy_relation_id, r.srstudentID AS legacy_student_id,
       r.srschoolyearID AS source_relation_year_id, r.srclassesID AS source_relation_grade_id,
       NULLIF(r.srsectionID, 0) AS source_relation_section_id
FROM studentrelation AS r
WHERE @source_schoolyear_id IS NOT NULL
  AND EXISTS (
    SELECT 1 FROM student AS s
    WHERE s.schoolyearID = @source_schoolyear_id AND s.studentID = r.srstudentID
  )
ORDER BY r.srstudentID, r.studentrelationID;

COMMIT;

-- No contiene INTO OUTFILE, DDL, INSERT, UPDATE, DELETE ni acceso a notas/dinero.
-- El extractor futuro debe abortar ante cualquier error; nunca continuar con
-- archivos parciales como si fueran una extracción completa.
