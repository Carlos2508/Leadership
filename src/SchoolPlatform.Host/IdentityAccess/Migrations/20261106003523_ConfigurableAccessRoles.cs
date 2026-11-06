using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.IdentityAccess.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableAccessRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessRoles",
                schema: "iam",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRoles", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AccessRolePermissions",
                schema: "iam",
                columns: table => new
                {
                    RoleKey = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Permission = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRolePermissions", x => new { x.RoleKey, x.Permission });
                    table.ForeignKey(
                        name: "FK_AccessRolePermissions_AccessRoles_RoleKey",
                        column: x => x.RoleKey,
                        principalSchema: "iam",
                        principalTable: "AccessRoles",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountRoleAssignments",
                schema: "iam",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleKey = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRoleAssignments", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_AccountRoleAssignments_AccessRoles_RoleKey",
                        column: x => x.RoleKey,
                        principalSchema: "iam",
                        principalTable: "AccessRoles",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountRoleAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRoles_Label",
                schema: "iam",
                table: "AccessRoles",
                column: "Label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountRoleAssignments_RoleKey",
                schema: "iam",
                table: "AccountRoleAssignments",
                column: "RoleKey");

            // Initial examples are data, never runtime role definitions. Preserve existing effective access exactly.
            migrationBuilder.Sql("""
                INSERT INTO iam.AccessRoles ([Key],Label,Description,IsActive) VALUES
                (N'basic',N'Cuenta básica',N'',1),
                (N'access-manager',N'Gestión de accesos',N'',1),
                (N'auditor',N'Consulta de auditoría',N'',1),
                (N'access-owner',N'Gestión de accesos y auditoría',N'',1),
                (N'people-reader',N'Consulta de personas y familias',N'',1),
                (N'registrar',N'Secretaría: personas, familias e inscripciones',N'',1),
                (N'academic-coordinator',N'Coordinación académica: ciclos, grados, secciones y avisos',N'',1),
                (N'cashier',N'Caja: cobros y recibos',N'',1),
                (N'finance-manager',N'Administración financiera',N'',1),
                (N'gatekeeper',N'Portería: entradas y salidas con carnet',N'',1),
                (N'attendance-supervisor',N'Control de asistencia: portería, reportes y dispositivos',N'',1),
                (N'administrator',N'Administración institucional',N'',1);
                INSERT INTO iam.AccessRolePermissions (RoleKey,Permission) VALUES
                (N'access-manager',N'accounts.manage'),
                (N'auditor',N'audit.read'),
                (N'access-owner',N'accounts.manage'),
                (N'access-owner',N'audit.read'),
                (N'people-reader',N'people.read'),
                (N'registrar',N'people.read'),
                (N'registrar',N'people.manage'),
                (N'registrar',N'academic.read'),
                (N'registrar',N'enrollment.manage'),
                (N'academic-coordinator',N'people.read'),
                (N'academic-coordinator',N'academic.read'),
                (N'academic-coordinator',N'academic.manage'),
                (N'academic-coordinator',N'communication.publish'),
                (N'cashier',N'people.read'),
                (N'cashier',N'academic.read'),
                (N'cashier',N'finance.read'),
                (N'cashier',N'finance.collect'),
                (N'finance-manager',N'people.read'),
                (N'finance-manager',N'academic.read'),
                (N'finance-manager',N'finance.read'),
                (N'finance-manager',N'finance.charges'),
                (N'finance-manager',N'finance.collect'),
                (N'finance-manager',N'finance.reverse'),
                (N'gatekeeper',N'attendance.gate'),
                (N'attendance-supervisor',N'people.read'),
                (N'attendance-supervisor',N'attendance.gate'),
                (N'attendance-supervisor',N'attendance.read'),
                (N'attendance-supervisor',N'attendance.manage'),
                (N'administrator',N'accounts.manage'),
                (N'administrator',N'audit.read'),
                (N'administrator',N'people.read'),
                (N'administrator',N'people.manage'),
                (N'administrator',N'academic.read'),
                (N'administrator',N'academic.manage'),
                (N'administrator',N'enrollment.manage'),
                (N'administrator',N'finance.read'),
                (N'administrator',N'finance.charges'),
                (N'administrator',N'finance.collect'),
                (N'administrator',N'finance.reverse'),
                (N'administrator',N'communication.publish'),
                (N'administrator',N'attendance.gate'),
                (N'administrator',N'attendance.read'),
                (N'administrator',N'attendance.manage');
                INSERT INTO iam.AccountRoleAssignments (UserId,RoleKey)
                SELECT u.Id, matched.[Key] FROM iam.AspNetUsers u
                CROSS APPLY (
                SELECT TOP(1) r.[Key] FROM iam.AccessRoles r
                WHERE NOT EXISTS (SELECT Permission FROM iam.AccountPermissions WHERE UserId=u.Id
                EXCEPT SELECT Permission FROM iam.AccessRolePermissions WHERE RoleKey=r.[Key])
                AND NOT EXISTS (SELECT Permission FROM iam.AccessRolePermissions WHERE RoleKey=r.[Key]
                EXCEPT SELECT Permission FROM iam.AccountPermissions WHERE UserId=u.Id)
                ORDER BY r.[Key]
                ) matched;
                INSERT INTO iam.AccessRoles ([Key],Label,Description,IsActive)
                SELECT 'legacy-'+REPLACE(CONVERT(varchar(36),u.Id),'-',''),
                N'Acceso conservado '+CONVERT(nvarchar(36),u.Id),
                N'Permisos previos preservados sin ampliación; puede renombrarse y administrarse.',1
                FROM iam.AspNetUsers u WHERE NOT EXISTS (SELECT 1 FROM iam.AccountRoleAssignments a WHERE a.UserId=u.Id);
                INSERT INTO iam.AccountRoleAssignments (UserId,RoleKey)
                SELECT u.Id,'legacy-'+REPLACE(CONVERT(varchar(36),u.Id),'-','')
                FROM iam.AspNetUsers u WHERE NOT EXISTS (SELECT 1 FROM iam.AccountRoleAssignments a WHERE a.UserId=u.Id);
                INSERT INTO iam.AccessRolePermissions (RoleKey,Permission)
                SELECT a.RoleKey,p.Permission FROM iam.AccountRoleAssignments a
                JOIN iam.AccountPermissions p ON p.UserId=a.UserId WHERE a.RoleKey LIKE 'legacy-%';
                INSERT INTO audit.Events (Id,OccurredAt,ActorId,TargetId,TargetType,Action,Reason,Detail,TraceId)
                VALUES (NEWID(),SYSDATETIMEOFFSET(),NULL,NULL,N'AccessRole',N'AccessRolesMigrated',
                N'Roles configurables; permisos previos conservados.',N'No cambia el acceso efectivo.',N'migration');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessRolePermissions",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "AccountRoleAssignments",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "AccessRoles",
                schema: "iam");
        }
    }
}
