using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationIdToBedarf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "organisation_id",
                table: "bedarfe",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "organisation_id",
                table: "bedarfe");
        }
    }
}
