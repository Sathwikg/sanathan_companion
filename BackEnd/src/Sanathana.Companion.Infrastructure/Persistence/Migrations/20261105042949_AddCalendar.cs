using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The Calendar form: a seeker's own marks table, the menu row under Sadhana, the seeker grant,
    /// and — by SQL, like AddGuestRole — the guest grant and the menu name in each language.
    /// </summary>
    public partial class AddCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RepeatYearly = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[] { new Guid("ca1e0da8-0000-0000-0000-000000000001"), "calendar", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Month calendar: tithis, moon, festivals and deities by region, with your own marks", 2, "📅", true, true, null, null, "Calendar", new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), "/calendar", true });

            migrationBuilder.InsertData(
                table: "ModuleRoleMappings",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "MenuModuleId", "MobileEnabled", "ModifiedBy", "ModifiedDate", "RoleId", "WebEnabled" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000010"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("ca1e0da8-0000-0000-0000-000000000001"), true, null, null, 2, true });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_User_Date",
                table: "CalendarEvents",
                columns: new[] { "UserId", "Date" });

            // A guest may browse the month like a seeker browses the Panchangam; the marks endpoints
            // stay [Authorize] and the page asks a guest to sign in. Guarded like AddGuestRole: a no-op
            // on a database without the Guest role, and never a duplicate.
            migrationBuilder.Sql("""
                INSERT INTO "ModuleRoleMappings" ("Id", "RoleId", "MenuModuleId", "WebEnabled", "MobileEnabled", "CreatedBy", "CreatedDate")
                SELECT 'b0000000-0000-0000-0000-000000000010'::uuid, r."RoleId", 'ca1e0da8-0000-0000-0000-000000000001'::uuid, TRUE, TRUE, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
                FROM "Roles" r
                WHERE lower(r."RoleName") = 'guest'
                  AND NOT EXISTS (SELECT 1 FROM "ModuleRoleMappings" x WHERE x."RoleId" = r."RoleId" AND x."MenuModuleId" = 'ca1e0da8-0000-0000-0000-000000000001'::uuid)
                  AND NOT EXISTS (SELECT 1 FROM "ModuleRoleMappings" x WHERE x."Id" = 'b0000000-0000-0000-0000-000000000010'::uuid);
                """);

            // The menu label in each language. Language ids differ between databases (two were
            // re-created during the master-data rebuild), so they are resolved by code, not seeded.
            migrationBuilder.Sql("""
                INSERT INTO "EntityTranslations" ("Id", "EntityType", "EntityKey", "Field", "LanguageId", "Text", "CreatedBy", "CreatedDate")
                SELECT gen_random_uuid(), 'MenuModule', 'ca1e0da8-0000-0000-0000-000000000001', 'Name', l."Id", t.text, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
                FROM (VALUES
                    ('te', 'క్యాలెండర్'),
                    ('hi', 'कैलेंडर'),
                    ('ta', 'காலண்டர்'),
                    ('kn', 'ಕ್ಯಾಲೆಂಡರ್')
                ) AS t(code, text)
                JOIN "Languages" l ON lower(l."Code") = t.code
                WHERE NOT EXISTS (
                    SELECT 1 FROM "EntityTranslations" x
                    WHERE x."LanguageId" = l."Id" AND x."EntityType" = 'MenuModule'
                      AND x."EntityKey" = 'ca1e0da8-0000-0000-0000-000000000001' AND x."Field" = 'Name');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEvents");

            migrationBuilder.Sql("""
                DELETE FROM "EntityTranslations" WHERE "EntityType" = 'MenuModule' AND "EntityKey" = 'ca1e0da8-0000-0000-0000-000000000001';
                DELETE FROM "ModuleRoleMappings" WHERE "MenuModuleId" = 'ca1e0da8-0000-0000-0000-000000000001'::uuid;
                """);

            migrationBuilder.DeleteData(
                table: "ModuleRoleMappings",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("ca1e0da8-0000-0000-0000-000000000001"));
        }
    }
}
