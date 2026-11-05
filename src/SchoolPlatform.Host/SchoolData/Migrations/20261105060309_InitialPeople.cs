using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class InitialPeople : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "people");

            migrationBuilder.CreateTable(
                name: "Persons",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GivenNames = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FamilyNames = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreferredName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persons", x => x.Id);
                    table.CheckConstraint("CK_Persons_Status", "[Status] IN ('Active','Archived')");
                });

            migrationBuilder.CreateTable(
                name: "AccountLinks",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LinkedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnlinkedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UnlinkedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountLinks_AspNetUsers_LinkedBy",
                        column: x => x.LinkedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountLinks_AspNetUsers_UnlinkedBy",
                        column: x => x.UnlinkedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountLinks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountLinks_Persons_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "people",
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Guardians",
                schema: "people",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guardians", x => x.PersonId);
                    table.ForeignKey(
                        name: "FK_Guardians_Persons_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "people",
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonContacts",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonContacts", x => x.Id);
                    table.CheckConstraint("CK_PersonContacts_Kind", "[Kind] IN ('Phone','Email','Address')");
                    table.ForeignKey(
                        name: "FK_PersonContacts_Persons_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "people",
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                schema: "people",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.PersonId);
                    table.ForeignKey(
                        name: "FK_Students_Persons_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "people",
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuardianRelationships",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuardianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuardianRelationships", x => x.Id);
                    table.CheckConstraint("CK_GuardianRelationships_Distinct", "[StudentId] <> [GuardianId]");
                    table.CheckConstraint("CK_GuardianRelationships_End", "([EndedAt] IS NULL AND [EndedBy] IS NULL) OR ([EndedAt] IS NOT NULL AND [EndedBy] IS NOT NULL)");
                    table.CheckConstraint("CK_GuardianRelationships_Kind", "[Kind] IN ('Mother','Father','Grandparent','Sibling','OtherRelative','LegalGuardian','Other')");
                    table.ForeignKey(
                        name: "FK_GuardianRelationships_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianRelationships_AspNetUsers_EndedBy",
                        column: x => x.EndedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianRelationships_Guardians_GuardianId",
                        column: x => x.GuardianId,
                        principalSchema: "people",
                        principalTable: "Guardians",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianRelationships_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "people",
                        principalTable: "Students",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RelationshipFaculties",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Faculty = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GrantedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelationshipFaculties", x => x.Id);
                    table.CheckConstraint("CK_RelationshipFaculties_Faculty", "[Faculty] IN ('AcademicRecords','Finance','BillingResponsible','EmergencyContact','Pickup','Communications')");
                    table.ForeignKey(
                        name: "FK_RelationshipFaculties_AspNetUsers_GrantedBy",
                        column: x => x.GrantedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RelationshipFaculties_AspNetUsers_RevokedBy",
                        column: x => x.RevokedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RelationshipFaculties_GuardianRelationships_RelationshipId",
                        column: x => x.RelationshipId,
                        principalSchema: "people",
                        principalTable: "GuardianRelationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountLinks_LinkedBy",
                schema: "people",
                table: "AccountLinks",
                column: "LinkedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLinks_PersonId",
                schema: "people",
                table: "AccountLinks",
                column: "PersonId",
                unique: true,
                filter: "[UnlinkedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLinks_UnlinkedBy",
                schema: "people",
                table: "AccountLinks",
                column: "UnlinkedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLinks_UserId",
                schema: "people",
                table: "AccountLinks",
                column: "UserId",
                unique: true,
                filter: "[UnlinkedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianRelationships_CreatedBy",
                schema: "people",
                table: "GuardianRelationships",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianRelationships_EndedBy",
                schema: "people",
                table: "GuardianRelationships",
                column: "EndedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianRelationships_GuardianId_EndedAt",
                schema: "people",
                table: "GuardianRelationships",
                columns: new[] { "GuardianId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianRelationships_StudentId_GuardianId",
                schema: "people",
                table: "GuardianRelationships",
                columns: new[] { "StudentId", "GuardianId" },
                unique: true,
                filter: "[EndedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PersonContacts_PersonId",
                schema: "people",
                table: "PersonContacts",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Persons_FamilyNames_GivenNames",
                schema: "people",
                table: "Persons",
                columns: new[] { "FamilyNames", "GivenNames" });

            migrationBuilder.CreateIndex(
                name: "IX_RelationshipFaculties_GrantedBy",
                schema: "people",
                table: "RelationshipFaculties",
                column: "GrantedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RelationshipFaculties_RelationshipId_Faculty",
                schema: "people",
                table: "RelationshipFaculties",
                columns: new[] { "RelationshipId", "Faculty" },
                unique: true,
                filter: "[RevokedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RelationshipFaculties_RevokedBy",
                schema: "people",
                table: "RelationshipFaculties",
                column: "RevokedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentCode",
                schema: "people",
                table: "Students",
                column: "StudentCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountLinks",
                schema: "people");

            migrationBuilder.DropTable(
                name: "PersonContacts",
                schema: "people");

            migrationBuilder.DropTable(
                name: "RelationshipFaculties",
                schema: "people");

            migrationBuilder.DropTable(
                name: "GuardianRelationships",
                schema: "people");

            migrationBuilder.DropTable(
                name: "Guardians",
                schema: "people");

            migrationBuilder.DropTable(
                name: "Students",
                schema: "people");

            migrationBuilder.DropTable(
                name: "Persons",
                schema: "people");
        }
    }
}
