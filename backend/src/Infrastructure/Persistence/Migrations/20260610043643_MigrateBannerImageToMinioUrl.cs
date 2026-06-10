using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class MigrateBannerImageToMinioUrl : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "banner_image",
				table: "volunteer_opportunity");

			migrationBuilder.RenameColumn(
				name: "banner_image_content_type",
				table: "volunteer_opportunity",
				newName: "banner_image_url");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.RenameColumn(
				name: "banner_image_url",
				table: "volunteer_opportunity",
				newName: "banner_image_content_type");

			migrationBuilder.AddColumn<byte[]>(
				name: "banner_image",
				table: "volunteer_opportunity",
				type: "bytea",
				nullable: true);
		}
	}
}
