using Microsoft.EntityFrameworkCore.Migrations;

namespace AppsTester.Controller.Migrations
{
    public partial class AddStepId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AttemptId",
                table: "SubmissionChecks",
                newName: "AttemptStepId");
            
            migrationBuilder.AddColumn<string>(
                name: "AttemptId",
                table: "SubmissionChecks",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptId",
                table: "SubmissionChecks");
            
            migrationBuilder.RenameColumn(
                name: "AttemptStepId",
                table: "SubmissionChecks",
                newName: "AttemptId");
        }
    }
}
