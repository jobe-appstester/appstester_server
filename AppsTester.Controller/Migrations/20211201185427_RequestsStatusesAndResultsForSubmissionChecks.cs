using Microsoft.EntityFrameworkCore.Migrations;

namespace AppsTester.Controller.Migrations
{
    public partial class RequestsStatusesAndResultsForSubmissionChecks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SubmissionCheckStatus",
                table: "SubmissionChecks",
                newName: "SerializedResult");

            migrationBuilder.AddColumn<string>(
                name: "LastSerializedStatus",
                table: "SubmissionChecks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastStatusVersion",
                table: "SubmissionChecks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SerializedRequest",
                table: "SubmissionChecks",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSerializedStatus",
                table: "SubmissionChecks");

            migrationBuilder.DropColumn(
                name: "LastStatusVersion",
                table: "SubmissionChecks");

            migrationBuilder.DropColumn(
                name: "SerializedRequest",
                table: "SubmissionChecks");

            migrationBuilder.RenameColumn(
                name: "SerializedResult",
                table: "SubmissionChecks",
                newName: "SubmissionCheckStatus");
        }
    }
}
