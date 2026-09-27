using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lets a seeker connect a Google account: the stable Google id, when it was connected, and
    /// when a trusted party confirmed the email address.
    /// </summary>
    /// <remarks>
    /// Three nullable columns and a filtered unique index. Nothing existing changes meaning:
    /// every account keeps its password and stays unlinked until its owner connects Google. The
    /// scaffolder's UpdateData for the seeded administrator writes three nulls into columns that
    /// are already null; it is kept only so the model snapshot and the seed agree.
    /// </remarks>
    public partial class AddGoogleSignIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GoogleLinkedAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleSubject",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "EmailVerifiedAtUtc", "GoogleLinkedAtUtc", "GoogleSubject" },
                values: new object[] { null, null, null });

            migrationBuilder.CreateIndex(
                name: "UX_Users_GoogleSubject",
                table: "Users",
                column: "GoogleSubject",
                unique: true,
                filter: "\"GoogleSubject\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Users_GoogleSubject",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GoogleLinkedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GoogleSubject",
                table: "Users");
        }
    }
}
