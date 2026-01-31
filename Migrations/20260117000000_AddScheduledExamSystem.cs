using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledExamSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old columns from Exams table
            migrationBuilder.DropColumn(
                name: "ExamDate",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ExamSession",
                table: "Exams");

            // Add new columns to Exams table
            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledStartTime",
                table: "Exams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledEndTime",
                table: "Exams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsAutoScheduled",
                table: "Exams",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPreVerification",
                table: "Exams",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Drop old columns from ExamVerifications table
            migrationBuilder.DropColumn(
                name: "Address",
                table: "ExamVerifications");

            migrationBuilder.DropColumn(
                name: "EmergencyContact",
                table: "ExamVerifications");

            migrationBuilder.DropColumn(
                name: "CaptchaAnswer",
                table: "ExamVerifications");

            // Add new column to ExamVerifications table
            migrationBuilder.AddColumn<string>(
                name: "EnrollmentNumber",
                table: "ExamVerifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Create MissedExamRequests table
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
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissedExamRequests_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "ScheduledStartTime",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ScheduledEndTime",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "IsAutoScheduled",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "RequiresPreVerification",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "EnrollmentNumber",
                table: "ExamVerifications");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExamDate",
                table: "Exams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ExamSession",
                table: "Exams",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "ExamVerifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContact",
                table: "ExamVerifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CaptchaAnswer",
                table: "ExamVerifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}