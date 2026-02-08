using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FFXIV.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeYield : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Yield",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Yield",
                table: "Recipes");
        }
    }
}
