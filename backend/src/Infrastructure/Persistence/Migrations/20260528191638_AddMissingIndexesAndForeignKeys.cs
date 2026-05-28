using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingIndexesAndForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_volunteer_opportunity_organization_id",
                table: "volunteer_opportunity",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_engagement_opportunity_id",
                table: "engagement",
                column: "opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_engagement_time_slot_id",
                table: "engagement",
                column: "time_slot_id");

            migrationBuilder.CreateIndex(
                name: "ix_engagement_volunteer_id",
                table: "engagement",
                column: "volunteer_id");

            migrationBuilder.AddForeignKey(
                name: "fk_engagement_time_slot_time_slot_id",
                table: "engagement",
                column: "time_slot_id",
                principalTable: "time_slot",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_engagement_time_slot_time_slot_id",
                table: "engagement");

            migrationBuilder.DropIndex(
                name: "ix_volunteer_opportunity_organization_id",
                table: "volunteer_opportunity");

            migrationBuilder.DropIndex(
                name: "ix_engagement_opportunity_id",
                table: "engagement");

            migrationBuilder.DropIndex(
                name: "ix_engagement_time_slot_id",
                table: "engagement");

            migrationBuilder.DropIndex(
                name: "ix_engagement_volunteer_id",
                table: "engagement");
        }
    }
}
