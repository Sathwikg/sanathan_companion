using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleRoleMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModuleRoleMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    MenuModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    WebEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MobileEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleRoleMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleRoleMappings_MenuModules_MenuModuleId",
                        column: x => x.MenuModuleId,
                        principalTable: "MenuModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModuleRoleMappings_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[] { new Guid("10101010-1010-1010-1010-101010101010"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Manage which forms each role can access on web and mobile", 3, "🔐", true, true, null, null, "Access Rights", new Guid("99999999-9999-9999-9999-999999999999"), "/access-rights", false });

            migrationBuilder.InsertData(
                table: "ModuleRoleMappings",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "MenuModuleId", "MobileEnabled", "ModifiedBy", "ModifiedDate", "RoleId", "WebEnabled" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000001"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("22222222-2222-2222-2222-222222222222"), true, null, null, 2, true },
                    { new Guid("a0000000-0000-0000-0000-000000000002"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), true, null, null, 2, true },
                    { new Guid("a0000000-0000-0000-0000-000000000003"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), true, null, null, 2, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModuleRoleMappings_MenuModuleId",
                table: "ModuleRoleMappings",
                column: "MenuModuleId");

            migrationBuilder.CreateIndex(
                name: "UX_ModuleRoleMappings_Role_Module",
                table: "ModuleRoleMappings",
                columns: new[] { "RoleId", "MenuModuleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModuleRoleMappings");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("10101010-1010-1010-1010-101010101010"));
        }
    }
}
