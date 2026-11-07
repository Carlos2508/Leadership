using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPlatform.Host.SchoolData.Migrations
{
    /// <inheritdoc />
    public partial class ChatReceiptsAndPresence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                schema: "chat",
                table: "Messages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadAt",
                schema: "chat",
                table: "Messages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MemberDeliveredSequence",
                schema: "chat",
                table: "Conversations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TeacherDeliveredSequence",
                schema: "chat",
                table: "Conversations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "Presence",
                schema: "chat",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastActiveAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActiveUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presence", x => new { x.UserId, x.ClientId });
                    table.CheckConstraint("CK_ChatPresence_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_Presence_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatMessage_Receipts",
                schema: "chat",
                table: "Messages",
                sql: "([DeliveredAt] IS NULL OR [DeliveredAt] >= [SentAt]) AND ([ReadAt] IS NULL OR ([DeliveredAt] IS NOT NULL AND [ReadAt] >= [DeliveredAt]))");

            // Preserve already-confirmed read progress without inventing historical delivery/read times.
            migrationBuilder.Sql("UPDATE [chat].[Conversations] SET [TeacherDeliveredSequence]=[TeacherReadSequence], [MemberDeliveredSequence]=[MemberReadSequence];");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Chat_Delivery",
                schema: "chat",
                table: "Conversations",
                sql: "[TeacherDeliveredSequence] >= [TeacherReadSequence] AND [MemberDeliveredSequence] >= [MemberReadSequence] AND [TeacherDeliveredSequence] <= [LastSequence] AND [MemberDeliveredSequence] <= [LastSequence]");

            migrationBuilder.CreateIndex(
                name: "IX_Presence_UserId_ActiveUntil",
                schema: "chat",
                table: "Presence",
                columns: new[] { "UserId", "ActiveUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Presence",
                schema: "chat");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatMessage_Receipts",
                schema: "chat",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Chat_Delivery",
                schema: "chat",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                schema: "chat",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                schema: "chat",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MemberDeliveredSequence",
                schema: "chat",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "TeacherDeliveredSequence",
                schema: "chat",
                table: "Conversations");
        }
    }
}
