using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

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
