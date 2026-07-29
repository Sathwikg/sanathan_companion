using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChantConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChantConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeityIds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ChantText = table.Column<string>(type: "text", nullable: false),
                    AudioFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AudioContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AudioSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    FromTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ToTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TimeDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChantConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChantConfigs_Chants_ChantId",
                        column: x => x.ChantId,
                        principalTable: "Chants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChantConfigAudios",
                columns: table => new
                {
                    ChantConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChantConfigAudios", x => x.ChantConfigId);
                    table.ForeignKey(
                        name: "FK_ChantConfigAudios_ChantConfigs_ChantConfigId",
                        column: x => x.ChantConfigId,
                        principalTable: "ChantConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[,]
                {
                    { new Guid("99999999-9999-9999-9999-999999999999"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Application configuration", 3, "⚙️", true, true, null, null, "Configuration", null, null, false },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Configure chants under each chant category", 1, "📜", true, true, null, null, "Chants Config", new Guid("99999999-9999-9999-9999-999999999999"), "/chants-config", true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChantConfigs_ChantId",
                table: "ChantConfigs",
                column: "ChantId");

            migrationBuilder.CreateIndex(
                name: "UX_ChantConfigs_Name",
                table: "ChantConfigs",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChantConfigAudios");

            migrationBuilder.DropTable(
                name: "ChantConfigs");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"));
        }
    }
}
