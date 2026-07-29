using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminDashboardMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "Description", "DisplayOrder", "Name" },
                values: new object[] { "Your personal sadhana home", 2, "User Dashboard" });

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "DisplayOrder",
                value: 3);

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "DisplayOrder",
                value: 4);

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                column: "DisplayOrder",
                value: 5);

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[] { new Guid("30303030-3030-3030-3030-303030303030"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Community & sadhana analytics for administrators", 1, "📊", true, true, null, null, "Admin Dashboard", null, "/admin-dashboard", false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("30303030-3030-3030-3030-303030303030"));

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "Description", "DisplayOrder", "Name" },
                values: new object[] { "Home dashboard", 1, "Dashboard" });

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "DisplayOrder",
                value: 2);

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "DisplayOrder",
                value: 3);

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                column: "DisplayOrder",
                value: 4);
        }
    }
}
