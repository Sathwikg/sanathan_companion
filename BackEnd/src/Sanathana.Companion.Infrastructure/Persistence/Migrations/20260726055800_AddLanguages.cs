using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NativeName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Regions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[] { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Manage languages and their regions", 6, "🗣️", true, true, null, null, "Languages", new Guid("33333333-3333-3333-3333-333333333333"), "/languages", true });

            migrationBuilder.CreateIndex(
                name: "UX_Languages_Name",
                table: "Languages",
                column: "Name",
                unique: true);

            migrationBuilder.Sql(SeedSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Languages");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        }

        /// <summary>
        /// Seeds the starter languages. Region links resolve by NAME into a comma-separated id
        /// list, so the seed applies to any database regardless of the region ids it holds, and
        /// simply leaves the mapping empty for regions that do not exist there.
        /// </summary>
        private const string SeedSql = """
INSERT INTO "Languages" ("Id","Name","NativeName","Code","Description","Regions","IsActive","CreatedBy","CreatedDate")
SELECT 'da000000-0000-0000-0000-000000000001'::uuid, 'Telugu', 'తెలుగు', 'te',
       'Dravidian language and the official language of both Andhra Pradesh and Telangana, widely used in devotional song and literature.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Regions" WHERE "Name" IN ('Andhra Pradesh','Telangana')),
       true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
ON CONFLICT ("Name") DO NOTHING;

INSERT INTO "Languages" ("Id","Name","NativeName","Code","Description","Regions","IsActive","CreatedBy","CreatedDate")
SELECT 'da000000-0000-0000-0000-000000000002'::uuid, 'Urdu', 'اردو', 'ur',
       'Indo-Aryan language in Perso-Arabic script; second official language of Telangana (2017) and Andhra Pradesh (2022), after Telugu.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Regions" WHERE "Name" IN ('Andhra Pradesh','Telangana')),
       true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
ON CONFLICT ("Name") DO NOTHING;

INSERT INTO "Languages" ("Id","Name","NativeName","Code","Description","Regions","IsActive","CreatedBy","CreatedDate")
SELECT 'da000000-0000-0000-0000-000000000003'::uuid, 'Hindi', 'हिन्दी', 'hi',
       'Indo-Aryan link language spoken by a minority and used in bhajans across both states; not an official language of either state.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Regions" WHERE "Name" IN ('Andhra Pradesh','Telangana')),
       true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
ON CONFLICT ("Name") DO NOTHING;

INSERT INTO "Languages" ("Id","Name","NativeName","Code","Description","Regions","IsActive","CreatedBy","CreatedDate")
SELECT 'da000000-0000-0000-0000-000000000004'::uuid, 'Sanskrit', 'संस्कृतम्', 'sa',
       'Classical liturgical language of Hindu scripture; not a state official language in Andhra Pradesh or Telangana but used in temple rituals.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Regions" WHERE "Name" IN ('Andhra Pradesh','Telangana')),
       true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
ON CONFLICT ("Name") DO NOTHING;

INSERT INTO "Languages" ("Id","Name","NativeName","Code","Description","Regions","IsActive","CreatedBy","CreatedDate")
SELECT 'da000000-0000-0000-0000-000000000005'::uuid, 'English', 'English', 'en',
       'Widely used in administration, higher education and commerce in Andhra Pradesh and Telangana, where Telugu is the main official language.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Regions" WHERE "Name" IN ('Andhra Pradesh','Telangana')),
       true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
ON CONFLICT ("Name") DO NOTHING;
""";
    }
}
