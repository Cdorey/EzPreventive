using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNutrition.Server.Migrations
{
    public partial class mssqlonprem_migration_124 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonalDietaryReferenceIntakes",
                columns: table => new
                {
                    PersonalDietaryReferenceIntakeValueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NutrientId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MeasureUnit = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalDietaryReferenceIntakes", x => x.PersonalDietaryReferenceIntakeValueId);
                    table.ForeignKey(
                        name: "FK_PersonalDietaryReferenceIntakes_Nutrients_NutrientId",
                        column: x => x.NutrientId,
                        principalTable: "Nutrients",
                        principalColumn: "NutrientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonalDietaryReferenceIntakes_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalDietaryReferenceIntakes_NutrientId",
                table: "PersonalDietaryReferenceIntakes",
                column: "NutrientId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalDietaryReferenceIntakes_PersonId",
                table: "PersonalDietaryReferenceIntakes",
                column: "PersonId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonalDietaryReferenceIntakes");
        }
    }
}
