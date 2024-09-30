using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyReplayLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class Takedowns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Takedowns",
                columns: table => new
                {
                    ReplayId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeqId = table.Column<int>(type: "INTEGER", nullable: false),
                    KillerId = table.Column<int>(type: "INTEGER", nullable: false),
                    VictimId = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeSpan = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    KillingBlow = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Takedowns", x => new { x.ReplayId, x.SeqId, x.VictimId, x.KillerId });
                    table.ForeignKey(
                        name: "FK_Takedowns_ReplayCharacters_ReplayId_KillerId",
                        columns: x => new { x.ReplayId, x.KillerId },
                        principalTable: "ReplayCharacters",
                        principalColumns: new[] { "ReplayId", "PlayerId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Takedowns_ReplayCharacters_ReplayId_VictimId",
                        columns: x => new { x.ReplayId, x.VictimId },
                        principalTable: "ReplayCharacters",
                        principalColumns: new[] { "ReplayId", "PlayerId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Takedowns_Replays_ReplayId",
                        column: x => x.ReplayId,
                        principalTable: "Replays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Takedowns_ReplayId_KillerId",
                table: "Takedowns",
                columns: new[] { "ReplayId", "KillerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Takedowns_ReplayId_VictimId",
                table: "Takedowns",
                columns: new[] { "ReplayId", "VictimId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Takedowns");
        }
    }
}
