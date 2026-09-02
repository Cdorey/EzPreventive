using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNutrition.Server.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class ReplaceNoticeCoverLetterFlagWithKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsCoverLetter",
                table: "Notices",
                newName: "Kind");

            migrationBuilder.AlterColumn<int>(
                name: "Kind",
                table: "Notices",
                type: "int",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 原 bit 字段无法表达新增类别；回滚时按约定丢弃这些通知。
            migrationBuilder.Sql(
                """
                DELETE FROM [Notices]
                WHERE [Kind] NOT IN (0, 1);
                """);

            migrationBuilder.AlterColumn<bool>(
                name: "Kind",
                table: "Notices",
                type: "bit",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.RenameColumn(
                name: "Kind",
                table: "Notices",
                newName: "IsCoverLetter");
        }
    }
}
