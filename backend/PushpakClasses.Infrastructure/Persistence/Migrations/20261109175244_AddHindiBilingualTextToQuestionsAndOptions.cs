using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PushpakClasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHindiBilingualTextToQuestionsAndOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TextHi",
                table: "Questions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextHi",
                table: "Options",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TextHi",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TextHi",
                table: "Options");
        }
    }
}
