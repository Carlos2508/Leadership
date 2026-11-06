<?php
declare(strict_types=1);
require __DIR__ . '/../../scripts/Export-LegacyRoster.php';

$controls = [];
$queries = [];
$fail = '';
$query = function(string $sql, array $parameters) use (&$queries, &$fail): array {
    $queries[] = $sql;
    roster_require(!preg_match('/\b(password|username|dni|INSERT|UPDATE|DELETE|OUTFILE)\b/i', $sql), 'unexpected-column-or-write');
    if (strpos($sql, 'information_schema.TABLES') !== false) {
        $result = [];
        foreach (roster_tables() as $name => $cols) { $result[] = ['TABLE_NAME' => $name, 'ENGINE' => $fail === 'engine' ? 'MyISAM' : 'InnoDB']; }
        return $result;
    }
    if (strpos($sql, 'information_schema.COLUMNS') !== false) {
        $result = [];
        foreach (roster_tables() as $name => $cols) {
            foreach ($cols as $col) { if ($fail !== 'column' || $col !== 'parentID') { $result[] = ['TABLE_NAME' => $name, 'COLUMN_NAME' => $col]; } }
        }
        return $result;
    }
    roster_require($parameters === ['42'], 'missing-explicit-year-parameter');
    if (strpos($sql, 'FROM schoolyear WHERE') !== false) { return $fail === 'year' ? [] : [['sourceYearId' => '42', 'sourceYearLabel' => '2026']]; }
    if (strpos($sql, 'FROM student WHERE') !== false) {
        if ($fail === 'query') { throw new RuntimeException('synthetic-query-failure'); }
        return $fail === 'empty' ? [] : [['legacyStudentId' => '1', 'fullName' => 'Persona completamente sintética', 'birthDate' => null,
            'studentCodeCandidate' => '000123', 'registerNumberCandidate' => null, 'phone' => null, 'email' => null,
            'familyLegacyId' => '9', 'sourceGradeId' => '4', 'sourceSectionId' => null]];
    }
    if (strpos($sql, 'FROM parents p') !== false) { return [['legacyFamilyId' => '9', 'fatherName' => 'Adulto sintético', 'motherName' => 'Adulta sintética']]; }
    return [];
};
$control = function(string $sql) use (&$controls): void { $controls[] = $sql; };
$result = roster_extract($query, $control, '42', 'synthetic-source');
roster_require($result['students'][0]['studentCodeCandidate'] === '000123', 'leading-zero-lost');
roster_require($result['students'][0]['sourceSectionId'] === null && count($result['families']) === 1, 'source-shape-changed');
roster_require($controls === ['SET TRANSACTION ISOLATION LEVEL REPEATABLE READ', 'START TRANSACTION WITH CONSISTENT SNAPSHOT, READ ONLY', 'COMMIT'], 'wrong-transaction');
foreach (['engine','column','year','empty','query'] as $scenario) {
    $fail = $scenario; $controls = [];
    $caught = false;
    try { roster_extract($query, $control, '42', 'synthetic-source'); } catch (RuntimeException $e) { $caught = true; }
    roster_require($caught, 'expected-rejection-missing');
    if (in_array($scenario, ['year','empty','query'], true)) { roster_require(end($controls) === 'ROLLBACK', 'rollback-required'); }
    else { roster_require(count($controls) === 0, 'metadata-failure-started-transaction'); }
}
$caught = false;
try { roster_extract($query, $control, '42 OR 1=1', 'synthetic-source'); } catch (RuntimeException $e) { $caught = true; }
roster_require($caught, 'invalid-year-accepted');
echo "7 synthetic exporter scenarios passed; no database connected.\n";

$config = tempnam(sys_get_temp_dir(), 'roster_config_test_');
$marker = $config . '.must-not-exist';
try {
    $prefix = '<?php $active_group = \'default\'; file_put_contents(' . var_export($marker,true) . ', \'never execute\'); ';
    file_put_contents($config, $prefix . '$db[\'default\'] = array(\'hostname\'=>\'localhost\',\'database\'=>\'synthetic\',\'username\'=>\'reader\',\'password\'=>\'quote\\\'slash\\\\end\');');
    $detected = roster_config($config);
    roster_require($detected['user'] === 'reader' && $detected['password'] === "quote'slash\\end" && !file_exists($marker), 'config-was-executed-or-escaped-incorrectly');
    file_put_contents($config, $prefix . '$db[\'default\'][\'hostname\']=\'localhost\'; $db[\'default\'][\'database\']=\'synthetic\'; $db[\'default\'][\'username\']=\'reader\'; $db[\'default\'][\'password\']=\'\';');
    roster_require(roster_config($config)['password'] === '', 'direct-config-unreadable');
    file_put_contents($config, $prefix . '$db[\'default\'] = array(\'hostname\'=>\'localhost\',\'database\'=>\'synthetic\',\'username\'=>\'reader\',\'password\'=>\'\'); $db[\'default\'][\'password\']=getenv(\'SECRET\');');
    $caught=false; try { roster_config($config); } catch (RuntimeException $e) { $caught=true; }
    roster_require($caught && !file_exists($marker), 'dynamic-config-not-rejected');
    file_put_contents($config, '<?php $db[\'default\']=array(\'hostname\'=>\'localhost\',\'database\'=>\'synthetic\',\'username\'=>\'reader\',\'password\'=>\'\'); $active_group=\'other\';');
    $caught=false; try { roster_config($config); } catch (RuntimeException $e) { $caught=true; }
    roster_require($caught, 'other-active-group-not-rejected');
    echo "4 synthetic config detection scenarios passed; no PHP configuration executed and no database connected.\n";
} finally { unlink($config); if (file_exists($marker)) { unlink($marker); } }
