using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PujaOptionalFestivalAndDeity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Pujas_Festival_Name",
                table: "Pujas");

            migrationBuilder.AlterColumn<Guid>(
                name: "FestivalId",
                table: "Pujas",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "DeityId",
                table: "Pujas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pujas_Deity",
                table: "Pujas",
                column: "DeityId");

            migrationBuilder.CreateIndex(
                name: "UX_Pujas_Festival_Name",
                table: "Pujas",
                columns: new[] { "FestivalId", "Name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddForeignKey(
                name: "FK_Pujas_Deities_DeityId",
                table: "Pujas",
                column: "DeityId",
                principalTable: "Deities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pujas_Deities_DeityId",
                table: "Pujas");

            migrationBuilder.DropIndex(
                name: "IX_Pujas_Deity",
                table: "Pujas");

            migrationBuilder.DropIndex(
                name: "UX_Pujas_Festival_Name",
                table: "Pujas");

            migrationBuilder.DropColumn(
                name: "DeityId",
                table: "Pujas");

            migrationBuilder.AlterColumn<Guid>(
                name: "FestivalId",
                table: "Pujas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Pujas_Festival_Name",
                table: "Pujas",
                columns: new[] { "FestivalId", "Name" },
                unique: true);
        }
    }
}
