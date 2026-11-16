using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdultConfirmedAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomineeContact",
                table: "Users",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomineeName",
                table: "Users",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomineeRelationship",
                table: "Users",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrivacyNoticeAcceptedAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrivacyNoticeVersion",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsageAnalyticsConsent",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Granted = table.Column<bool>(type: "boolean", nullable: false),
                    NoticeVersion = table.Column<int>(type: "integer", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                });

            // Raised to the legal floor (one year), never lowered: an administrator who already keeps
            // logs longer keeps that setting. CERT-In requires 180 days now; DPDP Rules one year.
            migrationBuilder.Sql("""
                UPDATE "AuditSettings" SET "AuditRetentionDays" = GREATEST("AuditRetentionDays", 365),
                                           "ErrorRetentionDays" = GREATEST("ErrorRetentionDays", 365);
                """);

            // The privacy request / grievance type for the feedback form. Tolerates an existing type of
            // the same name or id (the master data has been rebuilt by hand before) instead of failing
            // the migration and with it the API's start-up.
            migrationBuilder.Sql("""
                INSERT INTO "IssueTypes" ("Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "IsActive", "Name")
                VALUES ('41000000-0000-0000-0000-000000000006', 'system', '2026-01-01T00:00:00Z',
                        'A request about your personal data (access, correction, erasure, nominee) or a privacy grievance. Answered within 30 days.',
                        0, TRUE, 'Privacy / My Data Request')
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "AdultConfirmedAtUtc", "NomineeContact", "NomineeName", "NomineeRelationship", "PrivacyNoticeAcceptedAtUtc", "PrivacyNoticeVersion", "UsageAnalyticsConsent" },
                values: new object[] { null, null, null, null, null, null, false });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_User_At",
                table: "ConsentRecords",
                columns: new[] { "UserId", "AtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentRecords");

            migrationBuilder.DeleteData(
                table: "IssueTypes",
                keyColumn: "Id",
                keyValue: new Guid("41000000-0000-0000-0000-000000000006"));

            migrationBuilder.DropColumn(
                name: "AdultConfirmedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NomineeContact",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NomineeName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NomineeRelationship",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PrivacyNoticeAcceptedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PrivacyNoticeVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UsageAnalyticsConsent",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "AuditSettings",
                keyColumn: "Id",
                keyValue: new Guid("91919191-0002-0002-0002-000000000001"),
                columns: new[] { "AuditRetentionDays", "ErrorRetentionDays" },
                values: new object[] { 90, 30 });
        }
    }
}
