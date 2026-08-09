using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropPujaStepProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPujaStepProgress");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPujaStepProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PujaStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PujaId = table.Column<Guid>(type: "uuid", nullable: false)
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
    }
}
