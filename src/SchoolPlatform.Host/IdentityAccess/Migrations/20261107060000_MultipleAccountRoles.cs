using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.IdentityAccess.Migrations
{
    /// <summary>An account may hold several access roles; the primary key becomes (UserId, RoleKey). Existing rows are kept.</summary>
    public partial class MultipleAccountRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_AccountRoleAssignments",
                schema: "iam",
                table: "AccountRoleAssignments");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AccountRoleAssignments",
                schema: "iam",
                table: "AccountRoleAssignments",
                columns: new[] { "UserId", "RoleKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep one role per account (alphabetical first) before restoring the single-role key.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT ROW_NUMBER() OVER (PARTITION BY UserId ORDER BY RoleKey) AS n FROM iam.AccountRoleAssignments)
                DELETE FROM ranked WHERE n > 1;
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_AccountRoleAssignments",
                schema: "iam",
                table: "AccountRoleAssignments");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AccountRoleAssignments",
                schema: "iam",
                table: "AccountRoleAssignments",
                column: "UserId");
        }
    }
}
