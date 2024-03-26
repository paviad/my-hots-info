using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyReplayLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BuildNumbers",
                columns: table => new
                {
                    Buildnumber1 = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Builddate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildNumbers", x => x.Buildnumber1);
                });

            migrationBuilder.CreateTable(
                name: "HeroTalentInformations",
                columns: table => new
                {
                    Character = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ReplayBuildFirst = table.Column<int>(type: "INTEGER", nullable: false),
                    TalentId = table.Column<int>(type: "INTEGER", maxLength: 450, nullable: false),
                    ReplayBuildLast = table.Column<int>(type: "INTEGER", nullable: false),
                    TalentTier = table.Column<int>(type: "INTEGER", nullable: false),
                    TalentName = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    TalentDescription = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeroTalentInformations", x => new { x.Character, x.ReplayBuildFirst, x.TalentId });
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BattleNetRegionId = table.Column<int>(type: "INTEGER", nullable: false),
                    BattleNetSubId = table.Column<int>(type: "INTEGER", nullable: false),
                    BattleNetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    BattleTag = table.Column<int>(type: "INTEGER", nullable: false),
                    TimestampCreated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Replays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReplayHash = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReplayBuild = table.Column<int>(type: "INTEGER", nullable: false),
                    GameMode = table.Column<string>(type: "TEXT", nullable: false),
                    MapId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ReplayLength = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    TimestampReplay = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TimestampCreated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Replays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReplayCharacters",
                columns: table => new
                {
                    ReplayId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsAutoSelect = table.Column<bool>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CharacterLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    IsWinner = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMe = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayCharacters", x => new { x.ReplayId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_ReplayCharacters_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReplayCharacters_Replays_ReplayId",
                        column: x => x.ReplayId,
                        principalTable: "Replays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReplayTeamObjectives",
                columns: table => new
                {
                    ReplayId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsWinner = table.Column<bool>(type: "INTEGER", nullable: false),
                    TeamObjectiveType = table.Column<string>(type: "TEXT", nullable: false),
                    TimeSpan = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: true),
                    Value = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayTeamObjectives", x => new { x.ReplayId, x.IsWinner, x.TeamObjectiveType, x.TimeSpan });
                    table.ForeignKey(
                        name: "FK_ReplayTeamObjectives_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReplayTeamObjectives_Replays_ReplayId",
                        column: x => x.ReplayId,
                        principalTable: "Replays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReplayCharacterDraftOrders",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReplayId = table.Column<int>(type: "INTEGER", nullable: false),
                    DraftOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayCharacterDraftOrders", x => new { x.ReplayId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_ReplayCharacterDraftOrders_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReplayCharacterDraftOrders_ReplayCharacters_ReplayId_PlayerId",
                        columns: x => new { x.ReplayId, x.PlayerId },
                        principalTable: "ReplayCharacters",
                        principalColumns: new[] { "ReplayId", "PlayerId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReplayCharacterDraftOrders_Replays_ReplayId",
                        column: x => x.ReplayId,
                        principalTable: "Replays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReplayCharacterMatchAwards",
                columns: table => new
                {
                    ReplayId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchAwardType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayCharacterMatchAwards", x => new { x.ReplayId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_ReplayCharacterMatchAwards_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReplayCharacterMatchAwards_ReplayCharacters_ReplayId_PlayerId",
                        columns: x => new { x.ReplayId, x.PlayerId },
                        principalTable: "ReplayCharacters",
                        principalColumns: new[] { "ReplayId", "PlayerId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReplayCharacterMatchAwards_Replays_ReplayId",
                        column: x => x.ReplayId,
                        principalTable: "Replays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReplayCharacterScoreResults",
                columns: table => new
                {
                    ReplayId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Takedowns = table.Column<int>(type: "INTEGER", nullable: false),
                    SoloKills = table.Column<int>(type: "INTEGER", nullable: false),
                    Assists = table.Column<int>(type: "INTEGER", nullable: false),
                    Deaths = table.Column<int>(type: "INTEGER", nullable: false),
                    HighestKillStreak = table.Column<int>(type: "INTEGER", nullable: false),
                    HeroDamage = table.Column<int>(type: "INTEGER", nullable: false),
                    SiegeDamage = table.Column<int>(type: "INTEGER", nullable: false),
                    StructureDamage = table.Column<int>(type: "INTEGER", nullable: false),
                    MinionDamage = table.Column<int>(type: "INTEGER", nullable: false),
                    CreepDamage = table.Column<int>(type: "INTEGER", nullable: false),
                    SummonDamage = table.Column<int>(type: "INTEGER", nullable: false),
                    Healing = table.Column<int>(type: "INTEGER", nullable: true),
                    SelfHealing = table.Column<int>(type: "INTEGER", nullable: false),
                    DamageTaken = table.Column<int>(type: "INTEGER", nullable: true),
                    ExperienceContribution = table.Column<int>(type: "INTEGER", nullable: false),
                    TownKills = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeSpentDead = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    MercCampCaptures = table.Column<int>(type: "INTEGER", nullable: false),
                    WatchTowerCaptures = table.Column<int>(type: "INTEGER", nullable: false),
                    MetaExperience = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayCharacterScoreResults", x => new { x.ReplayId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_ReplayCharacterScoreResults_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReplayCharacterScoreResults_ReplayCharacters_ReplayId_PlayerId",
                        columns: x => new { x.ReplayId, x.PlayerId },
                        principalTable: "ReplayCharacters",
                        principalColumns: new[] { "ReplayId", "PlayerId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReplayCharacterScoreResults_Replays_ReplayId",
                        column: x => x.ReplayId,
                        principalTable: "Replays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReplayCharacterTalents",
                columns: table => new
                {
                    ReplayId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    TalentId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayCharacterTalents", x => new { x.ReplayId, x.PlayerId, x.TalentId });
                    table.ForeignKey(
                        name: "FK_ReplayCharacterTalents_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReplayCharacterTalents_ReplayCharacters_ReplayId_PlayerId",
                        columns: x => new { x.ReplayId, x.PlayerId },
                        principalTable: "ReplayCharacters",
                        principalColumns: new[] { "ReplayId", "PlayerId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReplayCharacterTalents_Replays_ReplayId",
                        column: x => x.ReplayId,
                        principalTable: "Replays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReplayCharacterDraftOrders_PlayerId",
                table: "ReplayCharacterDraftOrders",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayCharacterMatchAwards_PlayerId",
                table: "ReplayCharacterMatchAwards",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayCharacters_PlayerId",
                table: "ReplayCharacters",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayCharacterScoreResults_PlayerId",
                table: "ReplayCharacterScoreResults",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayCharacterTalents_PlayerId",
                table: "ReplayCharacterTalents",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayTeamObjectives_PlayerId",
                table: "ReplayTeamObjectives",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildNumbers");

            migrationBuilder.DropTable(
                name: "HeroTalentInformations");

            migrationBuilder.DropTable(
                name: "ReplayCharacterDraftOrders");

            migrationBuilder.DropTable(
                name: "ReplayCharacterMatchAwards");

            migrationBuilder.DropTable(
                name: "ReplayCharacterScoreResults");

            migrationBuilder.DropTable(
                name: "ReplayCharacterTalents");

            migrationBuilder.DropTable(
                name: "ReplayTeamObjectives");

            migrationBuilder.DropTable(
                name: "ReplayCharacters");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Replays");
        }
    }
}
