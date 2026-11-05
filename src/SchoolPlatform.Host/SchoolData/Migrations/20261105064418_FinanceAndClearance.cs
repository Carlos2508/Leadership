using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class FinanceAndClearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateSequence(
                name: "ReceiptNumbers",
                schema: "finance");

            migrationBuilder.CreateTable(
                name: "BillingPeriods",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingPeriods", x => x.Id);
                    table.CheckConstraint("CK_BillingPeriods_Sequence", "[Sequence] BETWEEN 1 AND 24");
                    table.ForeignKey(
                        name: "FK_BillingPeriods_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "academic",
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClearanceCutoffs",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ThroughSequence = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClearanceCutoffs", x => x.Id);
                    table.CheckConstraint("CK_ClearanceCutoffs_Sequence", "[ThroughSequence] BETWEEN 1 AND 24");
                    table.ForeignKey(
                        name: "FK_ClearanceCutoffs_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClearanceCutoffs_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "academic",
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentAssets",
                schema: "finance",
                columns: table => new
                {
                    Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAssets", x => x.Sha256);
                });

            migrationBuilder.CreateTable(
                name: "FeeConcepts",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    CountsForClearance = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeConcepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptNumber = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR finance.ReceiptNumbers"),
                    IdempotencyKey = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    PayerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Method = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReversedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReversedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceiptSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Payments_Reversal", "([ReversedAt] IS NULL AND [ReversedBy] IS NULL) OR ([ReversedAt] IS NOT NULL AND [ReversedBy] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Payments_AspNetUsers_RecordedBy",
                        column: x => x.RecordedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_AspNetUsers_ReversedBy",
                        column: x => x.ReversedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Charges",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConceptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Occurrence = table.Column<int>(type: "int", nullable: false),
                    ObligationKey = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Charges", x => x.Id);
                    table.CheckConstraint("CK_Charges_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Charges_Occurrence", "[Occurrence] >= 1");
                    table.CheckConstraint("CK_Charges_Void", "([VoidedAt] IS NULL AND [VoidedBy] IS NULL) OR ([VoidedAt] IS NOT NULL AND [VoidedBy] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Charges_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_AspNetUsers_VoidedBy",
                        column: x => x.VoidedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_BillingPeriods_BillingPeriodId",
                        column: x => x.BillingPeriodId,
                        principalSchema: "finance",
                        principalTable: "BillingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "academic",
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_FeeConcepts_ConceptId",
                        column: x => x.ConceptId,
                        principalSchema: "finance",
                        principalTable: "FeeConcepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "people",
                        principalTable: "Students",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "academic",
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChargeAdjustments",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeAdjustments", x => x.Id);
                    table.CheckConstraint("CK_ChargeAdjustments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_ChargeAdjustments_Kind", "[Kind] IN ('Scholarship','Discount','Waiver')");
                    table.ForeignKey(
                        name: "FK_ChargeAdjustments_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChargeAdjustments_Charges_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "finance",
                        principalTable: "Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.Id);
                    table.CheckConstraint("CK_PaymentAllocations_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Charges_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "finance",
                        principalTable: "Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "finance",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingPeriods_AcademicYearId_Sequence",
                schema: "finance",
                table: "BillingPeriods",
                columns: new[] { "AcademicYearId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChargeAdjustments_ChargeId",
                schema: "finance",
                table: "ChargeAdjustments",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargeAdjustments_CreatedBy",
                schema: "finance",
                table: "ChargeAdjustments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_AcademicYearId",
                schema: "finance",
                table: "Charges",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_BillingPeriodId",
                schema: "finance",
                table: "Charges",
                column: "BillingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_ConceptId",
                schema: "finance",
                table: "Charges",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_CreatedBy",
                schema: "finance",
                table: "Charges",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_EnrollmentId_BillingPeriodId",
                schema: "finance",
                table: "Charges",
                columns: new[] { "EnrollmentId", "BillingPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_ObligationKey",
                schema: "finance",
                table: "Charges",
                column: "ObligationKey",
                unique: true,
                filter: "[VoidedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_StudentId_AcademicYearId",
                schema: "finance",
                table: "Charges",
                columns: new[] { "StudentId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_VoidedBy",
                schema: "finance",
                table: "Charges",
                column: "VoidedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ClearanceCutoffs_AcademicYearId_Name",
                schema: "finance",
                table: "ClearanceCutoffs",
                columns: new[] { "AcademicYearId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClearanceCutoffs_CreatedBy",
                schema: "finance",
                table: "ClearanceCutoffs",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FeeConcepts_Code",
                schema: "finance",
                table: "FeeConcepts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeConcepts_Name",
                schema: "finance",
                table: "FeeConcepts",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_ChargeId",
                schema: "finance",
                table: "PaymentAllocations",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_PaymentId_ChargeId",
                schema: "finance",
                table: "PaymentAllocations",
                columns: new[] { "PaymentId", "ChargeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                schema: "finance",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReceiptNumber",
                schema: "finance",
                table: "Payments",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReceivedOn",
                schema: "finance",
                table: "Payments",
                column: "ReceivedOn");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RecordedBy",
                schema: "finance",
                table: "Payments",
                column: "RecordedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReversedBy",
                schema: "finance",
                table: "Payments",
                column: "ReversedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargeAdjustments",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "ClearanceCutoffs",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "DocumentAssets",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "PaymentAllocations",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "Charges",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "BillingPeriods",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "FeeConcepts",
                schema: "finance");

            migrationBuilder.DropSequence(
                name: "ReceiptNumbers",
                schema: "finance");
        }
    }
}
