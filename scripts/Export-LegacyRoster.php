<?php
// Standalone read-only CLI. Never include or bootstrap the legacy PHP application.
// Compatible with the installed PHP CLI; no framework or Composer dependencies.
declare(strict_types=1);

function roster_require(bool $condition, string $code): void {
    if (!$condition) { throw new RuntimeException($code); }
}

function roster_tables(): array {
    return [
        'schoolyear' => ['schoolyearID', 'schoolyear', 'schoolyeartitle'],
        'student' => ['studentID', 'schoolyearID', 'name', 'dob', 'state', 'registerNO', 'phone', 'email', 'parentID', 'classesID', 'sectionID'],
        'parents' => ['parentsID', 'schoolyearID', 'name', 'father_name', 'mother_name', 'phone', 'email'],
        'studentrelation' => ['studentrelationID', 'srstudentID', 'srschoolyearID', 'srclassesID', 'srsectionID'],
        'classes' => ['classesID', 'classes', 'classes_numeric'],
        'section' => ['sectionID', 'section', 'classesID', 'category']
    ];
}

// Injected query/control functions permit a synthetic contract test without a legacy connection.
function roster_extract(callable $query, callable $control, string $year, string $source): array {
    roster_require((bool)preg_match('/^[1-9][0-9]{0,9}$/D', $year) &&
        (bool)preg_match('/^[a-z0-9][a-z0-9-]{0,39}$/D', $source), 'invalid-source-or-year');
    $tables = $query("SELECT TABLE_NAME, ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN ('schoolyear','student','parents','studentrelation','classes','section')", []);
    $required = roster_tables();
    $found = [];
    foreach ($tables as $t) {
        roster_require(isset($required[$t['TABLE_NAME']]) && strcasecmp($t['ENGINE'] ?? '', 'InnoDB') === 0, 'unsupported-source-engine');
        $found[$t['TABLE_NAME']] = true;
    }
    roster_require(count($found) === count($required), 'missing-source-table');
    $columns = $query("SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN ('schoolyear','student','parents','studentrelation','classes','section')", []);
    $known = [];
    foreach ($columns as $c) { $known[$c['TABLE_NAME']][$c['COLUMN_NAME']] = true; }
    foreach ($required as $table => $names) {
        foreach ($names as $name) { roster_require(isset($known[$table][$name]), 'missing-source-column'); }
    }
    $control('SET TRANSACTION ISOLATION LEVEL REPEATABLE READ');
    $control('START TRANSACTION WITH CONSISTENT SNAPSHOT, READ ONLY');
    try {
        $years = $query('SELECT schoolyearID AS sourceYearId, schoolyear AS sourceYearLabel FROM schoolyear WHERE schoolyearID = ?', [$year]);
        roster_require(count($years) === 1, 'source-year-not-found');
        $students = $query('SELECT studentID AS legacyStudentId, name AS fullName, dob AS birthDate, state AS studentCodeCandidate, registerNO AS registerNumberCandidate, phone, email, NULLIF(parentID,0) AS familyLegacyId, classesID AS sourceGradeId, NULLIF(sectionID,0) AS sourceSectionId FROM student WHERE schoolyearID = ? ORDER BY studentID LIMIT 10001', [$year]);
        roster_require(count($students) > 0 && count($students) <= 10000, 'empty-or-too-large-source');
        $families = $query('SELECT p.parentsID AS legacyFamilyId, p.schoolyearID AS sourceYearId, p.name AS recordName, p.father_name AS fatherName, p.mother_name AS motherName, p.phone AS sharedPhone, p.email AS sharedEmail FROM parents p WHERE EXISTS (SELECT 1 FROM student s WHERE s.parentID = p.parentsID AND s.schoolyearID = ?) ORDER BY p.parentsID', [$year]);
        $grades = $query('SELECT c.classesID AS sourceGradeId, c.classes AS name, c.classes_numeric AS sequence FROM classes c WHERE EXISTS (SELECT 1 FROM student s WHERE s.classesID = c.classesID AND s.schoolyearID = ?) ORDER BY c.classesID', [$year]);
        $sections = $query('SELECT c.sectionID AS sourceSectionId, c.section AS name, c.classesID AS sourceGradeId, c.category FROM section c WHERE EXISTS (SELECT 1 FROM student s WHERE s.sectionID = c.sectionID AND s.schoolyearID = ?) ORDER BY c.sectionID', [$year]);
        $relations = $query('SELECT r.studentrelationID AS legacyRelationId, r.srstudentID AS legacyStudentId, r.srschoolyearID AS sourceYearId, r.srclassesID AS sourceGradeId, NULLIF(r.srsectionID,0) AS sourceSectionId FROM studentrelation r WHERE EXISTS (SELECT 1 FROM student s WHERE s.studentID = r.srstudentID AND s.schoolyearID = ?) ORDER BY r.srstudentID, r.studentrelationID', [$year]);
        $control('COMMIT');
        return ['formatVersion' => 1, 'artifactKind' => 'raw-roster-export-needs-review', 'source' => $source,
            'sourceYearId' => $year, 'sourceYearLabel' => (string)$years[0]['sourceYearLabel'],
            'exportedAt' => gmdate('Y-m-d\TH:i:s\Z'), 'students' => $students, 'families' => $families,
            'grades' => $grades, 'sections' => $sections, 'historicalRelations' => $relations];
    } catch (Throwable $e) {
        $control('ROLLBACK');
        throw $e;
    }
}

