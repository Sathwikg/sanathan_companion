using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LockSeededAdminAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "PasswordHash",
                value: "$2a$11$3Qm5rGml0DHTfjcYb4qBSuwNwEcQPXNbe5v8oJEt2hqNz0oV.KEwG");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty. The value this migration replaced was the BCrypt hash of the
            // literal "admin" — a working administrator credential that was also published in the
            // repository's README. Rolling the schema back is not a reason to hand it back, so the
            // account simply stays locked. To regain access, set Admin__InitialPassword and
            // restart, or use POST /api/auth/change-password while signed in.
        }
    }
}
