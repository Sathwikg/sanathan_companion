using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF EXISTS, not DropIndex: this database has lost indexes before (a restore that
            // carried the tables and not their constraints), and a migration that stops on a missing
            // index stops the API from starting. The index it is replaced by is created below.
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"UX_NotificationConfigs_Module\";");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "NotificationConfigs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "NotificationConfigs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "reminder");

            migrationBuilder.AddColumn<int>(
                name: "LeadMinutes",
                table: "NotificationConfigs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "SendTime",
                table: "NotificationConfigs",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Threshold",
                table: "NotificationConfigs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "DeviceTokens",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "DeviceTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NotificationConfigId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Route = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DedupeKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Devices = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    Due = table.Column<int>(type: "integer", nullable: false),
                    Sent = table.Column<int>(type: "integer", nullable: false),
                    Failed = table.Column<int>(type: "integer", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRuns", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[,]
                {
                    { new Guid("63636363-6363-6363-6363-636363636363"), "pushNotifications", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Every notification as a job: schedule, last run, delivery log, devices, run now and test", 3, "📡", true, true, null, null, "Notification Console", new Guid("60606060-6060-6060-6060-606060606060"), "/push-notifications", false },
                    { new Guid("64646464-6464-6464-6464-646464646464"), "hangfire", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "The server's recurring jobs: status, failures, run now", 9, "⚙️", true, true, null, null, "Background Jobs", new Guid("99999999-9999-9999-9999-999999999999"), "/hangfire", false }
                });

            // The plain reminder an administrator had already switched on for Today's Sadhana becomes
            // the sadhana reminder the server now sends. Same row, so every seeker keeps the time they
            // chose and their on/off choice; a second, separate "sadhana reminder" beside it would
            // have asked them to choose again and reminded them twice.
            migrationBuilder.Sql("""
                UPDATE "NotificationConfigs" c
                SET "Kind" = 'sadhanaReminder'
                FROM "MenuModules" m
                WHERE m."Id" = c."MenuModuleId" AND m."Code" = 'sadhana' AND c."Kind" = 'reminder';
                """);

            migrationBuilder.CreateIndex(
                name: "UX_NotificationConfigs_Module_Kind",
                table: "NotificationConfigs",
                columns: new[] { "MenuModuleId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_Created",
                table: "NotificationLogs",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_User_Created",
                table: "NotificationLogs",
                columns: new[] { "UserId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "UX_NotificationLogs_DedupeKey",
                table: "NotificationLogs",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRuns_Kind_Started",
                table: "NotificationRuns",
                columns: new[] { "Kind", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "NotificationRuns");

            migrationBuilder.DropIndex(
                name: "UX_NotificationConfigs_Module_Kind",
                table: "NotificationConfigs");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("63636363-6363-6363-6363-636363636363"));

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("64646464-6464-6464-6464-646464646464"));

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "NotificationConfigs");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "NotificationConfigs");

            migrationBuilder.DropColumn(
                name: "LeadMinutes",
                table: "NotificationConfigs");

            migrationBuilder.DropColumn(
                name: "SendTime",
                table: "NotificationConfigs");

            migrationBuilder.DropColumn(
                name: "Threshold",
                table: "NotificationConfigs");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "DeviceTokens");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "DeviceTokens");

            migrationBuilder.CreateIndex(
                name: "UX_NotificationConfigs_Module",
                table: "NotificationConfigs",
                column: "MenuModuleId",
                unique: true);
        }
    }
}
