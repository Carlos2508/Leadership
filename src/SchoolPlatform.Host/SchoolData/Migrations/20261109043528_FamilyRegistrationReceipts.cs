using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class FamilyRegistrationReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyRegistrations",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyRegistrations", x => x.Id);
                    table.CheckConstraint("CK_FamilyRegistration_Result", "ISJSON([ResultJson]) = 1");
                    table.ForeignKey(
                        name: "FK_FamilyRegistrations_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyRegistrations_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "academic",
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRegistrations_AcademicYearId",
                schema: "people",
                table: "FamilyRegistrations",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyRegistrations_ActorId",
                schema: "people",
                table: "FamilyRegistrations",
                column: "ActorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyRegistrations",
                schema: "people");
        }
    }
}
