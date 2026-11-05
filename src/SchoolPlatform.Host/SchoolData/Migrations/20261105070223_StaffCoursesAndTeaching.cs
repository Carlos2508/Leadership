using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class StaffCoursesAndTeaching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Staff",
                schema: "people",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.PersonId);
                    table.ForeignKey(
                        name: "FK_Staff_Persons_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "people",
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Courses",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Courses_Grades_GradeLevelId",
                        column: x => x.GradeLevelId,
                        principalSchema: "academic",
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Courses_Sections_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "academic",
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Courses_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "academic",
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Courses_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "academic",
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeachingAssignments",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeachingAssignments", x => x.Id);
                    table.CheckConstraint("CK_TeachingAssignments_Dates", "[EndsOn] IS NULL OR [EndsOn] >= [StartsOn]");
                    table.CheckConstraint("CK_TeachingAssignments_Role", "[Role] IN ('Lead','Assistant')");
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "academic",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_Staff_StaffId",
                        column: x => x.StaffId,
                        principalSchema: "people",
                        principalTable: "Staff",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_AcademicYearId_GradeLevelId_SectionId_SubjectId",
                schema: "academic",
                table: "Courses",
                columns: new[] { "AcademicYearId", "GradeLevelId", "SectionId", "SubjectId" },
                unique: true,
                filter: "[SectionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_AcademicYearId_GradeLevelId_SubjectId",
                schema: "academic",
                table: "Courses",
                columns: new[] { "AcademicYearId", "GradeLevelId", "SubjectId" },
                unique: true,
                filter: "[SectionId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_GradeLevelId",
                schema: "academic",
                table: "Courses",
                column: "GradeLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_SectionId",
                schema: "academic",
                table: "Courses",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_SubjectId",
                schema: "academic",
                table: "Courses",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_Code",
                schema: "academic",
                table: "Subjects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_Name",
                schema: "academic",
                table: "Subjects",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_CourseId_StaffId",
                schema: "academic",
                table: "TeachingAssignments",
                columns: new[] { "CourseId", "StaffId" },
                unique: true,
                filter: "[EndsOn] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_CreatedBy",
                schema: "academic",
                table: "TeachingAssignments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_StaffId_EndsOn",
                schema: "academic",
                table: "TeachingAssignments",
                columns: new[] { "StaffId", "EndsOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeachingAssignments",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Courses",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Staff",
                schema: "people");

            migrationBuilder.DropTable(
                name: "Subjects",
                schema: "academic");
        }
    }
}
