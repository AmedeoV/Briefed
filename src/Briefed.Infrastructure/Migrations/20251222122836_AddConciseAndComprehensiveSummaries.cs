using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Briefed.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConciseAndComprehensiveSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComprehensiveContent",
                table: "Summaries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConciseContent",
                table: "Summaries",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComprehensiveContent",
                table: "Summaries");

            migrationBuilder.DropColumn(
                name: "ConciseContent",
                table: "Summaries");
        }
    }
}
