using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNutrition.Server.Migrations
{
    public partial class mssqlonprem_migration_362 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OffsetEnergy",
                table: "EERs",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialPhysiologicalPeriod",
                table: "EERs",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OffsetEnergy",
                table: "EERs");

            migrationBuilder.DropColumn(
                name: "SpecialPhysiologicalPeriod",
                table: "EERs");
        }
    }
}
