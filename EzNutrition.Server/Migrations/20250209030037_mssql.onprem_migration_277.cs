using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNutrition.Server.Migrations
{
    /// <inheritdoc />
    public partial class mssqlonprem_migration_277 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FoodNutrientValues_Nutrient_NutrientId",
                table: "FoodNutrientValues");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Nutrient",
                table: "Nutrient");

            migrationBuilder.RenameTable(
                name: "Nutrient",
                newName: "Nutrients");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Nutrients",
                table: "Nutrients",
                column: "NutrientId");

            migrationBuilder.AddForeignKey(
                name: "FK_FoodNutrientValues_Nutrients_NutrientId",
                table: "FoodNutrientValues",
                column: "NutrientId",
                principalTable: "Nutrients",
                principalColumn: "NutrientId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FoodNutrientValues_Nutrients_NutrientId",
                table: "FoodNutrientValues");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Nutrients",
                table: "Nutrients");

            migrationBuilder.RenameTable(
                name: "Nutrients",
                newName: "Nutrient");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Nutrient",
                table: "Nutrient",
                column: "NutrientId");

            migrationBuilder.AddForeignKey(
                name: "FK_FoodNutrientValues_Nutrient_NutrientId",
                table: "FoodNutrientValues",
                column: "NutrientId",
                principalTable: "Nutrient",
                principalColumn: "NutrientId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
