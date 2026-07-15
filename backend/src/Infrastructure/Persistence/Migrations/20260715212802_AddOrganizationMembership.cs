using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddOrganizationMembership : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "organization_membership",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					organization_id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					role = table.Column<string>(type: "text", nullable: false),
					created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_organization_membership", x => x.id);
				});

			migrationBuilder.CreateIndex(
				name: "ix_organization_membership_organization_id_user_id",
				table: "organization_membership",
				columns: new[] { "organization_id", "user_id" },
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "organization_membership");
		}
	}
}
