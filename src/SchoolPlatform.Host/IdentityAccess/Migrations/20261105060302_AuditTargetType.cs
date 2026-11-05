using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.IdentityAccess.Migrations
{
    /// <inheritdoc />
    public partial class AuditTargetType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                schema: "audit",
                table: "Events",
                type: "varchar(40)",
                unicode: false,
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetType",
                schema: "audit",
                table: "Events");
        }
    }
}
