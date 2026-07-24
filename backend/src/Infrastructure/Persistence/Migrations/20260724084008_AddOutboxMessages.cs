using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddOutboxMessages : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "outbox_message",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					type = table.Column<string>(type: "text", nullable: false),
					content = table.Column<string>(type: "text", nullable: false),
					occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					error = table.Column<string>(type: "text", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_outbox_message", x => x.id);
				});

			migrationBuilder.CreateIndex(
				name: "ix_outbox_message_processed_on_utc",
				table: "outbox_message",
				column: "processed_on_utc");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "outbox_message");
		}
	}
}
