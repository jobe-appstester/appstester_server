using Microsoft.EntityFrameworkCore.Migrations;

namespace AppsTester.Controller.Migrations
{
    public partial class AddStepId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttemptStepId",
                table: "SubmissionChecks",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptStepId",
                table: "SubmissionChecks");
        }
    }
}
