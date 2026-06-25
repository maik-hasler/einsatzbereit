using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_invitation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_name = table.Column<string>(type: "text", nullable: false),
                    invitee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitee_name = table.Column<string>(type: "text", nullable: false),
                    invited_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organization_invitation", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_organization_invitation_invitee_id",
                table: "organization_invitation",
                column: "invitee_id");

            migrationBuilder.CreateIndex(
                name: "ix_organization_invitation_organization_id",
                table: "organization_invitation",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_invitation");
        }
    }
}
