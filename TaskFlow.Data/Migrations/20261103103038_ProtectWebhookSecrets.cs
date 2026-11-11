using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProtectWebhookSecrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Secret",
                table: "WebhookSubscriptions",
                newName: "ProtectedSecret");

            migrationBuilder.AlterColumn<string>(
                name: "ProtectedSecret",
                table: "WebhookSubscriptions",
                type: "nvarchar(max)",
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                nullable: false,
                oldNullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProtectedSecret",
                table: "WebhookSubscriptions",
                type: "nvarchar(512)",
                maxLength: 512,
                oldType: "nvarchar(max)",
                nullable: false,
                oldNullable: false);

            migrationBuilder.RenameColumn(
                name: "ProtectedSecret",
                table: "WebhookSubscriptions",
                newName: "Secret");
        }
    }
}
