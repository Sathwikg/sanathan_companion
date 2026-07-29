using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDefaultRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultRegionId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "DefaultRegionId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DefaultRegionId",
                table: "Users",
                column: "DefaultRegionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Regions_DefaultRegionId",
                table: "Users",
                column: "DefaultRegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Regions_DefaultRegionId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_DefaultRegionId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DefaultRegionId",
                table: "Users");
        }
    }
}
