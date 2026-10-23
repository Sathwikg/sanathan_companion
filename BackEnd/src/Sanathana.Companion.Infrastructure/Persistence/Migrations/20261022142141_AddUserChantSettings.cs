using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A seeker's own mala size and vibration choice per chant, and banked malas on the day's log.
    /// </summary>
    public partial class AddUserChantSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MalaStartCount",
                table: "SadhanaLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Malas were TotalCount / TargetCount until now, so the mala in progress began at the
            // last whole multiple. Backfilled so today's half-finished malas keep their count.
            migrationBuilder.Sql("""
                UPDATE "SadhanaLogs" SET "MalaStartCount" = "MalasCompleted" * "TargetCount";
                """);

            migrationBuilder.CreateTable(
                name: "UserChantSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChantConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    MalaSize = table.Column<int>(type: "integer", nullable: true),
                    VibrationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChantSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_UserChantSettings_User_Chant",
                table: "UserChantSettings",
                columns: new[] { "UserId", "ChantConfigId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserChantSettings");

            migrationBuilder.DropColumn(
                name: "MalaStartCount",
                table: "SadhanaLogs");
        }
    }
}
