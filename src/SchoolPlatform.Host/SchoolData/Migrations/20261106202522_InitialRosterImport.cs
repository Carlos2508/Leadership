using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class InitialRosterImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RosterBatches",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileSha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    PlanSha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RosterBatches_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterBatches_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "academic",
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RosterSourceAliases",
                schema: "people",
                columns: table => new
                {
                    Source = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Entity = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    LegacyId = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    Discriminator = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterSourceAliases", x => new { x.Source, x.Entity, x.LegacyId, x.Discriminator });
                    table.ForeignKey(
                        name: "FK_RosterSourceAliases_Persons_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "people",
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterSourceAliases_RosterBatches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "people",
                        principalTable: "RosterBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RosterBatches_AcademicYearId",
                schema: "people",
                table: "RosterBatches",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterBatches_ActorId",
                schema: "people",
                table: "RosterBatches",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterSourceAliases_BatchId",
                schema: "people",
                table: "RosterSourceAliases",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterSourceAliases_PersonId",
                schema: "people",
                table: "RosterSourceAliases",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RosterSourceAliases",
                schema: "people");

            migrationBuilder.DropTable(
                name: "RosterBatches",
                schema: "people");
        }
    }
}
