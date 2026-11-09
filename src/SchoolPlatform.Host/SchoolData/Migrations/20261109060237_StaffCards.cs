using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class StaffCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffCards",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IssuedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffCards_AspNetUsers_IssuedBy",
                        column: x => x.IssuedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffCards_AspNetUsers_RevokedBy",
                        column: x => x.RevokedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffCards_Staff_StaffId",
                        column: x => x.StaffId,
                        principalSchema: "people",
                        principalTable: "Staff",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffCards_IssuedBy",
                schema: "people",
                table: "StaffCards",
                column: "IssuedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StaffCards_RevokedBy",
                schema: "people",
                table: "StaffCards",
                column: "RevokedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StaffCards_Token",
                schema: "people",
                table: "StaffCards",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_StaffCards_ActivePerStaff",
                schema: "people",
                table: "StaffCards",
                column: "StaffId",
                unique: true,
                filter: "[RevokedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffCards",
                schema: "people");
        }
    }
}
