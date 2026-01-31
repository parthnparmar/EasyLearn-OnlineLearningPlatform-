using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyLearn.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMCQMarkingScheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update existing Part A (MCQ) questions to have 2 points instead of 1
            migrationBuilder.Sql("UPDATE ExamQuestions SET Points = 2 WHERE Part = 1 AND Points = 1");
            
            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 9, 6, 52, 54, 150, DateTimeKind.Utc).AddTicks(3161));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 9, 6, 52, 54, 150, DateTimeKind.Utc).AddTicks(3785));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 9, 6, 52, 54, 150, DateTimeKind.Utc).AddTicks(3786));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 9, 6, 52, 54, 150, DateTimeKind.Utc).AddTicks(3787));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 9, 5, 7, 32, 315, DateTimeKind.Utc).AddTicks(3822));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 9, 5, 7, 32, 315, DateTimeKind.Utc).AddTicks(4342));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 9, 5, 7, 32, 315, DateTimeKind.Utc).AddTicks(4344));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 9, 5, 7, 32, 315, DateTimeKind.Utc).AddTicks(4345));
        }
    }
}
