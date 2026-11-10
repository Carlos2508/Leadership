using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class GradeActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    MaxPoints = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RemovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RemoveReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.CheckConstraint("CK_Activities_Points", "[MaxPoints] > 0 AND [MaxPoints] <= 100");
                    table.ForeignKey(
                        name: "FK_Activities_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activities_AspNetUsers_RemovedBy",
                        column: x => x.RemovedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activities_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "learning",
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activities_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "academic",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activities_Periods_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "academic",
                        principalTable: "Periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityTemplates",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityTemplates_AspNetUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityScores",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityScores", x => x.Id);
                    table.CheckConstraint("CK_ActivityScores_Status", "[Status] IN ('Scored','Missing','Excused')");
                    table.CheckConstraint("CK_ActivityScores_Value", "([Status] = 'Scored' AND [Points] IS NOT NULL AND [Points] >= 0) OR ([Status] <> 'Scored' AND [Points] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ActivityScores_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalSchema: "assessment",
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityScores_AspNetUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityScores_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "people",
                        principalTable: "Students",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityScoreRevisions",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousPoints = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    PreviousStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    NewPoints = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    NewStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityScoreRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityScoreRevisions_ActivityScores_ScoreId",
                        column: x => x.ScoreId,
                        principalSchema: "assessment",
                        principalTable: "ActivityScores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityScoreRevisions_AspNetUsers_ChangedBy",
                        column: x => x.ChangedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_AssignmentId",
                schema: "assessment",
                table: "Activities",
                column: "AssignmentId",
                unique: true,
                filter: "[AssignmentId] IS NOT NULL AND [RemovedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_CourseId_PeriodId_Sequence",
                schema: "assessment",
                table: "Activities",
                columns: new[] { "CourseId", "PeriodId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_CreatedBy",
                schema: "assessment",
                table: "Activities",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_PeriodId",
                schema: "assessment",
                table: "Activities",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_RemovedBy",
                schema: "assessment",
                table: "Activities",
                column: "RemovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityScoreRevisions_ChangedBy",
                schema: "assessment",
                table: "ActivityScoreRevisions",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityScoreRevisions_ScoreId_ChangedAt",
                schema: "assessment",
                table: "ActivityScoreRevisions",
                columns: new[] { "ScoreId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityScores_ActivityId_StudentId",
                schema: "assessment",
                table: "ActivityScores",
                columns: new[] { "ActivityId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityScores_StudentId",
                schema: "assessment",
                table: "ActivityScores",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityScores_UpdatedBy",
                schema: "assessment",
                table: "ActivityScores",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTemplates_Name",
                schema: "assessment",
                table: "ActivityTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTemplates_UpdatedBy",
                schema: "assessment",
                table: "ActivityTemplates",
                column: "UpdatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityScoreRevisions",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "ActivityTemplates",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "ActivityScores",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "Activities",
                schema: "assessment");
        }
    }
}
