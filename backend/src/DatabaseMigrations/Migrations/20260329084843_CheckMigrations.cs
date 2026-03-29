using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseMigrations.Migrations
{
    /// <inheritdoc />
    public partial class CheckMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_organisationen",
                table: "organisationen");

            migrationBuilder.DropPrimaryKey(
                name: "pk_bedarfe",
                table: "bedarfe");

            migrationBuilder.RenameTable(
                name: "organisationen",
                newName: "organisation");

            migrationBuilder.RenameTable(
                name: "bedarfe",
                newName: "bedarf");

            migrationBuilder.RenameIndex(
                name: "ix_organisationen_keycloak_id",
                table: "organisation",
                newName: "ix_organisation_keycloak_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_organisation",
                table: "organisation",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_bedarf",
                table: "bedarf",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_organisation",
                table: "organisation");

            migrationBuilder.DropPrimaryKey(
                name: "pk_bedarf",
                table: "bedarf");

            migrationBuilder.RenameTable(
                name: "organisation",
                newName: "organisationen");

            migrationBuilder.RenameTable(
                name: "bedarf",
                newName: "bedarfe");

            migrationBuilder.RenameIndex(
                name: "ix_organisation_keycloak_id",
                table: "organisationen",
                newName: "ix_organisationen_keycloak_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_organisationen",
                table: "organisationen",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_bedarfe",
                table: "bedarfe",
                column: "id");
        }
    }
}
