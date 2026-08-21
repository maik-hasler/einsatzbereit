using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class SearchMemberCandidatesTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	[Arguments("v")]
	[Arguments("ve")]
	[Arguments("ver")]
	public async Task SearchMemberCandidates_ShouldReturnNothing_WhenQueryIsShorterThanFourCharacters(
		string query,
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var organizationId = await CreateOrganizationAsync(client, cancellationToken);

		var candidates = await client.SearchMemberCandidatesAsync(
			organizationId, query, cancellationToken);

		candidates.Should().BeEmpty();
	}

	[Test]
	public async Task SearchMemberCandidates_ShouldFindTheUser_WhenQueryReachesFourCharacters(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var organizationId = await CreateOrganizationAsync(client, cancellationToken);

		var candidates = await client.SearchMemberCandidatesAsync(
			organizationId, "vera", cancellationToken);

		candidates.Should().ContainSingle()
			.Which.Username.Should().Be("vera");
	}

	[Test]
	public async Task SearchMemberCandidates_ShouldNeverCarryAnEmailAddress(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var organizationId = await CreateOrganizationAsync(client, cancellationToken);

		var candidates = await client.SearchMemberCandidatesAsync(
			organizationId, "vera", cancellationToken);

		var candidate = candidates.Should().ContainSingle().Which;

		typeof(MemberCandidateDto).GetProperties()
			.Select(property => property.Name)
			.Should().BeEquivalentTo(
				"UserId", "Username", "FirstName", "LastName", "AdditionalProperties");

		candidate.AdditionalProperties.Should().BeEmpty();

		new[] { candidate.Username, candidate.FirstName, candidate.LastName }
			.Should().NotContain(value => value != null && value.Contains('@'));
	}

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, CancellationToken cancellationToken)
	{
		var organization = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Member search {Guid.NewGuid()}" },
			cancellationToken);
		return organization.Id.Value;
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}
}
