using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class CourseTimetables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "timetable");

            migrationBuilder.CreateTable(
                name: "Operations",
                schema: "timetable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Hash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                    table.CheckConstraint("CK_TimetableOperation_Json", "ISJSON([ResultJson]) = 1");
                    table.ForeignKey(
                        name: "FK_Operations_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                schema: "timetable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Plans_Grades_GradeLevelId",
                        column: x => x.GradeLevelId,
                        principalSchema: "academic",
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Plans_Sections_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "academic",
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Plans_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "academic",
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                schema: "timetable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Templates",
                schema: "timetable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StructureJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                    table.CheckConstraint("CK_TimetableTemplate_Json", "ISJSON([StructureJson]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "Revisions",
                schema: "timetable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    UntilExclusive = table.Column<DateOnly>(type: "date", nullable: false),
                    StructureJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Revisions", x => x.Id);
                    table.CheckConstraint("CK_TimetableRevision_Dates", "[UntilExclusive] >= [StartsOn] AND ISJSON([StructureJson]) = 1");
                    table.CheckConstraint("CK_TimetableRevision_State", "[State] IN ('Draft','Published') AND ([State] = 'Draft' AND [PublishedAt] IS NULL OR [State] = 'Published' AND [PublishedAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Revisions_AspNetUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Revisions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "timetable",
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Entries",
                schema: "timetable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    Start = table.Column<int>(type: "int", nullable: false),
                    End = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Teacher = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    Room = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entries", x => x.Id);
                    table.CheckConstraint("CK_TimetableEntry_Kind", "([Kind] = 'Lesson' AND [CourseId] IS NOT NULL) OR ([Kind] = 'Break' AND [CourseId] IS NULL AND [AssignmentId] IS NULL AND [StaffId] IS NULL AND [RoomId] IS NULL)");
                    table.CheckConstraint("CK_TimetableEntry_Time", "[Day] BETWEEN 1 AND 7 AND [Start] >= 0 AND [End] <= 1440 AND [Start] < [End]");
                    table.ForeignKey(
                        name: "FK_Entries_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "academic",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Entries_Revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalSchema: "timetable",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Entries_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "timetable",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Entries_Staff_StaffId",
                        column: x => x.StaffId,
                        principalSchema: "people",
                        principalTable: "Staff",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Entries_TeachingAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "academic",
                        principalTable: "TeachingAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Entries_AssignmentId",
                schema: "timetable",
                table: "Entries",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_CourseId",
                schema: "timetable",
                table: "Entries",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_Day_Start_End",
                schema: "timetable",
                table: "Entries",
                columns: new[] { "Day", "Start", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_Entries_RevisionId_PeriodId_Day",
                schema: "timetable",
                table: "Entries",
                columns: new[] { "RevisionId", "PeriodId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entries_RoomId",
                schema: "timetable",
                table: "Entries",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_StaffId",
                schema: "timetable",
                table: "Entries",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Operations_ActorId",
                schema: "timetable",
                table: "Operations",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_AcademicYearId_GradeLevelId_SectionId",
                schema: "timetable",
                table: "Plans",
                columns: new[] { "AcademicYearId", "GradeLevelId", "SectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plans_GradeLevelId",
                schema: "timetable",
                table: "Plans",
                column: "GradeLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_SectionId",
                schema: "timetable",
                table: "Plans",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_PlanId",
                schema: "timetable",
                table: "Revisions",
                column: "PlanId",
                unique: true,
                filter: "[State] = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_PlanId_StartsOn",
                schema: "timetable",
                table: "Revisions",
                columns: new[] { "PlanId", "StartsOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_UpdatedBy",
                schema: "timetable",
                table: "Revisions",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_Name",
                schema: "timetable",
                table: "Rooms",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Templates_Name",
                schema: "timetable",
                table: "Templates",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Entries",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "Operations",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "Templates",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "Revisions",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "Rooms",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "Plans",
                schema: "timetable");
        }
    }
}
