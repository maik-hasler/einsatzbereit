using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddUserNotificationPreferences : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<bool>(
				name: "notify_on_engagement_cancelled",
				table: "user",
				type: "boolean",
				nullable: false,
				defaultValue: true);

			migrationBuilder.AddColumn<bool>(
				name: "notify_on_engagement_confirmed",
				table: "user",
				type: "boolean",
				nullable: false,
				defaultValue: true);

			migrationBuilder.AddColumn<bool>(
				name: "notify_on_engagement_reminder",
				table: "user",
				type: "boolean",
				nullable: false,
				defaultValue: true);

			migrationBuilder.AddColumn<bool>(
				name: "notify_on_new_sign_up",
				table: "user",
				type: "boolean",
				nullable: false,
				defaultValue: true);

			migrationBuilder.AddColumn<bool>(
				name: "notify_on_withdrawal",
				table: "user",
				type: "boolean",
				nullable: false,
				defaultValue: true);

			migrationBuilder.AddColumn<Guid>(
				name: "unsubscribe_token",
				table: "user",
				type: "uuid",
				nullable: false,
				defaultValueSql: "gen_random_uuid()");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "notify_on_engagement_cancelled",
				table: "user");

			migrationBuilder.DropColumn(
				name: "notify_on_engagement_confirmed",
				table: "user");

			migrationBuilder.DropColumn(
				name: "notify_on_engagement_reminder",
				table: "user");

			migrationBuilder.DropColumn(
				name: "notify_on_new_sign_up",
				table: "user");

			migrationBuilder.DropColumn(
				name: "notify_on_withdrawal",
				table: "user");

			migrationBuilder.DropColumn(
				name: "unsubscribe_token",
				table: "user");
		}
	}
}
