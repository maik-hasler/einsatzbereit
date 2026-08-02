using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

// Regression coverage for #1385 - the landing page's main query (GetPagedSummariesAsync)
// filters on Status and sorts by CreatedOn, and separately filters on Tags containment,
// with none of it indexed. Asserts the indexes actually exist in Postgres rather than
// just that the EF Core model declares them, since a migration can drift from the model
// snapshot (e.g. a hand-edited migration, or one generated against a stale snapshot).
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class VolunteerOpportunityIndexTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task Database_ShouldHaveCompositeIndex_OnStatusAndCreatedOn()
	{
		var indexDef = await GetIndexDefinitionAsync("ix_volunteer_opportunity_status_created_on");

		indexDef.Should().NotBeNull("the landing-page query filters on status and sorts by created_on");
		indexDef!.Should().Contain("status").And.Contain("created_on");
	}

	[Test]
	public async Task Database_ShouldHaveGinIndex_OnTags()
	{
		var indexDef = await GetIndexDefinitionAsync("ix_volunteer_opportunity_tags");

		indexDef.Should().NotBeNull("the landing-page query filters via Tags.Contains(filter.Tag)");
		indexDef!.Should().Contain("gin").And.Contain("tags");
	}

	[Test]
	public async Task Database_ShouldReturnNull_ForANonExistentIndexName()
	{
		var indexDef = await GetIndexDefinitionAsync("ix_does_not_exist_1385");

		// Sanity check that the lookup itself is discriminating (an unknown name
		// returns null) rather than the two assertions above passing vacuously
		// against a lookup that always finds something.
		indexDef.Should().BeNull();
	}

	private async Task<string?> GetIndexDefinitionAsync(string indexName)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
		await connection.OpenAsync();

		await using var cmd = new NpgsqlCommand(
			"SELECT indexdef FROM pg_indexes WHERE indexname = @indexName", connection);
		cmd.Parameters.AddWithValue("indexName", indexName);

		var result = await cmd.ExecuteScalarAsync();
		return result as string;
	}
}
