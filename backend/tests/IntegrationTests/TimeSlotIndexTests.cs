using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

// Regression coverage for #1213 - GetBlockingOpportunitiesForOrganizationAsync
// filters opportunities by whether they have a time slot with a future
// StartDateTime, now pushed into SQL rather than evaluated client-side after
// loading every opportunity's full time-slot collection. Asserts the index
// actually exists in Postgres rather than just that the EF Core model
// declares it, since a migration can drift from the model snapshot.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class TimeSlotIndexTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task Database_ShouldHaveIndex_OnStartDateTime()
	{
		var indexDef = await GetIndexDefinitionAsync("ix_time_slot_start_date_time");

		indexDef.Should().NotBeNull(
			"GetBlockingOpportunitiesForOrganizationAsync filters on time_slot.start_date_time");
		indexDef!.Should().Contain("start_date_time");
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
