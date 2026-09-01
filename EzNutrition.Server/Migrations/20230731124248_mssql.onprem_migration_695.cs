using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNutrition.Server.Migrations
{
    public partial class mssqlonprem_migration_695 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "PersonalDietaryReferenceIntakes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOffsetValue",
                table: "PersonalDietaryReferenceIntakes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceType",
                table: "PersonalDietaryReferenceIntakes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Advices",
                columns: table => new
                {
                    AdviceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Advices", x => x.AdviceId);
                });

            migrationBuilder.CreateTable(
                name: "Diseases",
                columns: table => new
                {
                    DiseaseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ICD10 = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Diseases", x => x.DiseaseId);
                });

            migrationBuilder.CreateTable(
                name: "EERs",
                columns: table => new
                {
                    EERId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgeStart = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PAL = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AvgBwEER = table.Column<int>(type: "int", nullable: false),
                    BEE = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EERs", x => x.EERId);
                });

            migrationBuilder.CreateTable(
                name: "AdviceDisease",
                columns: table => new
                {
                    AdvicesAdviceId = table.Column<int>(type: "int", nullable: false),
                    DiseasesDiseaseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdviceDisease", x => new { x.AdvicesAdviceId, x.DiseasesDiseaseId });
                    table.ForeignKey(
                        name: "FK_AdviceDisease_Advices_AdvicesAdviceId",
                        column: x => x.AdvicesAdviceId,
                        principalTable: "Advices",
                        principalColumn: "AdviceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdviceDisease_Diseases_DiseasesDiseaseId",
                        column: x => x.DiseasesDiseaseId,
                        principalTable: "Diseases",
                        principalColumn: "DiseaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdviceDisease_DiseasesDiseaseId",
                table: "AdviceDisease",
                column: "DiseasesDiseaseId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdviceDisease");

            migrationBuilder.DropTable(
                name: "EERs");

            migrationBuilder.DropTable(
                name: "Advices");

            migrationBuilder.DropTable(
                name: "Diseases");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "PersonalDietaryReferenceIntakes");

            migrationBuilder.DropColumn(
                name: "IsOffsetValue",
                table: "PersonalDietaryReferenceIntakes");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "PersonalDietaryReferenceIntakes");
        }
    }
}
