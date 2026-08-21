using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleAdsConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdFormats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    PlacementGuidance = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    IsFullScreen = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresUserOptIn = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdFormats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AndroidAppId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IosAppId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UseTestAds = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdPlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AdFormatId = table.Column<Guid>(type: "uuid", nullable: true),
                    AndroidAdUnitId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IosAdUnitId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdPlacements_AdFormats_AdFormatId",
                        column: x => x.AdFormatId,
                        principalTable: "AdFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdPlacements_MenuModules_MenuModuleId",
                        column: x => x.MenuModuleId,
                        principalTable: "MenuModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AdFormats",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "IsActive", "IsFullScreen", "ModifiedBy", "ModifiedDate", "Name", "PlacementGuidance", "RequiresUserOptIn" },
                values: new object[,]
                {
                    { new Guid("a1a1a1a1-0001-0001-0001-000000000001"), "banner", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "A rectangular strip that occupies part of the screen and stays while the seeker reads. Refreshes on a timer.", 1, true, false, null, null, "Banner", "Keep it clear of buttons, menus and anything tappable — adjacent controls are the single biggest cause of accidental clicks, and accidental clicks are what gets ad serving disabled.", false },
                    { new Guid("a1a1a1a1-0001-0001-0001-000000000002"), "interstitial", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "A full page that covers the app until the seeker dismisses it.", 2, true, true, null, null, "Interstitial", "Only at a natural break between one piece of content and the next. Never on app launch or exit, never straight after another interstitial, and no more than one per two actions. It must not appear while somebody is concentrating on something.", false },
                    { new Guid("a1a1a1a1-0001-0001-0001-000000000003"), "native", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ad content rendered with the app's own styling, so it sits inside a list or a card like ordinary content.", 3, true, false, null, null, "Native", "It has to be labelled as an ad and must not be made to look like something the seeker can act on by mistake.", false },
                    { new Guid("a1a1a1a1-0001-0001-0001-000000000004"), "rewarded", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "A video the seeker chooses to watch in exchange for something in return.", 4, true, true, null, null, "Rewarded", "The seeker must opt in before it plays, and must be told what they get for it. Nothing they already had may be withheld to make them watch.", true },
                    { new Guid("a1a1a1a1-0001-0001-0001-000000000005"), "rewardedInterstitial", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "A full-page reward ad at a transition. Unlike Rewarded, the seeker does not have to opt in first.", 5, true, true, null, null, "Rewarded Interstitial", "An introductory screen has to announce it and offer a way out before it plays. Same transition rules as an interstitial.", false },
                    { new Guid("a1a1a1a1-0001-0001-0001-000000000006"), "appOpen", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Shown over the loading screen when the app is opened or returned to.", 6, true, true, null, null, "App Open", "This is the only format allowed at app launch — an interstitial there is a policy breach. It belongs on the app itself rather than on any one form.", false }
                });

            migrationBuilder.InsertData(
                table: "AdSettings",
                columns: new[] { "Id", "AdsEnabled", "AndroidAppId", "CreatedBy", "CreatedDate", "IosAppId", "ModifiedBy", "ModifiedDate", "UseTestAds" },
                values: new object[] { new Guid("a1a1a1a1-0002-0002-0002-000000000001"), false, null, "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, true });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[] { new Guid("90909090-9090-9090-9090-909090909090"), "adConfig", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Choose which forms show ads, and which single format each one shows", 7, "📢", true, true, null, null, "Ad Config", new Guid("99999999-9999-9999-9999-999999999999"), "/ad-config", false });

            migrationBuilder.CreateIndex(
                name: "UX_AdFormats_Code",
                table: "AdFormats",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdPlacements_AdFormatId",
                table: "AdPlacements",
                column: "AdFormatId");

            migrationBuilder.CreateIndex(
                name: "UX_AdPlacements_MenuModuleId",
                table: "AdPlacements",
                column: "MenuModuleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdPlacements");

            migrationBuilder.DropTable(
                name: "AdSettings");

            migrationBuilder.DropTable(
                name: "AdFormats");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("90909090-9090-9090-9090-909090909090"));
        }
    }
}
