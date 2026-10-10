using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobHunt.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobApplicationInterviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobApplicationInterviews",
                columns: table => new
                {
                    JobApplicationInterviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobApplicationId = table.Column<int>(type: "int", nullable: false),
                    RoundName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MeetingUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PreparationNotes = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    Feedback = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplicationInterviews", x => x.JobApplicationInterviewId);
                    table.ForeignKey(
                        name: "FK_JobApplicationInterviews_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "JobApplicationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplicationInterviews_IsCancelled_ScheduledAtUtc",
                table: "JobApplicationInterviews",
                columns: new[] { "IsCancelled", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplicationInterviews_JobApplicationId",
                table: "JobApplicationInterviews",
                column: "JobApplicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobApplicationInterviews");
        }
    }
}
