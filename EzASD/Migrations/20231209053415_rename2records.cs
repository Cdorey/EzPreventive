using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzASD.Migrations
{
    public partial class rename2records : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PositiveSignRecord_Children_ChildId",
                table: "PositiveSignRecord");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PositiveSignRecord",
                table: "PositiveSignRecord");

            migrationBuilder.RenameTable(
                name: "PositiveSignRecord",
                newName: "PositiveSignRecords");

            migrationBuilder.RenameColumn(
                name: "Question",
                table: "PositiveSignRecords",
                newName: "PositiveQuestion");

            migrationBuilder.RenameColumn(
                name: "Age",
                table: "PositiveSignRecords",
                newName: "Date");

            migrationBuilder.RenameIndex(
                name: "IX_PositiveSignRecord_ChildId",
                table: "PositiveSignRecords",
                newName: "IX_PositiveSignRecords_ChildId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PositiveSignRecords",
                table: "PositiveSignRecords",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Chat23aRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChildId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Answer = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chat23aRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chat23aRecords_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "ChildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chat23aRecords_ChildId",
                table: "Chat23aRecords",
                column: "ChildId");

            migrationBuilder.AddForeignKey(
                name: "FK_PositiveSignRecords_Children_ChildId",
                table: "PositiveSignRecords",
                column: "ChildId",
                principalTable: "Children",
                principalColumn: "ChildId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PositiveSignRecords_Children_ChildId",
                table: "PositiveSignRecords");

            migrationBuilder.DropTable(
                name: "Chat23aRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PositiveSignRecords",
                table: "PositiveSignRecords");

            migrationBuilder.RenameTable(
                name: "PositiveSignRecords",
                newName: "PositiveSignRecord");

            migrationBuilder.RenameColumn(
                name: "PositiveQuestion",
                table: "PositiveSignRecord",
                newName: "Question");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "PositiveSignRecord",
                newName: "Age");

            migrationBuilder.RenameIndex(
                name: "IX_PositiveSignRecords_ChildId",
                table: "PositiveSignRecord",
                newName: "IX_PositiveSignRecord_ChildId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PositiveSignRecord",
                table: "PositiveSignRecord",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PositiveSignRecord_Children_ChildId",
                table: "PositiveSignRecord",
                column: "ChildId",
                principalTable: "Children",
                principalColumn: "ChildId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
