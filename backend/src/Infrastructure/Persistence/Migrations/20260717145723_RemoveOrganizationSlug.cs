using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class RemoveOrganizationSlug : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_organization_slug",
				table: "organization");

			migrationBuilder.DropColumn(
				name: "slug",
				table: "organization");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "slug",
				table: "organization",
				type: "text",
				nullable: true);

			migrationBuilder.CreateIndex(
				name: "ix_organization_slug",
				table: "organization",
				column: "slug",
				unique: true);
		}
	}
}
