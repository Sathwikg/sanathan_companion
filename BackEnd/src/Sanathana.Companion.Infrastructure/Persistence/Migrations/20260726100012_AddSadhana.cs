using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSadhana : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SadhanaLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ChantConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeityName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CategoryName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TargetCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    MalasCompleted = table.Column<int>(type: "integer", nullable: false),
                    WasRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SadhanaLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SadhanaLogs_ChantConfigs_ChantConfigId",
                        column: x => x.ChantConfigId,
                        principalTable: "ChantConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SadhanaStreaks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStreak = table.Column<int>(type: "integer", nullable: false),
                    LongestStreak = table.Column<int>(type: "integer", nullable: false),
                    LastPracticeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TotalMalas = table.Column<int>(type: "integer", nullable: false),
                    TotalDaysPracticed = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SadhanaStreaks", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[,]
                {
                    { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Daily spiritual practice", 4, "🙏", true, true, null, null, "Sadhana", null, null, true },
                    { new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Recommended chants for today and your japa practice", 1, "🪷", true, true, null, null, "Today's Sadhana", new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), "/sadhana", true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SadhanaLogs_ChantConfigId",
                table: "SadhanaLogs",
                column: "ChantConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_SadhanaLogs_User_Date",
                table: "SadhanaLogs",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "UX_SadhanaLogs_User_Date_Chant",
                table: "SadhanaLogs",
                columns: new[] { "UserId", "Date", "ChantConfigId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SadhanaStreaks_User",
                table: "SadhanaStreaks",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SadhanaLogs");

            migrationBuilder.DropTable(
                name: "SadhanaStreaks");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        }
    }
}
