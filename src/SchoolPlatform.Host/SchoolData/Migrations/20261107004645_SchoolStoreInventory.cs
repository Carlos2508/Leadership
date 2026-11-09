using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class SchoolStoreInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Minimum = table.Column<int>(type: "int", nullable: false),
                    OnHand = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreationHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_InventoryProduct_Stock", "[OnHand] BETWEEN 0 AND 1000000 AND [Minimum] BETWEEN 0 AND 1000000 AND [LastSequence] >= 0 AND ([IsActive] = 1 OR [OnHand] = 0)");
                    table.CheckConstraint("CK_InventoryProduct_Text", "LEN(LTRIM(RTRIM([Code]))) > 0 AND LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.ForeignKey(
                        name: "FK_Products_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Movements",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    Before = table.Column<int>(type: "int", nullable: false),
                    After = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestHash = table.Column<byte[]>(type: "binary(32)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.Id);
                    table.CheckConstraint("CK_InventoryMovement_Kind", "([Kind] = 'In' AND [Delta] > 0) OR ([Kind] = 'Out' AND [Delta] < 0) OR [Kind] = 'Count'");
                    table.CheckConstraint("CK_InventoryMovement_Reason", "LEN(LTRIM(RTRIM([Reason]))) >= 8");
                    table.CheckConstraint("CK_InventoryMovement_Stock", "[Before] BETWEEN 0 AND 1000000 AND [After] BETWEEN 0 AND 1000000 AND [After] = [Before] + [Delta] AND [Sequence] > 0");
                    table.ForeignKey(
                        name: "FK_Movements_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movements_ActorId",
                schema: "inventory",
                table: "Movements",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_ProductId_Sequence",
                schema: "inventory",
                table: "Movements",
                columns: new[] { "ProductId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                schema: "inventory",
                table: "Products",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CreatedBy",
                schema: "inventory",
                table: "Products",
                column: "CreatedBy");

            migrationBuilder.Sql("CREATE TRIGGER [inventory].[Movements_AppendOnly] ON [inventory].[Movements] AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted) THROW 51001, 'El historial de inventario no se modifica ni elimina.', 1; END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "inventory");
        }
    }
}
