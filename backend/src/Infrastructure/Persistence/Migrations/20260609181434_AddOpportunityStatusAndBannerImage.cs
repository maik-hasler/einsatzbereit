using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddOpportunityStatusAndBannerImage : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<byte[]>(
				name: "banner_image",
				table: "volunteer_opportunity",
				type: "bytea",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "banner_image_content_type",
				table: "volunteer_opportunity",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "status",
				table: "volunteer_opportunity",
				type: "text",
				nullable: false,
				defaultValue: "Published");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "banner_image",
				table: "volunteer_opportunity");

			migrationBuilder.DropColumn(
				name: "banner_image_content_type",
				table: "volunteer_opportunity");

			migrationBuilder.DropColumn(
				name: "status",
				table: "volunteer_opportunity");
		}
	}
}
