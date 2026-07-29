using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IssueTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Feedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Feedbacks_IssueTypes_IssueTypeId",
                        column: x => x.IssueTypeId,
                        principalTable: "IssueTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "IssueTypes",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "IsActive", "ModifiedBy", "ModifiedDate", "Name" },
                values: new object[,]
                {
                    { new Guid("41000000-0000-0000-0000-000000000001"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Something in the app isn't working correctly.", 1, true, null, null, "Bug / Technical Issue" },
                    { new Guid("41000000-0000-0000-0000-000000000002"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "A chant, deity, festival or other detail needs correcting.", 2, true, null, null, "Content Correction" },
                    { new Guid("41000000-0000-0000-0000-000000000003"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Suggest a new feature or an improvement.", 3, true, null, null, "Feature Request" },
                    { new Guid("41000000-0000-0000-0000-000000000004"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Share what you love about the app.", 4, true, null, null, "Praise / Appreciation" },
                    { new Guid("41000000-0000-0000-0000-000000000005"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Anything else you'd like to share.", 5, true, null, null, "Other" }
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[,]
                {
                    { new Guid("40404040-4040-4040-4040-404040404040"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Share feedback and review what seekers have sent", 6, "💬", true, true, null, null, "Feedback", null, null, true },
                    { new Guid("41414141-4141-4141-4141-414141414141"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Send feedback, suggestions or report an issue", 1, "📝", true, true, null, null, "Feedback Form", new Guid("40404040-4040-4040-4040-404040404040"), "/feedback", true },
                    { new Guid("42424242-4242-4242-4242-424242424242"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Review and triage the feedback seekers have sent", 2, "📊", true, true, null, null, "Feedback Dashboard", new Guid("40404040-4040-4040-4040-404040404040"), "/feedback-dashboard", false },
                    { new Guid("43434343-4343-4343-4343-434343434343"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Manage the feedback issue types", 3, "🏷️", true, true, null, null, "Issue Types", new Guid("40404040-4040-4040-4040-404040404040"), "/issue-types", false }
                });

            migrationBuilder.InsertData(
                table: "ModuleRoleMappings",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "MenuModuleId", "MobileEnabled", "ModifiedBy", "ModifiedDate", "RoleId", "WebEnabled" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000004"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("41414141-4141-4141-4141-414141414141"), true, null, null, 2, true });

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_CreatedDate",
                table: "Feedbacks",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_IssueTypeId",
                table: "Feedbacks",
                column: "IssueTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_UserId",
                table: "Feedbacks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_IssueTypes_Name",
                table: "IssueTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Feedbacks");

            migrationBuilder.DropTable(
                name: "IssueTypes");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("42424242-4242-4242-4242-424242424242"));

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("43434343-4343-4343-4343-434343434343"));

            migrationBuilder.DeleteData(
                table: "ModuleRoleMappings",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("41414141-4141-4141-4141-414141414141"));

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("40404040-4040-4040-4040-404040404040"));
        }
    }
}
