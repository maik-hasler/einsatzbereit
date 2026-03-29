using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseMigrations.Migrations
{
    /// <inheritdoc />
    public partial class SecondCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_organisation_keycloak_id",
                table: "organisation");

            migrationBuilder.DropColumn(
                name: "keycloak_id",
                table: "organisation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "keycloak_id",
                table: "organisation",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_organisation_keycloak_id",
                table: "organisation",
                column: "keycloak_id",
                unique: true);
        }
    }
}
