using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Briefed.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedArticleIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "DeletedArticles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedArticles_DeletedAt",
                table: "DeletedArticles",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedArticles_Url",
                table: "DeletedArticles",
                column: "Url");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeletedArticles_DeletedAt",
                table: "DeletedArticles");

            migrationBuilder.DropIndex(
                name: "IX_DeletedArticles_Url",
                table: "DeletedArticles");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "DeletedArticles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);
        }
    }
}
