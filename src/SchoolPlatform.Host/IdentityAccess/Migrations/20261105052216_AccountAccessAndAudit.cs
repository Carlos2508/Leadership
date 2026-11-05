using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.IdentityAccess.Migrations
{
    /// <inheritdoc />
    public partial class AccountAccessAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "AccessGrants",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    SecretHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessGrants", x => x.Id);
                    table.CheckConstraint("CK_AccessGrants_Purpose", "[Purpose] IN ('Activation','Recovery')");
                    table.ForeignKey(
                        name: "FK_AccessGrants_AspNetUsers_IssuedBy",
                        column: x => x.IssuedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessGrants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountPermissions",
                schema: "iam",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permission = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountPermissions", x => new { x.UserId, x.Permission });
                    table.ForeignKey(
                        name: "FK_AccountPermissions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_IssuedBy",
                schema: "iam",
                table: "AccessGrants",
                column: "IssuedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_UserId_Purpose",
                schema: "iam",
                table: "AccessGrants",
                columns: new[] { "UserId", "Purpose" },
                unique: true,
                filter: "[ConsumedAt] IS NULL AND [RevokedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountPermissions_Permission_UserId",
                schema: "iam",
                table: "AccountPermissions",
                columns: new[] { "Permission", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_OccurredAt_Id",
                schema: "audit",
                table: "Events",
                columns: new[] { "OccurredAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Events_TargetId_OccurredAt",
                schema: "audit",
                table: "Events",
                columns: new[] { "TargetId", "OccurredAt" });

            migrationBuilder.Sql("""
                CREATE TRIGGER [audit].[Events_AppendOnly] ON [audit].[Events]
                INSTEAD OF UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51001, 'Audit events are append-only', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessGrants",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "AccountPermissions",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "Events",
                schema: "audit");
        }
    }
}
