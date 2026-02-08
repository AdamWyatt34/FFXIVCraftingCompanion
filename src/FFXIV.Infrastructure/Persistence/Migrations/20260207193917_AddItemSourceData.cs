using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FFXIV.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemSourceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GatheringInfos",
                columns: table => new
                {
                    GatheringInfoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    GatheringType = table.Column<string>(type: "text", nullable: false),
                    ClassJob = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    ZoneName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AreaName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CoordinateX = table.Column<float>(type: "real", nullable: true),
                    CoordinateY = table.Column<float>(type: "real", nullable: true),
                    IsTimed = table.Column<bool>(type: "boolean", nullable: false),
                    SpawnTimes = table.Column<string>(type: "text", nullable: true),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatheringInfos", x => x.GatheringInfoId);
                    table.ForeignKey(
                        name: "FK_GatheringInfos_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemSources",
                columns: table => new
                {
                    ItemSourceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    SourceDetails = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSources", x => x.ItemSourceId);
                    table.ForeignKey(
                        name: "FK_ItemSources_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorInfos",
                columns: table => new
                {
                    VendorInfoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    NpcName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ZoneName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CoordinateX = table.Column<float>(type: "real", nullable: true),
                    CoordinateY = table.Column<float>(type: "real", nullable: true),
                    Cost = table.Column<int>(type: "integer", nullable: false),
                    CurrencyType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CurrencyItemId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorInfos", x => x.VendorInfoId);
                    table.ForeignKey(
                        name: "FK_VendorInfos_Items_CurrencyItemId",
                        column: x => x.CurrencyItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorInfos_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GatheringInfos_ItemId",
                table: "GatheringInfos",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSources_ItemId",
                table: "ItemSources",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSources_ItemId_SourceType",
                table: "ItemSources",
                columns: new[] { "ItemId", "SourceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorInfos_CurrencyItemId",
                table: "VendorInfos",
                column: "CurrencyItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInfos_ItemId",
                table: "VendorInfos",
                column: "ItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GatheringInfos");

            migrationBuilder.DropTable(
                name: "ItemSources");

            migrationBuilder.DropTable(
                name: "VendorInfos");
        }
    }
}
