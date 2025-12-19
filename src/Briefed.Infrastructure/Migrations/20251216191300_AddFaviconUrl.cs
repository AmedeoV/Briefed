using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Briefed.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFaviconUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaviconUrl",
                table: "Feeds",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaviconUrl",
                table: "Feeds");
        }
    }
}
