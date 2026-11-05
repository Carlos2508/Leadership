using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.IdentityAccess.Migrations
{
    /// <inheritdoc />
    public partial class SingleInstitutionAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolMemberships",
                schema: "iam");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "iam",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "iam",
                table: "AspNetUsers");

            migrationBuilder.CreateTable(
                name: "SchoolMemberships",
                schema: "iam",
                columns: table => new
                {
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolMemberships", x => new { x.SchoolId, x.UserId });
                    table.ForeignKey(
                        name: "FK_SchoolMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolMemberships_UserId",
                schema: "iam",
                table: "SchoolMemberships",
                column: "UserId");
        }
    }
}
