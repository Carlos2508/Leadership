using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class GradesAndPublications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "assessment");

            migrationBuilder.CreateTable(
                name: "PeriodScores",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(7,3)", precision: 7, scale: 3, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodScores", x => x.Id);
                    table.CheckConstraint("CK_PeriodScores_Status", "[Status] IN ('Scored','Missing','Excused')");
                    table.CheckConstraint("CK_PeriodScores_Value", "([Status] = 'Scored' AND [Score] IS NOT NULL AND [Score] BETWEEN 0 AND 100) OR ([Status] <> 'Scored' AND [Score] IS NULL)");
                    table.ForeignKey(
                        name: "FK_PeriodScores_AspNetUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodScores_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "academic",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodScores_Periods_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "academic",
                        principalTable: "Periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodScores_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "people",
                        principalTable: "Students",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Publications",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CutoffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Publications_AspNetUsers_PublishedBy",
                        column: x => x.PublishedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Publications_ClearanceCutoffs_CutoffId",
                        column: x => x.CutoffId,
                        principalSchema: "finance",
                        principalTable: "ClearanceCutoffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Publications_Periods_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "academic",
                        principalTable: "Periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Publications_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "academic",
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScoreRevisions",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousScore = table.Column<decimal>(type: "decimal(7,3)", precision: 7, scale: 3, nullable: true),
                    PreviousStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    NewScore = table.Column<decimal>(type: "decimal(7,3)", precision: 7, scale: 3, nullable: true),
                    NewStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreRevisions_AspNetUsers_ChangedBy",
                        column: x => x.ChangedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScoreRevisions_PeriodScores_ScoreId",
                        column: x => x.ScoreId,
                        principalSchema: "assessment",
                        principalTable: "PeriodScores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodScores_CourseId_PeriodId_StudentId",
                schema: "assessment",
                table: "PeriodScores",
                columns: new[] { "CourseId", "PeriodId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodScores_PeriodId",
                schema: "assessment",
                table: "PeriodScores",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodScores_StudentId_PeriodId",
                schema: "assessment",
                table: "PeriodScores",
                columns: new[] { "StudentId", "PeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodScores_UpdatedBy",
                schema: "assessment",
                table: "PeriodScores",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_AcademicYearId",
                schema: "assessment",
                table: "Publications",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_CutoffId",
                schema: "assessment",
                table: "Publications",
                column: "CutoffId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_PeriodId",
                schema: "assessment",
                table: "Publications",
                column: "PeriodId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Publications_PublishedBy",
                schema: "assessment",
                table: "Publications",
                column: "PublishedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreRevisions_ChangedBy",
                schema: "assessment",
                table: "ScoreRevisions",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreRevisions_ScoreId_ChangedAt",
                schema: "assessment",
                table: "ScoreRevisions",
                columns: new[] { "ScoreId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Publications",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "ScoreRevisions",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "PeriodScores",
                schema: "assessment");
        }
    }
}
