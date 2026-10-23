using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the Guest role — what a visitor who chose "Continue as Guest" may browse — with the same
    /// default forms the Sanathan role is seeded with. Administrators adjust it on Access Rights.
    /// </summary>
    /// <remarks>
    /// Hand-written SQL rather than HasData, on purpose:
    /// <list type="bullet">
    /// <item>The role takes an explicit id one past the highest. Roles is an identity column whose
    /// sequence was never advanced past the two HasData rows, so letting it pick could collide with
    /// Admin (1) or Sanathan (2) — and a fixed HasData id could collide with a role an administrator
    /// has already created. The sequence is then moved up to the highest id, which also stops the
    /// next role created on the Roles screen from colliding.</item>
    /// <item>Every statement is guarded, so it is a no-op on a database that already has the rows
    /// and never fails on one that lost a module row: grants are only inserted for modules that
    /// exist.</item>
    /// </list>
    /// </remarks>
    public partial class AddGuestRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Roles" ("RoleId", "RoleName", "Description", "CreatedBy", "CreatedDate")
                SELECT n.next_id, 'Guest',
                       'Visitors browsing without an account (Continue as Guest). Never assigned to a user.',
                       'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
                FROM (SELECT COALESCE(MAX("RoleId"), 0) + 1 AS next_id FROM "Roles") AS n
                WHERE NOT EXISTS (SELECT 1 FROM "Roles" WHERE lower("RoleName") = 'guest');
                """);

            // setval is STRICT: on a table that has lost its identity the sequence name is NULL and
            // this does nothing rather than failing.
            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('"Roles"', 'RoleId'), (SELECT MAX("RoleId") FROM "Roles"));
                """);

            // Mirrors the Sanathan seed in ModuleRoleMappingConfiguration: the devotee-facing forms.
            // Favorites, Feedback and My Notifications are granted so they appear in a guest's
            // menu — the pages themselves ask a guest to sign in.
            migrationBuilder.Sql("""
                INSERT INTO "ModuleRoleMappings" ("Id", "RoleId", "MenuModuleId", "WebEnabled", "MobileEnabled", "CreatedBy", "CreatedDate")
                SELECT g.id, r."RoleId", g.module, g.web, g.mobile, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
                FROM (VALUES
                    ('b0000000-0000-0000-0000-000000000001'::uuid, '22222222-2222-2222-2222-222222222222'::uuid, TRUE,  TRUE),  -- User Dashboard
                    ('b0000000-0000-0000-0000-000000000002'::uuid, 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee'::uuid, TRUE,  TRUE),  -- Today's Sadhana
                    ('b0000000-0000-0000-0000-000000000003'::uuid, 'cccccccc-cccc-cccc-cccc-cccccccccccc'::uuid, TRUE,  TRUE),  -- Panchangam
                    ('b0000000-0000-0000-0000-000000000004'::uuid, '41414141-4141-4141-4141-414141414141'::uuid, TRUE,  TRUE),  -- Feedback
                    ('b0000000-0000-0000-0000-000000000005'::uuid, '50505050-5050-5050-5050-505050505050'::uuid, TRUE,  TRUE),  -- Favorites
                    ('b0000000-0000-0000-0000-000000000006'::uuid, '62626262-6262-6262-6262-626262626262'::uuid, TRUE,  TRUE),  -- My Notifications
                    ('b0000000-0000-0000-0000-000000000007'::uuid, '81818181-8181-8181-8181-818181818181'::uuid, TRUE,  TRUE),  -- Download Wallpapers
                    ('b0000000-0000-0000-0000-000000000008'::uuid, '84848484-8484-8484-8484-848484848484'::uuid, TRUE,  TRUE),  -- Puja Process
                    ('b0000000-0000-0000-0000-000000000009'::uuid, 'a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1'::uuid, FALSE, TRUE)   -- Mobile Home
                ) AS g(id, module, web, mobile)
                JOIN "Roles" r ON lower(r."RoleName") = 'guest'
                JOIN "MenuModules" m ON m."Id" = g.module
                WHERE NOT EXISTS (SELECT 1 FROM "ModuleRoleMappings" x WHERE x."RoleId" = r."RoleId" AND x."MenuModuleId" = g.module)
                  AND NOT EXISTS (SELECT 1 FROM "ModuleRoleMappings" x WHERE x."Id" = g.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "ModuleRoleMappings"
                WHERE "RoleId" IN (SELECT "RoleId" FROM "Roles" WHERE "RoleName" = 'Guest');
                """);

            migrationBuilder.Sql("""
                DELETE FROM "Roles" r
                WHERE r."RoleName" = 'Guest'
                  AND NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."RoleId" = r."RoleId");
                """);
        }
    }
}
