using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPanchangam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Regions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Regions",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Panchangams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TeluguSamvatsaram = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Ayanam = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SakaSamvatsaram = table.Column<int>(type: "integer", nullable: true),
                    VikramaSamvatsaram = table.Column<int>(type: "integer", nullable: true),
                    Masam = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Paksham = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Rutuvu = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Sunrise = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Sunset = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TithiDetails = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NakshatramDetails = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AmruthaKalam = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AbhijitMuhurtham = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Durmuhurtham = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RahuKalam = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Yamagandam = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Varjyam = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Gulika = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Panchangams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Panchangams_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[] { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Daily Panchangam by region and location", 2, "🗓️", true, true, null, null, "Panchangam", new Guid("99999999-9999-9999-9999-999999999999"), "/panchangam", true });

            migrationBuilder.CreateIndex(
                name: "IX_Panchangams_RegionId",
                table: "Panchangams",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_Panchangams_Year_Region",
                table: "Panchangams",
                columns: new[] { "Year", "RegionId" });

            migrationBuilder.CreateIndex(
                name: "UX_Panchangams_Date_Region",
                table: "Panchangams",
                columns: new[] { "Date", "RegionId" },
                unique: true);

            // Reference coordinates for Panchangam calculation, resolved by region name so this
            // seeds correctly whatever ids the target database holds. Telangana -> Hyderabad;
            // Andhra Pradesh -> Vijayawada (most populous city).
            migrationBuilder.Sql("""
UPDATE "Regions" SET "Latitude" = 17.3850, "Longitude" = 78.4867 WHERE "Name" = 'Telangana' AND "Latitude" IS NULL;
UPDATE "Regions" SET "Latitude" = 16.5062, "Longitude" = 80.6480 WHERE "Name" = 'Andhra Pradesh' AND "Latitude" IS NULL;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Panchangams");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"));

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Regions");
        }
    }
}
