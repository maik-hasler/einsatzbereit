using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

/// <summary>
/// The member-candidate search is reachable by anyone - any authenticated user
/// can self-create an organization and become its organizer - so it is the one
/// endpoint in the product that could turn into a realm-wide user directory.
/// Both guards against that are server-side: the minimum query length, and the
/// shape of <c>MemberCandidateDto</c> itself.
///
/// Moved down from `OrganizationTests` in #2148. The browser original asserted
/// the same two facts through the members tab, where the page can only fail to
/// print an email it was never handed - so it could not have caught the DTO
/// gaining an Email property, which is the drift actually worth guarding.
/// The rendering half (the "Enter at least 4 characters" hint) lives in
/// `frontend/src/pages/app/OrgMembersPage.test.tsx`.
/// </summary>
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

		// Empty, not an error: a prefix search that answered on one or two
		// characters would enumerate the realm.
		candidates.Should().BeEmpty();
	}

	[Test]
	public async Task SearchMemberCandidates_ShouldFindTheUser_WhenQueryReachesFourCharacters(
		CancellationToken cancellationToken)
	{
		// The positive half, which is what keeps the case above honest - without
		// it, an endpoint that returned nothing for every query would pass.
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

		// Asserted over the DTO's own surface rather than only over the values it
		// happens to hold: the guard is that there is no property to leak
		// through, so a future Email property fails here even if it were left
		// null for this particular user. `AdditionalProperties` is NSwag's
		// [JsonExtensionData] catch-all, present on every generated DTO.
		typeof(MemberCandidateDto).GetProperties()
			.Select(property => property.Name)
			.Should().BeEquivalentTo(
				"UserId", "Username", "FirstName", "LastName", "AdditionalProperties");

		// Which is also where an email the server started sending would land -
		// deserialization would not fail, it would just silently absorb it. So
		// the extension bag has to be empty too, or the check above proves
		// nothing about the wire.
		candidate.AdditionalProperties.Should().BeEmpty();

		// And nothing else on it smuggles the address through as a value.
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
