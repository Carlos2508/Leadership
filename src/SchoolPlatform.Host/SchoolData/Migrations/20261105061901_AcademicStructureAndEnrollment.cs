using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class AcademicStructureAndEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "academic");

            migrationBuilder.CreateTable(
                name: "Grades",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Years",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Years", x => x.Id);
                    table.CheckConstraint("CK_Years_Dates", "[EndsOn] > [StartsOn]");
                    table.CheckConstraint("CK_Years_Status", "[Status] IN ('Draft','EnrollmentOpen','Active','Closed')");
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    EnrolledOn = table.Column<DateOnly>(type: "date", nullable: false),
                    WithdrawnOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.Id);
                    table.CheckConstraint("CK_Enrollments_Status", "[Status] IN ('Active','Withdrawn','Cancelled','Completed')");
                    table.CheckConstraint("CK_Enrollments_Withdrawal", "([Status] = 'Withdrawn' AND [WithdrawnOn] IS NOT NULL) OR ([Status] <> 'Withdrawn' AND [WithdrawnOn] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Enrollments_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "people",
                        principalTable: "Students",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "academic",
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Periods",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Periods", x => x.Id);
                    table.CheckConstraint("CK_Periods_Dates", "[EndsOn] >= [StartsOn]");
                    table.CheckConstraint("CK_Periods_Sequence", "[Sequence] BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_Periods_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "academic",
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Shift = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.CheckConstraint("CK_Sections_Capacity", "[Capacity] IS NULL OR [Capacity] BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_Sections_Grades_GradeLevelId",
                        column: x => x.GradeLevelId,
                        principalSchema: "academic",
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sections_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "academic",
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EnrollmentPlacements",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentPlacements", x => x.Id);
                    table.CheckConstraint("CK_EnrollmentPlacements_Dates", "[EndsOn] IS NULL OR [EndsOn] >= [StartsOn]");
                    table.ForeignKey(
                        name: "FK_EnrollmentPlacements_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentPlacements_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "academic",
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentPlacements_Grades_GradeLevelId",
                        column: x => x.GradeLevelId,
                        principalSchema: "academic",
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentPlacements_Sections_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "academic",
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentPlacements_CreatedBy",
                schema: "academic",
                table: "EnrollmentPlacements",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentPlacements_EnrollmentId",
                schema: "academic",
                table: "EnrollmentPlacements",
                column: "EnrollmentId",
                unique: true,
                filter: "[EndsOn] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentPlacements_GradeLevelId_EndsOn",
                schema: "academic",
                table: "EnrollmentPlacements",
                columns: new[] { "GradeLevelId", "EndsOn" });

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentPlacements_SectionId_EndsOn",
                schema: "academic",
                table: "EnrollmentPlacements",
                columns: new[] { "SectionId", "EndsOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_AcademicYearId_Status",
                schema: "academic",
                table: "Enrollments",
                columns: new[] { "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_CreatedBy",
                schema: "academic",
                table: "Enrollments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId_AcademicYearId",
                schema: "academic",
                table: "Enrollments",
                columns: new[] { "StudentId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grades_Code",
                schema: "academic",
                table: "Grades",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grades_Name",
                schema: "academic",
                table: "Grades",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Periods_AcademicYearId_Sequence",
                schema: "academic",
                table: "Periods",
                columns: new[] { "AcademicYearId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sections_AcademicYearId_GradeLevelId_Name",
                schema: "academic",
                table: "Sections",
                columns: new[] { "AcademicYearId", "GradeLevelId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sections_GradeLevelId",
                schema: "academic",
                table: "Sections",
                column: "GradeLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Years_IsDefault",
                schema: "academic",
                table: "Years",
                column: "IsDefault",
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Years_Label",
                schema: "academic",
                table: "Years",
                column: "Label",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnrollmentPlacements",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Periods",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Enrollments",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Sections",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Grades",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Years",
                schema: "academic");
        }
    }
}
