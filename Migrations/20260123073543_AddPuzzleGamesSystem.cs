using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddPuzzleGamesSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PuzzleGames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Difficulty = table.Column<int>(type: "int", nullable: false),
                    InitialState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Solution = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxScore = table.Column<int>(type: "int", nullable: false),
                    TimeLimit = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuzzleGames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PuzzleAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PuzzleGameId = table.Column<int>(type: "int", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    HintsUsed = table.Column<int>(type: "int", nullable: false),
                    MovesCount = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeTakenSeconds = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuzzleAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PuzzleAttempts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PuzzleAttempts_PuzzleGames_PuzzleGameId",
                        column: x => x.PuzzleGameId,
                        principalTable: "PuzzleGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PuzzleLeaderboards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PuzzleGameId = table.Column<int>(type: "int", nullable: false),
                    BestScore = table.Column<int>(type: "int", nullable: false),
                    BestTimeSeconds = table.Column<int>(type: "int", nullable: false),
                    TotalAttempts = table.Column<int>(type: "int", nullable: false),
                    CompletedAttempts = table.Column<int>(type: "int", nullable: false),
                    LastPlayedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuzzleLeaderboards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PuzzleLeaderboards_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PuzzleLeaderboards_PuzzleGames_PuzzleGameId",
                        column: x => x.PuzzleGameId,
                        principalTable: "PuzzleGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PuzzleMoves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PuzzleAttemptId = table.Column<int>(type: "int", nullable: false),
                    MoveData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MadeAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuzzleMoves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PuzzleMoves_PuzzleAttempts_PuzzleAttemptId",
                        column: x => x.PuzzleAttemptId,
                        principalTable: "PuzzleAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 23, 7, 35, 42, 368, DateTimeKind.Utc).AddTicks(5));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 23, 7, 35, 42, 368, DateTimeKind.Utc).AddTicks(600));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 23, 7, 35, 42, 368, DateTimeKind.Utc).AddTicks(602));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 23, 7, 35, 42, 368, DateTimeKind.Utc).AddTicks(603));

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleAttempts_PuzzleGameId",
                table: "PuzzleAttempts",
                column: "PuzzleGameId");

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleAttempts_UserId",
                table: "PuzzleAttempts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleLeaderboards_PuzzleGameId",
                table: "PuzzleLeaderboards",
                column: "PuzzleGameId");

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleLeaderboards_UserId",
                table: "PuzzleLeaderboards",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleMoves_PuzzleAttemptId",
                table: "PuzzleMoves",
                column: "PuzzleAttemptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PuzzleLeaderboards");

            migrationBuilder.DropTable(
                name: "PuzzleMoves");

            migrationBuilder.DropTable(
                name: "PuzzleAttempts");

            migrationBuilder.DropTable(
                name: "PuzzleGames");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 23, 7, 22, 56, 51, DateTimeKind.Utc).AddTicks(3634));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 23, 7, 22, 56, 51, DateTimeKind.Utc).AddTicks(4554));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 23, 7, 22, 56, 51, DateTimeKind.Utc).AddTicks(4558));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 23, 7, 22, 56, 51, DateTimeKind.Utc).AddTicks(4559));
        }
    }
}
