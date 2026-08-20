using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives every form a stable name, so an API endpoint can say which one it belongs to.
    /// </summary>
    /// <remarks>
    /// Access Rights previously decided only which links appeared in the menu. This column is what
    /// lets the same matrix gate the endpoints, and it has to be stable: the module's Name is not
    /// unique (two seeded rows are both "Puja Process") and its RoutePath is free text an
    /// administrator can edit, so authorization keyed on either would break on an ordinary edit.
    /// </remarks>
    public partial class AddMenuModuleCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "MenuModules",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("10101010-1010-1010-1010-101010101010"),
                column: "Code",
                value: "accessRights");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("20202020-2020-2020-2020-202020202020"),
                column: "Code",
                value: "roles");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "Code",
                value: "dashboard");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("30303030-3030-3030-3030-303030303030"),
                column: "Code",
                value: "adminDashboard");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "Code",
                value: null);

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("40404040-4040-4040-4040-404040404040"),
                column: "Code",
                value: null);

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("41414141-4141-4141-4141-414141414141"),
                column: "Code",
                value: "feedback");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("42424242-4242-4242-4242-424242424242"),
                column: "Code",
                value: "feedbackDashboard");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("43434343-4343-4343-4343-434343434343"),
                column: "Code",
                value: "issueTypes");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "Code",
                value: "modules");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("50505050-5050-5050-5050-505050505050"),
                column: "Code",
                value: "favorites");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "Code",
                value: "regions");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("60606060-6060-6060-6060-606060606060"),
                column: "Code",
                value: null);

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("61616161-6161-6161-6161-616161616161"),
                column: "Code",
                value: "notificationConfig");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("62626262-6262-6262-6262-626262626262"),
                column: "Code",
                value: "myNotifications");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "Code",
                value: "festivals");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("70707070-7070-7070-7070-707070707070"),
                column: "Code",
                value: "languageConfigs");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "Code",
                value: "deities");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("80808080-8080-8080-8080-808080808080"),
                column: "Code",
                value: "wallpapers");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("81818181-8181-8181-8181-818181818181"),
                column: "Code",
                value: "wallpapersDownload");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("82828282-8282-8282-8282-828282828282"),
                column: "Code",
                value: "pujas");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("83838383-8383-8383-8383-838383838383"),
                column: "Code",
                value: "pujaProcessConfig");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("84848484-8484-8484-8484-848484848484"),
                column: "Code",
                value: "pujaProcess");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "Code",
                value: "chants");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "Code",
                value: null);

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                column: "Code",
                value: "chantsConfig");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                column: "Code",
                value: "languages");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                column: "Code",
                value: "panchangam");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                column: "Code",
                value: null);

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                column: "Code",
                value: "sadhana");

            migrationBuilder.UpdateData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                column: "Code",
                value: "users");

            // The UpdateData calls above cover the rows this application seeds. Anything else with a
            // route gets the same derivation, so a form an operator deleted and recreated by hand
            // is not left permanently closed to non-administrators. Collisions are skipped rather
            // than allowed to fail the index: a duplicate route is an existing data problem, and
            // leaving that row without a code denies it, which is the safe direction.
            migrationBuilder.Sql(@"
                UPDATE ""MenuModules"" m
                   SET ""Code"" = d.code
                  FROM (
                        SELECT ""Id"",
                               lower(split_part(regexp_replace(btrim(""RoutePath"", '/'), '/.*$', ''), '-', 1)) ||
                               CASE
                                 WHEN strpos(regexp_replace(btrim(""RoutePath"", '/'), '/.*$', ''), '-') = 0 THEN ''
                                 ELSE initcap(lower(split_part(regexp_replace(btrim(""RoutePath"", '/'), '/.*$', ''), '-', 2)))
                                      || initcap(lower(split_part(regexp_replace(btrim(""RoutePath"", '/'), '/.*$', ''), '-', 3)))
                               END AS code
                          FROM ""MenuModules""
                         WHERE ""Code"" IS NULL
                           AND ""RoutePath"" IS NOT NULL
                           AND btrim(""RoutePath"", '/') <> ''
                       ) d
                 WHERE d.""Id"" = m.""Id""
                   AND d.code <> ''
                   AND NOT EXISTS (SELECT 1 FROM ""MenuModules"" o WHERE o.""Code"" = d.code);
            ");

            migrationBuilder.CreateIndex(
                name: "UX_MenuModules_Code",
                table: "MenuModules",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_MenuModules_Code",
                table: "MenuModules");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "MenuModules");
        }
    }
}
