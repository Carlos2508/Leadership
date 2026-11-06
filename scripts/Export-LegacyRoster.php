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

// Read literal CodeIgniter connection settings without including/evaluating PHP.
// Dynamic expressions deliberately require manual connection settings instead.
function roster_literal(string $text): string {
    roster_require(strlen($text) >= 2 && ($text[0] === "'" || $text[0] === '"'), 'dynamic-source-config');
    $quote = $text[0]; $value = substr($text, 1, -1);
    if ($quote === "'") { return preg_replace_callback('/\\\\([\\\\\'])/', function($m) { return $m[1]; }, $value); }
    roster_require(!preg_match('/\\\\(?:[0-7xX])/', $value), 'dynamic-source-config');
    return preg_replace_callback('/\\\\([nrtvef\\\\$"])/', function($m) {
        $escapes = ['n'=>"\n",'r'=>"\r",'t'=>"\t",'v'=>"\v",'e'=>chr(27),'f'=>"\f",'\\'=>'\\','$'=>'$','"'=>'"'];
        return $escapes[$m[1]];
    }, $value);
}

function roster_config(string $path): array {
    roster_require(is_file($path) && filesize($path) < 1000000, 'source-config-unavailable');
    $tokens = array_values(array_filter(token_get_all(file_get_contents($path)), function($t) {
        return !is_array($t) || !in_array($t[0], [T_WHITESPACE,T_COMMENT,T_DOC_COMMENT], true);
    }));
    $text = function($i) use ($tokens) { return is_array($tokens[$i] ?? null) ? $tokens[$i][1] : ($tokens[$i] ?? ''); };
    $literal = function($i) use ($tokens) { return isset($tokens[$i]) && is_array($tokens[$i]) && $tokens[$i][0] === T_CONSTANT_ENCAPSED_STRING; };
    $settings = [];
    for ($i=0; $i<count($tokens); $i++) {
        if ($text($i) === '$active_group' && $text($i+1) === '=') {
            roster_require($literal($i+2) && roster_literal($text($i+2)) === 'default' && $text($i+3) === ';', 'dynamic-source-config');
        }
        if ($text($i) !== '$db' || $text($i+1) !== '[' || !$literal($i+2) || roster_literal($text($i+2)) !== 'default' || $text($i+3) !== ']') { continue; }
        if ($text($i+4) === '[' && $literal($i+5) && $text($i+6) === ']' && $text($i+7) === '=' &&
            in_array(roster_literal($text($i+5)), ['hostname','database','username','password','port'], true)) {
            roster_require($literal($i+8) && $text($i+9) === ';', 'dynamic-source-config');
        }
        if ($text($i+4) === '[' && $literal($i+5) && $text($i+6) === ']' && $text($i+7) === '=' && $literal($i+8) && $text($i+9) === ';') {
            $settings[roster_literal($text($i+5))] = roster_literal($text($i+8));
        } elseif ($text($i+4) === '=' && ($text($i+5) === 'array' || $text($i+5) === '[')) {
            $begin = $text($i+5) === 'array' ? $i+7 : $i+6;
            for ($j=$begin; $j<count($tokens)-2 && !in_array($text($j), [')',']'], true); $j++) {
                if ($literal($j) && $text($j+1) === '=>' && in_array(roster_literal($text($j)), ['hostname','database','username','password','port'], true)) {
                    $numericPort = roster_literal($text($j)) === 'port' && ctype_digit($text($j+2));
                    roster_require(($literal($j+2) || $numericPort) && in_array($text($j+3), [',',')',']'], true), 'dynamic-source-config');
                    if ($numericPort) { $settings['port'] = $text($j+2); $j+=2; continue; }
                }
                if ($literal($j) && $text($j+1) === '=>' && $literal($j+2) && in_array($text($j+3), [',',')',']'], true)) {
                    $settings[roster_literal($text($j))] = roster_literal($text($j+2));
                    $j+=2;
                }
            }
        }
    }
    roster_require(isset($settings['hostname'],$settings['database'],$settings['username'],$settings['password']) &&
        preg_match('/^[a-zA-Z0-9._-]+$/D',$settings['hostname']) && preg_match('/^[a-zA-Z0-9_-]+$/D',$settings['database']), 'dynamic-source-config');
    $port = $settings['port'] ?? '3306';
    roster_require(ctype_digit($port) && (int)$port > 0 && (int)$port <= 65535, 'invalid-source-port');
    return ['dsn'=>'mysql:host='.$settings['hostname'].';port='.$port.';dbname='.$settings['database'].';charset=utf8mb4',
        'user'=>$settings['username'],'password'=>$settings['password']];
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
        $args = getopt('', ['year:', 'source:', 'output:', 'inspect', 'config-file:']);
        $dsn = getenv('LEGACY_DSN') ?: '';
        $username = getenv('LEGACY_DB_USER') ?: '';
        $password = getenv('LEGACY_DB_PASSWORD') ?: '';
        if (isset($args['config-file'])) {
            $detected = roster_config((string)$args['config-file']);
            $dsn = $detected['dsn']; $username = $detected['user']; $password = $detected['password'];
        }
        roster_require(strpos($dsn, 'mysql:') === 0 && stripos($dsn, 'password') === false &&
            stripos($dsn, 'user=') === false, 'invalid-dsn');
        roster_require($username !== '', 'credentials-required');
        $pdo = new PDO($dsn, $username, $password, [
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
                'years' => $query('SELECT y.schoolyearID, y.schoolyear, (SELECT COUNT(*) FROM student s WHERE s.schoolyearID=y.schoolyearID) AS students FROM schoolyear y ORDER BY y.schoolyearID', []),
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
