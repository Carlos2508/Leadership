using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class CommunicationAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "communication");

            migrationBuilder.CreateTable(
                name: "Announcements",
                schema: "communication",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AudienceKind = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AudienceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ForStudents = table.Column<bool>(type: "bit", nullable: false),
                    ForGuardians = table.Column<bool>(type: "bit", nullable: false),
                    ForStaff = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.CheckConstraint("CK_Announcements_Audience", "[AudienceKind] IN ('School','Grade','Course')");
                    table.CheckConstraint("CK_Announcements_Status", "[Status] IN ('Draft','Published','Archived')");
                    table.ForeignKey(
                        name: "FK_Announcements_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Announcements_AspNetUsers_PublishedBy",
                        column: x => x.PublishedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Announcements_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "academic",
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementReads",
                schema: "communication",
                columns: table => new
                {
                    AnnouncementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementReads", x => new { x.AnnouncementId, x.UserId });
                    table.ForeignKey(
                        name: "FK_AnnouncementReads_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalSchema: "communication",
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnnouncementReads_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementReads_UserId",
                schema: "communication",
                table: "AnnouncementReads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_AcademicYearId",
                schema: "communication",
                table: "Announcements",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CreatedBy",
                schema: "communication",
                table: "Announcements",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_PublishedBy",
                schema: "communication",
                table: "Announcements",
                column: "PublishedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_Status_PublishedAt",
                schema: "communication",
                table: "Announcements",
                columns: new[] { "Status", "PublishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnouncementReads",
                schema: "communication");

            migrationBuilder.DropTable(
                name: "Announcements",
                schema: "communication");
        }
    }
}
