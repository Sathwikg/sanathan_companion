using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWallpapers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Wallpapers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ImageData = table.Column<byte[]>(type: "bytea", nullable: false),
                    ImageContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallpapers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Wallpapers_Deities_DeityId",
                        column: x => x.DeityId,
                        principalTable: "Deities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[,]
                {
                    { new Guid("80808080-8080-8080-8080-808080808080"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Upload and manage downloadable wallpapers for each deity", 9, "🖼️", true, true, null, null, "Wallpaper Master", new Guid("33333333-3333-3333-3333-333333333333"), "/wallpapers", false },
                    { new Guid("81818181-8181-8181-8181-818181818181"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Browse and download deity wallpapers", 2, "📱", true, true, null, null, "Download Wallpapers", new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), "/wallpapers-download", true }
                });

            migrationBuilder.InsertData(
                table: "ModuleRoleMappings",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "MenuModuleId", "MobileEnabled", "ModifiedBy", "ModifiedDate", "RoleId", "WebEnabled" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000007"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("81818181-8181-8181-8181-818181818181"), true, null, null, 2, true });

            migrationBuilder.CreateIndex(
                name: "IX_Wallpapers_Deity_Order",
                table: "Wallpapers",
                columns: new[] { "DeityId", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Wallpapers");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("80808080-8080-8080-8080-808080808080"));

            migrationBuilder.DeleteData(
                table: "ModuleRoleMappings",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("81818181-8181-8181-8181-818181818181"));
        }
    }
}
