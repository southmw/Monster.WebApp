using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monster.WebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddHtmlContentSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHtml",
                table: "Posts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "Posts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHtml",
                table: "Comments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHtml",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "IsHtml",
                table: "Comments");
        }
    }
}
