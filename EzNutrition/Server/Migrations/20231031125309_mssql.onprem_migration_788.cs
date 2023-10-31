using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNutrition.Server.Migrations
{
    public partial class mssqlonprem_migration_788 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FoodNutrientValues_Nutrients_NutrientId",
                table: "FoodNutrientValues");

            migrationBuilder.DropTable(
                name: "MultiDerivedPersonRelationships");

            migrationBuilder.DropTable(
                name: "PersonalDietaryReferenceIntakes");

            migrationBuilder.DropTable(
                name: "Nutrients");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.CreateTable(
                name: "DRIs",
                columns: table => new
                {
                    DietaryReferenceIntakeValueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgeStart = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpecialPhysiologicalPeriod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nutrient = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    RecordType = table.Column<int>(type: "int", nullable: false),
                    IsOffset = table.Column<bool>(type: "bit", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MeasureUnit = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DRIs", x => x.DietaryReferenceIntakeValueId);
                });

            migrationBuilder.CreateTable(
                name: "Nutrient",
                columns: table => new
                {
                    NutrientId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DefaultMeasureUnit = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FriendlyName = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nutrient", x => x.NutrientId);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_FoodNutrientValues_Nutrient_NutrientId",
                table: "FoodNutrientValues",
                column: "NutrientId",
                principalTable: "Nutrient",
                principalColumn: "NutrientId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FoodNutrientValues_Nutrient_NutrientId",
                table: "FoodNutrientValues");

            migrationBuilder.DropTable(
                name: "DRIs");

            migrationBuilder.DropTable(
                name: "Nutrient");

            migrationBuilder.CreateTable(
                name: "Nutrients",
                columns: table => new
                {
                    NutrientId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DefaultMeasureUnit = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FriendlyName = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nutrients", x => x.NutrientId);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DerivedFromPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgeEnd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AgeStart = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BodySize = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cite = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FriendlyName = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Illness = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhysicalActivityLevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpecialPhysiologicalPeriod = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.PersonId);
                    table.ForeignKey(
                        name: "FK_People_People_DerivedFromPersonId",
                        column: x => x.DerivedFromPersonId,
                        principalTable: "People",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "MultiDerivedPersonRelationships",
                columns: table => new
                {
                    MultiDerivedPersonRelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MultiDerivedPersonRelationships", x => x.MultiDerivedPersonRelationshipId);
                    table.ForeignKey(
                        name: "FK_MultiDerivedPersonRelationships_People_ChildModelId",
                        column: x => x.ChildModelId,
                        principalTable: "People",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK_MultiDerivedPersonRelationships_People_ParentModelId",
                        column: x => x.ParentModelId,
                        principalTable: "People",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "PersonalDietaryReferenceIntakes",
                columns: table => new
                {
                    PersonalDietaryReferenceIntakeValueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NutrientId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsOffsetValue = table.Column<bool>(type: "bit", nullable: false),
                    MeasureUnit = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    ReferenceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
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
                name: "IX_MultiDerivedPersonRelationships_ChildModelId",
                table: "MultiDerivedPersonRelationships",
                column: "ChildModelId");

            migrationBuilder.CreateIndex(
                name: "IX_MultiDerivedPersonRelationships_ParentModelId",
                table: "MultiDerivedPersonRelationships",
                column: "ParentModelId");

            migrationBuilder.CreateIndex(
                name: "IX_People_DerivedFromPersonId",
                table: "People",
                column: "DerivedFromPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalDietaryReferenceIntakes_NutrientId",
                table: "PersonalDietaryReferenceIntakes",
                column: "NutrientId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalDietaryReferenceIntakes_PersonId",
                table: "PersonalDietaryReferenceIntakes",
                column: "PersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_FoodNutrientValues_Nutrients_NutrientId",
                table: "FoodNutrientValues",
                column: "NutrientId",
                principalTable: "Nutrients",
                principalColumn: "NutrientId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
