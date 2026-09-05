using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNutrition.Server.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddAccountCleanupLookupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 先检查历史值，避免为索引收窄列类型时截断不符合 Identity 主键长度的用户标识。
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [ProfessionalCertificationRequests] WHERE DATALENGTH([UserId]) > 900)
                   OR EXISTS (SELECT 1 FROM [PrescriptionGenerateRequests] WHERE DATALENGTH([UserId]) > 900)
                BEGIN
                    THROW 51000, N'认证申请或审计记录存在超过 450 个 UTF-16 代码单元的 UserId，请先核实历史数据。', 1;
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ProfessionalCertificationRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PrescriptionGenerateRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCertificationRequests_UserId",
                table: "ProfessionalCertificationRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionGenerateRequests_UserId",
                table: "PrescriptionGenerateRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProfessionalCertificationRequests_UserId",
                table: "ProfessionalCertificationRequests");

            migrationBuilder.DropIndex(
                name: "IX_PrescriptionGenerateRequests_UserId",
                table: "PrescriptionGenerateRequests");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ProfessionalCertificationRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PrescriptionGenerateRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);
        }
    }
}
