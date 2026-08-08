using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationDictionary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TranslationSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TableName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ColumnName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MaxDistinct = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TranslationTerms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TermKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Source = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MissCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationTerms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TranslationTermTexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsSeeded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationTermTexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranslationTermTexts_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TranslationTermTexts_TranslationTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "TranslationTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "TranslationSources",
                columns: new[] { "Id", "Category", "ColumnName", "CreatedBy", "CreatedDate", "IsActive", "MaxDistinct", "Mode", "ModifiedBy", "ModifiedDate", "TableName" },
                values: new object[,]
                {
                    { new Guid("7c000000-0000-0000-0000-000000000001"), "panchangam", "DayOfWeek", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000002"), "panchangam", "TeluguSamvatsaram", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000003"), "panchangam", "Ayanam", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000004"), "panchangam", "Masam", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000005"), "panchangam", "Paksham", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000006"), "panchangam", "Rutuvu", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000007"), "panchangam", "TithiDetails", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "Words", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000008"), "panchangam", "NakshatramDetails", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "Words", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000009"), "panchangam", "AmruthaKalam", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "Words", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000010"), "panchangam", "AbhijitMuhurtham", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "Words", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000011"), "panchangam", "Durmuhurtham", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "Words", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000012"), "panchangam", "RahuKalam", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "Words", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000013"), "panchangam", "Yamagandam", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "Words", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000014"), "panchangam", "Varjyam", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "Words", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000015"), "panchangam", "Gulika", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "Words", null, null, "Panchangams" },
                    { new Guid("7c000000-0000-0000-0000-000000000016"), "day", "Name", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Days" },
                    { new Guid("7c000000-0000-0000-0000-000000000017"), "deityType", "DeityType", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Deities" },
                    { new Guid("7c000000-0000-0000-0000-000000000018"), "deity", "Name", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Deities" },
                    { new Guid("7c000000-0000-0000-0000-000000000019"), "chantCategory", "Name", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Chants" },
                    { new Guid("7c000000-0000-0000-0000-000000000020"), "festival", "Name", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Festivals" },
                    { new Guid("7c000000-0000-0000-0000-000000000021"), "region", "Name", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Regions" },
                    { new Guid("7c000000-0000-0000-0000-000000000022"), "issueType", "Name", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "IssueTypes" },
                    { new Guid("7c000000-0000-0000-0000-000000000023"), "status", "Status", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "Feedbacks" },
                    { new Guid("7c000000-0000-0000-0000-000000000024"), "notification", "Title", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5000, "WholeValue", null, null, "NotificationConfigs" }
                });

            migrationBuilder.CreateIndex(
                name: "UX_TranslationSources_Table_Column",
                table: "TranslationSources",
                columns: new[] { "TableName", "ColumnName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranslationTerms_Category_Active",
                table: "TranslationTerms",
                columns: new[] { "Category", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UX_TranslationTerms_Key",
                table: "TranslationTerms",
                column: "TermKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranslationTermTexts_TermId",
                table: "TranslationTermTexts",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "UX_TranslationTermTexts_Lang_Term",
                table: "TranslationTermTexts",
                columns: new[] { "LanguageId", "TermId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslationSources");

            migrationBuilder.DropTable(
                name: "TranslationTermTexts");

            migrationBuilder.DropTable(
                name: "TranslationTerms");
        }
    }
}
