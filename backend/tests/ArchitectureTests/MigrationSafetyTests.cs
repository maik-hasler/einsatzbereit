using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace ArchitectureTests;

// #1194: a migration that narrows a previously-unbounded (text) string column
// to a bounded varchar(n) fails outright if any existing row already exceeds
// n - and because Database__MigrateOnStartup retries and rethrows on every
// boot (ApplicationDbContextInitializer.MigrateAsync), one such row puts the
// backend in a permanent crash loop that only a manual SQL fix can clear.
// This enforces that every future narrowing migration truncates/validates the
// existing data first, the same way the ones already in the repo now do.
public sealed class MigrationSafetyTests
{
	[Test]
	public void NarrowingMigrations_ShouldPrecedeAlterColumnWithATruncationPreCheck()
	{
		var migrationsDir = FindMigrationsDirectory();

		var violations = Directory.GetFiles(migrationsDir, "*.cs")
			.Where(f => !f.EndsWith(".Designer.cs", StringComparison.Ordinal)
				&& !Path.GetFileName(f).Equals("ApplicationDbContextModelSnapshot.cs", StringComparison.Ordinal))
			.SelectMany(file => FindViolationsInFile(file))
			.ToList();

		violations.Should().BeEmpty(
			"a narrowing AlterColumn<string> (unbounded -> maxLength) must be preceded in the same " +
			"migration's Up() by a migrationBuilder.Sql(...) pre-check that truncates/validates existing " +
			"data - see AddVolunteerOpportunityTitleDescriptionMaxLength for the pattern. Violations:\n" +
			string.Join("\n", violations));
	}

	private static IEnumerable<string> FindViolationsInFile(string file)
	{
		var upMethod = ExtractUpMethod(File.ReadAllText(file));
		if (upMethod is null)
			yield break;

		// A call narrows an unbounded column when it introduces a maxLength
		// with no corresponding oldMaxLength (EF only emits oldMaxLength when
		// the previous type was itself bounded) - the unbounded -> bounded
		// case is exactly what crash-loops MigrateOnStartup on overlong
		// pre-existing data. A bounded -> smaller-bounded narrowing (both
		// maxLength and oldMaxLength present) is rarer and left to review by
		// eye rather than this check.
		foreach (Match call in Regex.Matches(upMethod, @"migrationBuilder\.AlterColumn<string>\s*\((?<args>[^;]*?)\);", RegexOptions.Singleline))
		{
			var args = call.Groups["args"].Value;
			var hasMaxLength = Regex.IsMatch(args, @"maxLength:\s*\d+");
			var hasOldMaxLength = Regex.IsMatch(args, @"oldMaxLength:\s*\d+");

			if (!hasMaxLength || hasOldMaxLength)
				continue;

			var precedingUpText = upMethod[..call.Index];
			if (!precedingUpText.Contains("migrationBuilder.Sql(", StringComparison.Ordinal))
				yield return $"{Path.GetFileName(file)}: narrows a column with no preceding data pre-check.";
		}
	}

	private static string? ExtractUpMethod(string content)
	{
		var upStart = content.IndexOf("protected override void Up(", StringComparison.Ordinal);
		if (upStart < 0)
			return null;

		var downStart = content.IndexOf("protected override void Down(", StringComparison.Ordinal);
		var upEnd = downStart > upStart ? downStart : content.Length;

		return content[upStart..upEnd];
	}

	private static string FindMigrationsDirectory()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Einsatzbereit.slnx")))
			dir = dir.Parent;

		if (dir is null)
			throw new InvalidOperationException($"Could not locate Einsatzbereit.slnx above {AppContext.BaseDirectory}.");

		var migrationsDir = Path.Combine(dir.FullName, "src", "Infrastructure", "Persistence", "Migrations");
		if (!Directory.Exists(migrationsDir))
			throw new InvalidOperationException($"Migrations directory not found at {migrationsDir}.");

		return migrationsDir;
	}
}
