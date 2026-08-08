using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntityTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EntityKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Field = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LanguageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntityTranslations_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LanguageFormConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageFormConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LanguageFormConfigs_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LanguageFormConfigs_MenuModules_MenuModuleId",
                        column: x => x.MenuModuleId,
                        principalTable: "MenuModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocalizationResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Namespace = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    LanguageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsSeeded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalizationResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocalizationResources_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[] { new Guid("70707070-7070-7070-7070-707070707070"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Translate the app and choose which forms use each language", 4, "🌐", true, true, null, null, "Language Configs", new Guid("99999999-9999-9999-9999-999999999999"), "/language-configs", false });

            migrationBuilder.CreateIndex(
                name: "UX_EntityTranslations_Lang_Type_Key_Field",
                table: "EntityTranslations",
                columns: new[] { "LanguageId", "EntityType", "EntityKey", "Field" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LanguageFormConfigs_MenuModuleId",
                table: "LanguageFormConfigs",
                column: "MenuModuleId");

            migrationBuilder.CreateIndex(
                name: "UX_LanguageFormConfigs_Language_Module",
                table: "LanguageFormConfigs",
                columns: new[] { "LanguageId", "MenuModuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocalizationResources_Language_Namespace",
                table: "LocalizationResources",
                columns: new[] { "LanguageId", "Namespace" });

            migrationBuilder.CreateIndex(
                name: "UX_LocalizationResources_Language_Key",
                table: "LocalizationResources",
                columns: new[] { "LanguageId", "Key" },
                unique: true);

            migrationBuilder.Sql(SeedSql);
        }

        /// <summary>
        /// Tamil was not part of the original language seed but is one of the shipped UI languages,
        /// and the other four must be selectable. Written as idempotent SQL so an environment that
        /// already added Tamil by hand is left alone.
        /// </summary>
        private const string SeedSql = """
INSERT INTO "Languages" ("Id","Name","NativeName","Code","Description","Regions","IsActive","CreatedBy","CreatedDate")
SELECT 'da000000-0000-0000-0000-000000000006'::uuid, 'Tamil', 'தமிழ்', 'ta',
       'Classical Dravidian language of Tamil Nadu, with a deep devotional literature tradition.',
       NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
ON CONFLICT ("Name") DO NOTHING;

-- The shipped UI languages must be selectable for the switcher to offer them.
UPDATE "Languages" SET "IsActive" = true WHERE lower("Code") IN ('en','te','hi','ta','kn');
""";

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntityTranslations");

            migrationBuilder.DropTable(
                name: "LanguageFormConfigs");

            migrationBuilder.DropTable(
                name: "LocalizationResources");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("70707070-7070-7070-7070-707070707070"));
        }
    }
}
