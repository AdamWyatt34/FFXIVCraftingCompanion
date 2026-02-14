using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FFXIV.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedRotationsAndVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharedRotations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ActionIds = table.Column<string>(type: "jsonb", nullable: false),
                    Craftsmanship = table.Column<int>(type: "integer", nullable: false),
                    Control = table.Column<int>(type: "integer", nullable: false),
                    CP = table.Column<int>(type: "integer", nullable: false),
                    CrafterLevel = table.Column<int>(type: "integer", nullable: false),
                    QualityPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    HQPercent = table.Column<int>(type: "integer", nullable: false),
                    TotalSteps = table.Column<int>(type: "integer", nullable: false),
                    CraftCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Algorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Upvotes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedRotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedRotations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedRotations_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RotationVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RotationId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RotationVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RotationVotes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RotationVotes_SharedRotations_RotationId",
                        column: x => x.RotationId,
                        principalTable: "SharedRotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RotationVotes_RotationId",
                table: "RotationVotes",
                column: "RotationId");

            migrationBuilder.CreateIndex(
                name: "IX_RotationVotes_UserId_RotationId",
                table: "RotationVotes",
                columns: new[] { "UserId", "RotationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedRotations_CreatedAt",
                table: "SharedRotations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SharedRotations_RecipeId",
                table: "SharedRotations",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedRotations_RecipeId_QualityPercent",
                table: "SharedRotations",
                columns: new[] { "RecipeId", "QualityPercent" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedRotations_Upvotes",
                table: "SharedRotations",
                column: "Upvotes");

            migrationBuilder.CreateIndex(
                name: "IX_SharedRotations_UserId",
                table: "SharedRotations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RotationVotes");

            migrationBuilder.DropTable(
                name: "SharedRotations");
        }
    }
}
