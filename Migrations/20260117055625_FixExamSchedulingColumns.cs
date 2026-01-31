using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyLearn.Migrations
{
    /// <inheritdoc />
    public partial class FixExamSchedulingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "ExamVerifications");

            migrationBuilder.DropColumn(
                name: "ExamSession",
                table: "Exams");

            migrationBuilder.RenameColumn(
                name: "EmergencyContact",
                table: "ExamVerifications",
                newName: "EnrollmentNumber");

            migrationBuilder.RenameColumn(
                name: "ExamDate",
                table: "Exams",
                newName: "ScheduledStartTime");

            migrationBuilder.AddColumn<bool>(
                name: "IsAutoScheduled",
                table: "Exams",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPreVerification",
                table: "Exams",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledEndTime",
                table: "Exams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "MissedExamRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExamId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    IsRejected = table.Column<bool>(type: "bit", nullable: false),
                    InstructorResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InstructorId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissedExamRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissedExamRequests_AspNetUsers_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MissedExamRequests_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MissedExamRequests_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 5, 56, 24, 401, DateTimeKind.Utc).AddTicks(2680));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 5, 56, 24, 401, DateTimeKind.Utc).AddTicks(3391));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 5, 56, 24, 401, DateTimeKind.Utc).AddTicks(3393));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 5, 56, 24, 401, DateTimeKind.Utc).AddTicks(3395));

            migrationBuilder.CreateIndex(
                name: "IX_MissedExamRequests_ExamId",
                table: "MissedExamRequests",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_MissedExamRequests_InstructorId",
                table: "MissedExamRequests",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_MissedExamRequests_StudentId",
                table: "MissedExamRequests",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MissedExamRequests");

            migrationBuilder.DropColumn(
                name: "IsAutoScheduled",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "RequiresPreVerification",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ScheduledEndTime",
                table: "Exams");

            migrationBuilder.RenameColumn(
                name: "EnrollmentNumber",
                table: "ExamVerifications",
                newName: "EmergencyContact");

            migrationBuilder.RenameColumn(
                name: "ScheduledStartTime",
                table: "Exams",
                newName: "ExamDate");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "ExamVerifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExamSession",
                table: "Exams",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 16, 4, 21, 48, 861, DateTimeKind.Utc).AddTicks(4323));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 16, 4, 21, 48, 861, DateTimeKind.Utc).AddTicks(4895));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 16, 4, 21, 48, 861, DateTimeKind.Utc).AddTicks(4897));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 16, 4, 21, 48, 861, DateTimeKind.Utc).AddTicks(4898));
        }
    }
}
