using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class MakeEngagementVolunteerIdNullable : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<double>(
				name: "address_latitude",
				table: "organization",
				type: "double precision",
				nullable: true);

			migrationBuilder.AddColumn<double>(
				name: "address_longitude",
				table: "organization",
				type: "double precision",
				nullable: true);

			migrationBuilder.AlterColumn<Guid>(
				name: "volunteer_id",
				table: "engagement",
				type: "uuid",
				nullable: true,
				oldClrType: typeof(Guid),
				oldType: "uuid");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "address_latitude",
				table: "organization");

			migrationBuilder.DropColumn(
				name: "address_longitude",
				table: "organization");

			migrationBuilder.AlterColumn<Guid>(
				name: "volunteer_id",
				table: "engagement",
				type: "uuid",
				nullable: false,
				defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
				oldClrType: typeof(Guid),
				oldType: "uuid",
				oldNullable: true);
		}
	}
}
