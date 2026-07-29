using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFestivals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Festivals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Festivals", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Festivals",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Date", "Description", "IsActive", "ModifiedBy", "ModifiedDate", "Name", "Year" },
                values: new object[,]
                {
                    { new Guid("fe510000-0000-0000-0000-000000000001"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 14), "Harvest festival marking the sun's transition into Capricorn", true, null, null, "Makar Sankranti", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000002"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 23), "Worship of Goddess Saraswati, welcoming spring", true, null, null, "Vasant Panchami", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000003"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 2, 15), "The great night of Lord Shiva", true, null, null, "Maha Shivaratri", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000004"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 3, 3), "Festival of colours celebrating the triumph of good over evil", true, null, null, "Holi", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000005"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 3, 19), "Hindu New Year for the Deccan region", true, null, null, "Ugadi / Gudi Padwa", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000006"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 3, 27), "Birth of Lord Rama", true, null, null, "Rama Navami", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000007"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 4, 2), "Birth of Lord Hanuman", true, null, null, "Hanuman Jayanti", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000008"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 7, 29), "Honouring spiritual gurus and teachers", true, null, null, "Guru Purnima", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000009"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 8, 28), "Celebrating the sacred bond between brothers and sisters", true, null, null, "Raksha Bandhan", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000010"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 9, 4), "Birth of Lord Krishna", true, null, null, "Krishna Janmashtami", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000011"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 9, 14), "Birth of Lord Ganesha", true, null, null, "Ganesh Chaturthi", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000012"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 10, 11), "Nine nights devoted to Goddess Durga", true, null, null, "Navaratri Begins", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000013"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 10, 20), "Victory of good over evil", true, null, null, "Dussehra (Vijayadashami)", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000014"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 10, 29), "Fast observed for the well-being of one's spouse", true, null, null, "Karwa Chauth", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000015"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 8), "Festival of lights", true, null, null, "Diwali (Deepavali)", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000016"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 10), "Worship of Govardhan Hill", true, null, null, "Govardhan Puja", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000017"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 11), "Celebrating the bond between brothers and sisters", true, null, null, "Bhai Dooj", 2026 },
                    { new Guid("fe510000-0000-0000-0000-000000000018"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 15), "Worship of the Sun God, Surya", true, null, null, "Chhath Puja", 2026 }
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[] { new Guid("66666666-6666-6666-6666-666666666666"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Manage festivals by year", 3, "🎉", true, true, null, null, "Festivals", new Guid("33333333-3333-3333-3333-333333333333"), "/festivals", true });

            migrationBuilder.CreateIndex(
                name: "IX_Festivals_Year",
                table: "Festivals",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "UX_Festivals_Year_Name",
                table: "Festivals",
                columns: new[] { "Year", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Festivals");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"));
        }
    }
}
