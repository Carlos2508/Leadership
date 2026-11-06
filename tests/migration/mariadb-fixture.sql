-- Exclusively for a new MariaDB instance with a random, fixture-owned data directory.
CREATE DATABASE SchoolPlatform_MigrationFixture CHARACTER SET utf8mb4;
USE SchoolPlatform_MigrationFixture;
CREATE TABLE schoolyear (schoolyearID INT PRIMARY KEY, schoolyear VARCHAR(40), schoolyeartitle VARCHAR(40)) ENGINE=InnoDB;
CREATE TABLE classes (classesID INT PRIMARY KEY, classes VARCHAR(60), classes_numeric INT) ENGINE=InnoDB;
CREATE TABLE section (sectionID INT PRIMARY KEY, section VARCHAR(20), classesID INT, category VARCHAR(40)) ENGINE=InnoDB;
CREATE TABLE student (studentID INT PRIMARY KEY, schoolyearID INT, name VARCHAR(100), dob DATE NULL,
    state VARCHAR(30), registerNO VARCHAR(40), phone VARCHAR(40) NULL, email VARCHAR(100) NULL,
    parentID INT NULL, classesID INT, sectionID INT, password VARCHAR(128), username VARCHAR(40)) ENGINE=InnoDB;
CREATE TABLE parents (parentsID INT PRIMARY KEY, schoolyearID INT, name VARCHAR(100), father_name VARCHAR(100),
    mother_name VARCHAR(100), phone VARCHAR(40), email VARCHAR(100), password VARCHAR(128)) ENGINE=InnoDB;
CREATE TABLE studentrelation (studentrelationID INT PRIMARY KEY, srstudentID INT, srschoolyearID INT, srclassesID INT, srsectionID INT) ENGINE=InnoDB;
INSERT INTO schoolyear VALUES (42,'2026','Origen sintético'),(43,'2027','Otro ciclo sintético');
INSERT INTO classes VALUES (4,'Cuarto sintético',4);
INSERT INTO section VALUES (2,'A',4,'Sintética');
INSERT INTO parents VALUES (9,41,'Adulto sintético','Adulto sintético','Adulta sintética','5555-0100','familia@example.test','DO-NOT-EXPORT-THIS');
INSERT INTO student VALUES
    (1,42,'Alumna Uno Ejemplo Sintético','2014-03-01','000001',NULL,NULL,NULL,9,4,2,'DO-NOT-EXPORT-THIS','unused-1'),
    (2,42,'Alumno Dos Ejemplo Sintético',NULL,'000002',NULL,NULL,NULL,9,4,0,'DO-NOT-EXPORT-THIS','unused-2'),
    (3,42,'Alumna Tres Caso Sintético',NULL,'000003',NULL,NULL,NULL,404,4,0,'DO-NOT-EXPORT-THIS','unused-3'),
    (4,43,'Alumna Fuera De Alcance',NULL,'000004',NULL,NULL,NULL,9,4,2,'DO-NOT-EXPORT-THIS','unused-4');
-- Two historical rows must not multiply the exported student count.
INSERT INTO studentrelation VALUES (1,1,42,4,2),(2,1,41,3,0);
CREATE USER 'roster_reader'@'127.0.0.1';
GRANT SELECT ON SchoolPlatform_MigrationFixture.* TO 'roster_reader'@'127.0.0.1';
