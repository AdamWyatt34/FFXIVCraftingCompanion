using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FFXIV.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConsumableBonuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsumableBonuses",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Stat = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsRelative = table.Column<bool>(type: "bit", nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false),
                    Max = table.Column<int>(type: "int", nullable: false),
                    ValueHQ = table.Column<int>(type: "int", nullable: false),
                    MaxHQ = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumableBonuses", x => new { x.ItemId, x.Stat });
                    table.ForeignKey(
                        name: "FK_ConsumableBonuses_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumableBonuses");
        }
    }
}
