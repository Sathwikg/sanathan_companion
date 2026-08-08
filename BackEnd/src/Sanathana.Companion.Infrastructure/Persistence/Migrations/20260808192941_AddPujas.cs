using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPujas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pujas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FestivalId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pujas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pujas_Festivals_FestivalId",
                        column: x => x.FestivalId,
                        principalTable: "Festivals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[] { new Guid("82828282-8282-8282-8282-828282828282"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Pujas mapped to festivals", 10, "🛐", true, true, null, null, "Puja", new Guid("33333333-3333-3333-3333-333333333333"), "/pujas", false });

            migrationBuilder.CreateIndex(
                name: "UX_Pujas_Festival_Name",
                table: "Pujas",
                columns: new[] { "FestivalId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pujas");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("82828282-8282-8282-8282-828282828282"));
        }
    }
}
