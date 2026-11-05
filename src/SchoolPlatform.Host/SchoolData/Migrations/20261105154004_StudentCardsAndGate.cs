using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class StudentCardsAndGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GateDevices",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GateDevices_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GateDevices_AspNetUsers_RevokedBy",
                        column: x => x.RevokedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Photos",
                schema: "people",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Photos", x => x.PersonId);
                    table.CheckConstraint("CK_Photos_Type", "[ContentType] IN ('image/jpeg','image/png')");
                    table.ForeignKey(
                        name: "FK_Photos_AspNetUsers_UploadedBy",
                        column: x => x.UploadedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Photos_Persons_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "people",
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentCards",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IssuedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentCards_AspNetUsers_IssuedBy",
                        column: x => x.IssuedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCards_AspNetUsers_RevokedBy",
                        column: x => x.RevokedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCards_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "people",
                        principalTable: "Students",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GateEvents",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    VoidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateEvents", x => x.Id);
                    table.CheckConstraint("CK_GateEvents_Actor", "[RecordedBy] IS NOT NULL OR [DeviceId] IS NOT NULL");
                    table.CheckConstraint("CK_GateEvents_Direction", "[Direction] IN ('In','Out')");
                    table.CheckConstraint("CK_GateEvents_Source", "[Source] IN ('Scanner','Manual','Mobile')");
                    table.CheckConstraint("CK_GateEvents_Void", "([VoidedAt] IS NULL AND [VoidedBy] IS NULL AND [VoidReason] IS NULL) OR ([VoidedAt] IS NOT NULL AND [VoidedBy] IS NOT NULL AND [VoidReason] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GateEvents_AspNetUsers_RecordedBy",
                        column: x => x.RecordedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GateEvents_AspNetUsers_VoidedBy",
                        column: x => x.VoidedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GateEvents_GateDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "attendance",
                        principalTable: "GateDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GateEvents_StudentCards_CardId",
                        column: x => x.CardId,
                        principalSchema: "people",
                        principalTable: "StudentCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GateEvents_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "people",
                        principalTable: "Students",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GateDevices_CreatedBy",
                schema: "attendance",
                table: "GateDevices",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GateDevices_RevokedBy",
                schema: "attendance",
                table: "GateDevices",
                column: "RevokedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GateDevices_TokenHash",
                schema: "attendance",
                table: "GateDevices",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_CardId",
                schema: "attendance",
                table: "GateEvents",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_DeviceId",
                schema: "attendance",
                table: "GateEvents",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_LocalDate_StudentId",
                schema: "attendance",
                table: "GateEvents",
                columns: new[] { "LocalDate", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_OccurredAt",
                schema: "attendance",
                table: "GateEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_RecordedBy",
                schema: "attendance",
                table: "GateEvents",
                column: "RecordedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_StudentId_OccurredAt",
                schema: "attendance",
                table: "GateEvents",
                columns: new[] { "StudentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_VoidedBy",
                schema: "attendance",
                table: "GateEvents",
                column: "VoidedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_UploadedBy",
                schema: "people",
                table: "Photos",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCards_IssuedBy",
                schema: "people",
                table: "StudentCards",
                column: "IssuedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCards_RevokedBy",
                schema: "people",
                table: "StudentCards",
                column: "RevokedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCards_Token",
                schema: "people",
                table: "StudentCards",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_StudentCards_ActivePerStudent",
                schema: "people",
                table: "StudentCards",
                column: "StudentId",
                unique: true,
                filter: "[RevokedAt] IS NULL");

            // The "Administración institucional" profile now includes gate attendance. Accounts holding exactly the previous
            // administrator permission set receive the three new permissions, with an audit event per account, so they keep
            // matching the profile instead of becoming "custom". No other account changes.
            migrationBuilder.Sql("""
                DECLARE @admins TABLE (UserId uniqueidentifier PRIMARY KEY);
                INSERT INTO @admins (UserId)
                SELECT p.UserId FROM iam.AccountPermissions p
                GROUP BY p.UserId
                HAVING COUNT(*) = 12 AND SUM(CASE WHEN p.Permission IN ('accounts.manage','audit.read','people.read','people.manage',
                    'academic.read','academic.manage','enrollment.manage','finance.read','finance.charges','finance.collect',
                    'finance.reverse','communication.publish') THEN 1 ELSE 0 END) = 12;
                INSERT INTO iam.AccountPermissions (UserId, Permission)
                SELECT a.UserId, v.Permission FROM @admins a
                CROSS JOIN (VALUES ('attendance.gate'), ('attendance.read'), ('attendance.manage')) v(Permission);
                INSERT INTO audit.Events (Id, OccurredAt, ActorId, TargetId, TargetType, Action, Reason, Detail, TraceId)
                SELECT NEWID(), SYSDATETIMEOFFSET(), NULL, a.UserId, NULL, 'ProfilePermissionsExtended',
                    N'Actualización de la plataforma: Administración institucional incluye la asistencia de portería.',
                    'administrator', 'migration:StudentCardsAndGate'
                FROM @admins a;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GateEvents",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "Photos",
                schema: "people");

            migrationBuilder.DropTable(
                name: "GateDevices",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "StudentCards",
                schema: "people");
        }
    }
}
