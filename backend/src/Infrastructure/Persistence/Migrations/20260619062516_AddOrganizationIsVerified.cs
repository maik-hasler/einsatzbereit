using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddOrganizationIsVerified : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Idempotent on purpose: on a database that predates this migration the
			// is_verified column can already exist (created by a duplicate migration
			// that was later removed, leaving no __EFMigrationsHistory row for this
			// id), so a plain AddColumn crashes the backend on startup with "column
			// is_verified already exists". IF NOT EXISTS makes the re-apply a no-op
			// there while still creating the column on fresh databases (CI, a clean
			// local stack, a first run).
			migrationBuilder.Sql(
				"ALTER TABLE organization ADD COLUMN IF NOT EXISTS is_verified boolean NOT NULL DEFAULT FALSE;");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"ALTER TABLE organization DROP COLUMN IF EXISTS is_verified;");
		}
	}
}
