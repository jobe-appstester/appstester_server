using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AppsTester.Controller.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubmissionChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MoodleId = table.Column<int>(type: "integer", nullable: false),
                    SerializedSubmissionCheckRequest = table.Column<string>(type: "text", nullable: true),
                    SerializedSubmissionCheckResult = table.Column<string>(type: "text", nullable: true),
                    SubmissionCheckStatus = table.Column<string>(type: "text", nullable: true),
                    SendingDateTimeUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionChecks", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionChecks");
        }
    }
}
