using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileDashboardMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "ShowInMobile",
                value: false);

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[] { new Guid("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1"), "mobileDashboard", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "User mobile dashboard — the phone home screen", 2, "🏠", true, true, null, null, "Mobile Home", null, "/mobile-dashboard", true });

            migrationBuilder.InsertData(
                table: "ModuleRoleMappings",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "MenuModuleId", "MobileEnabled", "ModifiedBy", "ModifiedDate", "RoleId", "WebEnabled" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000009"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1"), true, null, null, 2, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ModuleRoleMappings",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1"));

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "ShowInMobile",
                value: true);
        }
    }
}