function roster_export_main(): int {
    // No secret is accepted in argv. Exceptions from PDO may contain personal values: do not print them.
    try {
        $args = getopt('', ['year:', 'source:', 'output:', 'inspect']);
        $dsn = getenv('LEGACY_DSN') ?: '';
        roster_require(strpos($dsn, 'mysql:') === 0 && stripos($dsn, 'password') === false &&
            stripos($dsn, 'user=') === false, 'invalid-dsn');
        roster_require((getenv('LEGACY_DB_USER') ?: '') !== '', 'credentials-required');
        $pdo = new PDO($dsn, getenv('LEGACY_DB_USER'), getenv('LEGACY_DB_PASSWORD') ?: '', [
            PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_EMULATE_PREPARES => false,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC
        ]);
        $query = function(string $sql, array $parameters) use ($pdo): array {
            $statement = $pdo->prepare($sql);
            $statement->execute($parameters);
            return $statement->fetchAll();
        };
        $control = function(string $sql) use ($pdo): void { $pdo->exec($sql); };
        if (isset($args['inspect'])) {
            // Metadata/catalog labels only, never student or parent rows.
            echo json_encode(['server' => $query('SELECT VERSION() AS version, DATABASE() AS catalog', []),
                'years' => $query('SELECT schoolyearID, schoolyear FROM schoolyear ORDER BY schoolyearID', []),
                'tables' => $query("SELECT TABLE_NAME, ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN ('schoolyear','student','parents','studentrelation','classes','section')", [])], JSON_PRETTY_PRINT), PHP_EOL;
            return 0;
        }
        roster_require(isset($args['year'], $args['source'], $args['output']) && !file_exists($args['output']), 'arguments-or-output-invalid');
        $result = roster_extract($query, $control, (string)$args['year'], (string)$args['source']);
        $json = json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
        roster_require($json !== false, 'invalid-source-encoding');
        $file = fopen($args['output'], 'x');
        roster_require($file !== false, 'output-create-failed');
        $written = fwrite($file, $json);
        fclose($file);
        if ($written !== strlen($json)) { unlink($args['output']); throw new RuntimeException('output-write-failed'); }
        echo json_encode(['students' => count($result['students']), 'familyRows' => count($result['families']), 'sha256' => hash('sha256', $json)]), PHP_EOL;
        return 0;
    } catch (Throwable $e) {
        fwrite(STDERR, "legacy-export-failed; no complete export was produced\n");
        return 1;
    }
}

if (realpath($_SERVER['SCRIPT_FILENAME']) === __FILE__) { exit(roster_export_main()); }
