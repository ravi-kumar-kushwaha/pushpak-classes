using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PushpakClasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReattemptRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReattemptRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuizId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReattemptRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReattemptRequests_Quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "Quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReattemptRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReattemptRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReattemptRequests_QuizId_UserId_Status",
                table: "ReattemptRequests",
                columns: new[] { "QuizId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReattemptRequests_ReviewedByUserId",
                table: "ReattemptRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReattemptRequests_Status",
                table: "ReattemptRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReattemptRequests_UserId",
                table: "ReattemptRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReattemptRequests");
        }
    }
}
