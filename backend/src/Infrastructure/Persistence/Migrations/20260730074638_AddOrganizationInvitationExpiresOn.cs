using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddOrganizationInvitationExpiresOn : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "expires_on",
				table: "organization_invitation",
				type: "timestamp with time zone",
				nullable: false,
				defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

			// Backfill: existing rows predate the expiry feature (#1053), so give
			// each one the same 14-day window a brand new invitation gets,
			// counted from when it was actually created rather than from the
			// sentinel default above.
			migrationBuilder.Sql("""
				UPDATE organization_invitation
				SET expires_on = created_on + interval '14 days';
				""");

			// The AddColumn above needed some default to satisfy the NOT NULL
			// constraint while every existing row still had it, but the model
			// itself declares no default (OrganizationInvitationConfiguration
			// never calls .HasDefaultValue) - every write path always supplies an
			// explicit ExpiresOn. Drop the sentinel default now that the backfill
			// has replaced it everywhere, so the live schema matches the model.
			migrationBuilder.AlterColumn<DateTimeOffset>(
				name: "expires_on",
				table: "organization_invitation",
				type: "timestamp with time zone",
				nullable: false,
				oldClrType: typeof(DateTimeOffset),
				oldType: "timestamp with time zone",
				oldDefaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "expires_on",
				table: "organization_invitation");
		}
	}
}
