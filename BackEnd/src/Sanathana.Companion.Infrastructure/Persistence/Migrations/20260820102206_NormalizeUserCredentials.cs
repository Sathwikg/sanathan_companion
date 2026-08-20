using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Folds the two spellings of a credential into one and then makes the mobile number unique.
    /// </summary>
    /// <remarks>
    /// Email was compared case-sensitively, so Foo@x.com and foo@x.com were two accounts; the
    /// mobile number was a login credential with no constraint at all, stored exactly as typed with
    /// whatever punctuation and country code came with it. Both are rewritten here to the spelling
    /// CredentialNormalizer produces, and the index then makes the rule permanent.
    ///
    /// Rows that would collide are deliberately LEFT ALONE and the migration refuses to finish,
    /// naming them: choosing a winner between two real accounts is not a decision a migration
    /// should make quietly. Program.cs calls Migrate() outside its try/catch, so aborting here is a
    /// start-up failure rather than a skipped step. Run the pre-flight query in the release notes
    /// before deploying.
    /// </remarks>
    public partial class NormalizeUserCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The mobile rule is written once and used by all three statements below. Spelling it
            // out three times is how the first draft of this migration ended up with a backfill
            // that stripped a country code and a duplicate check that did not, which let a genuine
            // collision through and left one seeker unable to sign in with their own number.
            migrationBuilder.Sql(@"
                CREATE FUNCTION sc_normalize_mobile(value text) RETURNS text
                LANGUAGE sql IMMUTABLE AS $fn$
                    SELECT CASE
                             WHEN length(d) = 12 AND left(d, 2) = '91' THEN right(d, 10)
                             WHEN length(d) = 11 AND left(d, 1) = '0'  THEN right(d, 10)
                             ELSE d
                           END
                      FROM (SELECT regexp_replace(coalesce(value, ''), '[^0-9]', '', 'g')) AS s(d);
                $fn$;
            ");

            // Lower-case every address that does not thereby land on an existing one.
            migrationBuilder.Sql(@"
                UPDATE ""Users"" u
                   SET ""Email"" = lower(btrim(u.""Email""))
                 WHERE u.""Email"" <> lower(btrim(u.""Email""))
                   AND NOT EXISTS (
                       SELECT 1 FROM ""Users"" o
                        WHERE o.""UserId"" <> u.""UserId""
                          AND o.""Email"" = lower(btrim(u.""Email"")));
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Users"" u
                   SET ""MobileNumber"" = sc_normalize_mobile(u.""MobileNumber"")
                 WHERE sc_normalize_mobile(u.""MobileNumber"") <> u.""MobileNumber""
                   AND sc_normalize_mobile(u.""MobileNumber"") <> ''
                   AND NOT EXISTS (
                       SELECT 1 FROM ""Users"" o
                        WHERE o.""UserId"" <> u.""UserId""
                          AND o.""MobileNumber"" = sc_normalize_mobile(u.""MobileNumber""));
            ");

            // Anything still sharing a credential is a genuine duplicate between two accounts.
            // Stop, and say which — including rows the backfill above deliberately skipped.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE conflicting text;
                BEGIN
                    SELECT string_agg(detail, '; ') INTO conflicting FROM (
                        SELECT 'email ' || lower(btrim(""Email"")) AS detail
                          FROM ""Users""
                         GROUP BY lower(btrim(""Email""))
                        HAVING count(*) > 1
                        UNION ALL
                        SELECT 'mobile ' || sc_normalize_mobile(""MobileNumber"")
                          FROM ""Users""
                         GROUP BY sc_normalize_mobile(""MobileNumber"")
                        HAVING count(*) > 1
                    ) d;

                    IF conflicting IS NOT NULL THEN
                        RAISE EXCEPTION
                            'Two or more accounts share a credential once normalised (%). Merge or remove them, then deploy again.',
                            conflicting;
                    END IF;
                END $$;
            ");

            migrationBuilder.CreateIndex(
                name: "UX_Users_Mobile",
                table: "Users",
                column: "MobileNumber",
                unique: true);

            migrationBuilder.Sql(@"DROP FUNCTION sc_normalize_mobile(text);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the constraint comes back. The original casing and the punctuation people typed
            // are not recoverable, and inventing them would be worse than leaving them normalised.
            migrationBuilder.DropIndex(
                name: "UX_Users_Mobile",
                table: "Users");
        }
    }
}
