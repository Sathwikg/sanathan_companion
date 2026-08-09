using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPujaProcess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PujaMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PujaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Quantity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PujaMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PujaMaterials_Pujas_PujaId",
                        column: x => x.PujaId,
                        principalTable: "Pujas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PujaSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PujaId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PujaSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PujaSteps_Pujas_PujaId",
                        column: x => x.PujaId,
                        principalTable: "Pujas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PujaStepTexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PujaStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Content = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PujaStepTexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PujaStepTexts_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PujaStepTexts_PujaSteps_PujaStepId",
                        column: x => x.PujaStepId,
                        principalTable: "PujaSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPujaStepProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PujaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PujaStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPujaStepProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPujaStepProgress_PujaSteps_PujaStepId",
                        column: x => x.PujaStepId,
                        principalTable: "PujaSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPujaStepProgress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MenuModules",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "DisplayOrder", "Icon", "IsActive", "IsVisibleInMenu", "ModifiedBy", "ModifiedDate", "Name", "ParentId", "RoutePath", "ShowInMobile" },
                values: new object[,]
                {
                    { new Guid("83838383-8383-8383-8383-838383838383"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Required materials and step-by-step process for each puja", 6, "📜", true, true, null, null, "Puja Process", new Guid("99999999-9999-9999-9999-999999999999"), "/puja-process-config", false },
                    { new Guid("84848484-8484-8484-8484-848484848484"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Follow a puja step by step", 3, "📿", true, true, null, null, "Puja Process", new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), "/puja-process", true }
                });

            migrationBuilder.InsertData(
                table: "ModuleRoleMappings",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "MenuModuleId", "MobileEnabled", "ModifiedBy", "ModifiedDate", "RoleId", "WebEnabled" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000008"), "system", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("84848484-8484-8484-8484-848484848484"), true, null, null, 2, true });

            migrationBuilder.CreateIndex(
                name: "IX_PujaMaterials_Puja_Order",
                table: "PujaMaterials",
                columns: new[] { "PujaId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_PujaSteps_Puja_Number",
                table: "PujaSteps",
                columns: new[] { "PujaId", "StepNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PujaStepTexts_LanguageId",
                table: "PujaStepTexts",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "UX_PujaStepTexts_Step_Language",
                table: "PujaStepTexts",
                columns: new[] { "PujaStepId", "LanguageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPujaStepProgress_PujaStepId",
                table: "UserPujaStepProgress",
                column: "PujaStepId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPujaStepProgress_User_Puja",
                table: "UserPujaStepProgress",
                columns: new[] { "UserId", "PujaId" });

            migrationBuilder.CreateIndex(
                name: "UX_UserPujaStepProgress_User_Step",
                table: "UserPujaStepProgress",
                columns: new[] { "UserId", "PujaStepId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PujaMaterials");

            migrationBuilder.DropTable(
                name: "PujaStepTexts");

            migrationBuilder.DropTable(
                name: "UserPujaStepProgress");

            migrationBuilder.DropTable(
                name: "PujaSteps");

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("83838383-8383-8383-8383-838383838383"));

            migrationBuilder.DeleteData(
                table: "ModuleRoleMappings",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "MenuModules",
                keyColumn: "Id",
                keyValue: new Guid("84848484-8484-8484-8484-848484848484"));
        }
    }
}
