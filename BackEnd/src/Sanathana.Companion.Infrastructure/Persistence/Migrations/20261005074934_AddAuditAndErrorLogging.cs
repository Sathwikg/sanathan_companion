using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditAndErrorLogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsernameOrEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModuleCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FormName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RoutePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    EnteredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExitedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TimeSpentSeconds = table.Column<int>(type: "integer", nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditDataLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsernameOrEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EntityName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ModuleCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ChangedColumns = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OldValuesJson = table.Column<string>(type: "text", nullable: true),
                    NewValuesJson = table.Column<string>(type: "text", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Endpoint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditDataLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditModuleConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsActivityAuditEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDataAuditEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditModuleConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditModuleConfigs_MenuModules_MenuModuleId",
                        column: x => x.MenuModuleId,
                        principalTable: "MenuModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsGlobalAuditEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TrackUserSessions = table.Column<bool>(type: "boolean", nullable: false),
                    TrackPageNavigation = table.Column<bool>(type: "boolean", nullable: false),
                    TrackDataModifications = table.Column<bool>(type: "boolean", nullable: false),
                    TrackErrorLogs = table.Column<bool>(type: "boolean", nullable: false),
                    AuditRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    ErrorRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditUserSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsernameOrEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LoginTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LogoutTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    ExitReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditUserSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Severity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    ExceptionType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StackTrace = table.Column<string>(type: "text", nullable: true),
                    InnerException = table.Column<string>(type: "text", nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RequestMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsernameOrEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorLogs", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AuditSettings",
                columns: new[] { "Id", "AuditRetentionDays", "CreatedBy", "CreatedDate", "ErrorRetentionDays", "IsGlobalAuditEnabled", "ModifiedBy", "ModifiedDate", "TrackDataModifications", "TrackErrorLogs", "TrackPageNavigation", "TrackUserSessions" },
                values: new object[] { new Guid("91919191-0002-0002-0002-000000000001"), 90, "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 30, true, null, null, true, true, true, true });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[] { new Guid("91919191-9191-9191-9191-919191919191"), "auditConfig", "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Configure system audit logs, user activity tracking, and error logging", 8, "📋", true, true, null, null, "Audit Config", new Guid("99999999-9999-9999-9999-999999999999"), "/audit-config", false });

            migrationBuilder.CreateIndex(
                name: "IX_AuditActivityLogs_EnteredAtUtc",
                table: "AuditActivityLogs",
                column: "EnteredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditActivityLogs_ModuleCode",
                table: "AuditActivityLogs",
                column: "ModuleCode");

            migrationBuilder.CreateIndex(
                name: "IX_AuditActivityLogs_SessionId",
                table: "AuditActivityLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditActivityLogs_UserId",
                table: "AuditActivityLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditDataLogs_Entity",
                table: "AuditDataLogs",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditDataLogs_TimestampUtc",
                table: "AuditDataLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditDataLogs_UserId",
                table: "AuditDataLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditModuleConfigs_ModuleCode",
                table: "AuditModuleConfigs",
                column: "ModuleCode");

            migrationBuilder.CreateIndex(
                name: "UX_AuditModuleConfigs_MenuModuleId",
                table: "AuditModuleConfigs",
                column: "MenuModuleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditUserSessions_LoginTimeUtc",
                table: "AuditUserSessions",
                column: "LoginTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditUserSessions_UserId",
                table: "AuditUserSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_IsResolved",
                table: "ErrorLogs",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_Source",
                table: "ErrorLogs",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_TimestampUtc",
                table: "ErrorLogs",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditActivityLogs");

            migrationBuilder.DropTable(
                name: "AuditDataLogs");

            migrationBuilder.DropTable(
                name: "AuditModuleConfigs");

            migrationBuilder.DropTable(
                name: "AuditSettings");

            migrationBuilder.DropTable(
                name: "AuditUserSessions");

            migrationBuilder.DropTable(
                name: "ErrorLogs");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("91919191-9191-9191-9191-919191919191"));
        }
    }
}
