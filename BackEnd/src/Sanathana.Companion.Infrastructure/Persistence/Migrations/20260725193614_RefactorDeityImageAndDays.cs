using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorDeityImageAndDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add the new columns first.
            migrationBuilder.AddColumn<string>(
                name: "Days",
                table: "Deities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "Deities",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Deities",
                type: "bytea",
                nullable: true);

            // 2) Migrate existing DeityDays links into the new comma-separated Days column
            //    (ordered by the day's DisplayOrder) BEFORE dropping the junction table.
            migrationBuilder.Sql(@"
                UPDATE ""Deities"" d
                SET ""Days"" = sub.days
                FROM (
                    SELECT dd.""DeityId"" AS deity_id,
                           string_agg(day.""Name"", ',' ORDER BY day.""DisplayOrder"") AS days
                    FROM ""DeityDays"" dd
                    JOIN ""Days"" day ON day.""DayId"" = dd.""DayId""
                    GROUP BY dd.""DeityId""
                ) sub
                WHERE d.""Id"" = sub.deity_id;");

            // 3) Now drop the old image column and the junction table.
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Deities");

            migrationBuilder.DropTable(
                name: "DeityDays");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Days",
                table: "Deities");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "Deities");

            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Deities");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Deities",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeityDays",
                columns: table => new
                {
                    DeityId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeityDays", x => new { x.DeityId, x.DayId });
                    table.ForeignKey(
                        name: "FK_DeityDays_Days_DayId",
                        column: x => x.DayId,
                        principalTable: "Days",
                        principalColumn: "DayId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeityDays_Deities_DeityId",
                        column: x => x.DeityId,
                        principalTable: "Deities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeityDays_DayId",
                table: "DeityDays",
                column: "DayId");
        }
    }
}
