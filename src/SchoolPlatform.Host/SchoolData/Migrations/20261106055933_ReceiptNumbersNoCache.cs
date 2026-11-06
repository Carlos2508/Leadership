using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptNumbersNoCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server caches sequence values; a service restart discards the cache and receipt numbers jump (e.g. 3 -> 51).
            // Receipts must be consecutive for the cashier and accounting, so the sequence is not cached.
            migrationBuilder.Sql("ALTER SEQUENCE finance.ReceiptNumbers NO CACHE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER SEQUENCE finance.ReceiptNumbers CACHE;");
        }
    }
}
