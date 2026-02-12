using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FFXIV.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeLevelTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeLevelEntries",
                columns: table => new
                {
                    RecipeLevel = table.Column<int>(type: "integer", nullable: false),
                    ClassJobLevel = table.Column<int>(type: "integer", nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    Quality = table.Column<int>(type: "integer", nullable: false),
                    Durability = table.Column<int>(type: "integer", nullable: false),
                    SuggestedCraftsmanship = table.Column<int>(type: "integer", nullable: false),
                    SuggestedControl = table.Column<int>(type: "integer", nullable: false),
                    ProgressDivider = table.Column<int>(type: "integer", nullable: false),
                    ProgressModifier = table.Column<int>(type: "integer", nullable: false),
                    QualityDivider = table.Column<int>(type: "integer", nullable: false),
                    QualityModifier = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeLevelEntries", x => x.RecipeLevel);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeLevelEntries");
        }
    }
}
