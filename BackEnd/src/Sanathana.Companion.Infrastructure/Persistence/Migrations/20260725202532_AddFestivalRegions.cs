using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFestivalRegions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Regions",
                table: "Festivals",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000001"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000002"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000003"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000004"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000005"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000006"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000007"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000008"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000009"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000010"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000011"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000012"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000013"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000014"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000015"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000016"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000017"),
                column: "Regions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Festivals",
                keyColumn: "Id",
                keyValue: new Guid("fe510000-0000-0000-0000-000000000018"),
                column: "Regions",
                value: null);

            // Update existing data: assign all currently-active regions to festivals that have none.
            // (No-op on a fresh database where no regions exist yet.)
            migrationBuilder.Sql(@"
                UPDATE ""Festivals""
                SET ""Regions"" = (SELECT string_agg(""Id""::text, ',') FROM ""Regions"" WHERE ""IsActive"" = true)
                WHERE ""Regions"" IS NULL
                  AND EXISTS (SELECT 1 FROM ""Regions"" WHERE ""IsActive"" = true);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Regions",
                table: "Festivals");
        }
    }
}
