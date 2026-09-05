using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNutrition.Server.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddLlmAuditCleanupIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionGenerateRequests_RequestTime",
                table: "PrescriptionGenerateRequests",
                column: "RequestTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PrescriptionGenerateRequests_RequestTime",
                table: "PrescriptionGenerateRequests");
        }
    }
}
