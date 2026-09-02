using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNutrition.Server.Migrations
{
    public partial class mssqlonprem_migration_460 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_MultiDerivedPersonRelationships_ChildModelId",
                table: "MultiDerivedPersonRelationships",
                column: "ChildModelId");

            migrationBuilder.CreateIndex(
                name: "IX_MultiDerivedPersonRelationships_ParentModelId",
                table: "MultiDerivedPersonRelationships",
                column: "ParentModelId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MultiDerivedPersonRelationships");
        }
    }
}
