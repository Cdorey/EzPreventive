using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzASD.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Children",
                columns: table => new
                {
                    ChildId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChildName = table.Column<string>(type: "TEXT", nullable: true),
                    Gender = table.Column<string>(type: "TEXT", nullable: true),
                    BirthDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsOnlyChild = table.Column<bool>(type: "INTEGER", nullable: false),
                    ContactNumber = table.Column<string>(type: "TEXT", nullable: true),
                    IsFatherInFamily = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMotherInFamily = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsGrandpaInFamily = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsGrandmaInFamily = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsNurseInFamily = table.Column<bool>(type: "INTEGER", nullable: false),
                    OthersInFamily = table.Column<string>(type: "TEXT", nullable: true),
                    FatherEduLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    MotherEduLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    FatherProfession = table.Column<int>(type: "INTEGER", nullable: false),
                    MotherProfession = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentsRelationship = table.Column<int>(type: "INTEGER", nullable: false),
                    FatherCharacter = table.Column<int>(type: "INTEGER", nullable: false),
                    MotherCharacter = table.Column<int>(type: "INTEGER", nullable: false),
                    PrimaryEducator = table.Column<int>(type: "INTEGER", nullable: false),
                    MainEducationMethods = table.Column<int>(type: "INTEGER", nullable: false),
                    Respondent = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeTogether = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Children", x => x.ChildId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Children");
        }
    }
}
