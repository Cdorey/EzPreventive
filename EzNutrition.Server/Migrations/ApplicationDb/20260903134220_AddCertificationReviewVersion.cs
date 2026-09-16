using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNutrition.Server.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddCertificationReviewVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "ProfessionalCertificationRequests",
                type: "uniqueidentifier",
                nullable: true);

            // 为每条历史申请生成有效版本，不改变申请状态、提交时间或角色。
            migrationBuilder.Sql("UPDATE [ProfessionalCertificationRequests] SET [Version] = NEWID();");

            migrationBuilder.AlterColumn<Guid>(
                name: "Version",
                table: "ProfessionalCertificationRequests",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCertificationRequests_Status_RequestTime",
                table: "ProfessionalCertificationRequests",
                columns: new[] { "Status", "RequestTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProfessionalCertificationRequests_Status_RequestTime",
                table: "ProfessionalCertificationRequests");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ProfessionalCertificationRequests");
        }
    }
}
