using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FFXIV.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentSyncFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanBeHQ",
                table: "Equipment",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IconPath",
                table: "Equipment",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Rarity",
                table: "Equipment",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecipeId",
                table: "Equipment",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceDetails",
                table: "Equipment",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "Equipment",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_EquipSlot",
                table: "Equipment",
                column: "EquipSlot");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_EquipSlot_ClassJobCategory_ItemLevel",
                table: "Equipment",
                columns: new[] { "EquipSlot", "ClassJobCategory", "ItemLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_RecipeId",
                table: "Equipment",
                column: "RecipeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_Recipes_RecipeId",
                table: "Equipment",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "RecipeId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_Recipes_RecipeId",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_EquipSlot",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_EquipSlot_ClassJobCategory_ItemLevel",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_RecipeId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "CanBeHQ",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "IconPath",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Rarity",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "RecipeId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "SourceDetails",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Equipment");
        }
    }
}
