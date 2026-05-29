using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
	public partial class MigrateSkillsLanguagesDelimiter : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"""
				UPDATE users
				SET skills = replace(skills, chr(10), '|'),
				    languages = replace(languages, chr(10), '|')
				WHERE skills LIKE '%' || chr(10) || '%'
				   OR languages LIKE '%' || chr(10) || '%';
				""");
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"""
				UPDATE users
				SET skills = replace(skills, '|', chr(10)),
				    languages = replace(languages, '|', chr(10))
				WHERE skills LIKE '%|%'
				   OR languages LIKE '%|%';
				""");
		}
	}
}
