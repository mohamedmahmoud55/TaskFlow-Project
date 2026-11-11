using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskHistories_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TaskHistories_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskHistories_ActionType",
                table: "TaskHistories",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_TaskHistories_CreatedAt",
                table: "TaskHistories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TaskHistories_TaskId",
                table: "TaskHistories",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskHistories_UserId",
                table: "TaskHistories",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskHistories");
        }
    }
}
