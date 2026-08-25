using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNutrition.Server.Migrations
{
    /// <inheritdoc />
    public partial class mssqlonprem_migration_128 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EdiblePortion",
                table: "Foods",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FoodGroups",
                table: "Foods",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EdiblePortion",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "FoodGroups",
                table: "Foods");
        }
    }
}
