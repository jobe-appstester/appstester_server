using Microsoft.EntityFrameworkCore.Migrations;

namespace AppsTester.Controller.Migrations
{
    public partial class RemoveUselessProperties : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerializedSubmissionCheckRequest",
                table: "SubmissionChecks");

            migrationBuilder.DropColumn(
                name: "SerializedSubmissionCheckResult",
                table: "SubmissionChecks");

            migrationBuilder.RenameColumn(
                name: "MoodleId",
                table: "SubmissionChecks",
                newName: "AttemptId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AttemptId",
                table: "SubmissionChecks",
                newName: "MoodleId");

            migrationBuilder.AddColumn<string>(
                name: "SerializedSubmissionCheckRequest",
                table: "SubmissionChecks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerializedSubmissionCheckResult",
                table: "SubmissionChecks",
                type: "text",
                nullable: true);
        }
    }
}
