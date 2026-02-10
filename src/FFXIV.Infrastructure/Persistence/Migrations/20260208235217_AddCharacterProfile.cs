using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FFXIV.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CharacterProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CharacterName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ServerName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DataCenter = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LodestoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ActiveJob = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ActiveJobLevel = table.Column<int>(type: "integer", nullable: false),
                    JobLevels = table.Column<string>(type: "jsonb", nullable: false),
                    Craftsmanship = table.Column<int>(type: "integer", nullable: false),
                    Control = table.Column<int>(type: "integer", nullable: false),
                    CP = table.Column<int>(type: "integer", nullable: false),
                    Gathering = table.Column<int>(type: "integer", nullable: false),
                    Perception = table.Column<int>(type: "integer", nullable: false),
                    GP = table.Column<int>(type: "integer", nullable: false),
                    CurrentGear = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastLodestoneSync = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterProfiles_LodestoneId",
                table: "CharacterProfiles",
                column: "LodestoneId",
                unique: true,
                filter: "\"LodestoneId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterProfiles_UserId",
                table: "CharacterProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterProfiles");
        }
    }
}
