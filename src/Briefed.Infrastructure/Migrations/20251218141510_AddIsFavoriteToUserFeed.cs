using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Briefed.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFavoriteToUserFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "UserFeeds",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "UserFeeds");
        }
    }
}
