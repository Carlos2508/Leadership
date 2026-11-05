using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class LegacyIdentityMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegacyIdentities",
                schema: "people",
                columns: table => new
                {
                    Source = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Entity = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FileSha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyIdentities", x => new { x.Source, x.Entity, x.LegacyId });
                    table.CheckConstraint("CK_LegacyIdentities_Entity", "[Entity] IN ('student','guardian')");
                    table.ForeignKey(
                        name: "FK_LegacyIdentities_Persons_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "people",
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyIdentities_PersonId",
                schema: "people",
                table: "LegacyIdentities",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegacyIdentities",
                schema: "people");
        }
    }
}
