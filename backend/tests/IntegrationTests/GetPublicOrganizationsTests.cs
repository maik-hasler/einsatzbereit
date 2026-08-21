using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

/// <summary>
/// The public organization directory (<c>GET /v1/organizations/directory</c>),
/// whose only non-trivial field is <c>OpenOpportunityCount</c>: it has to count
/// Published opportunities and nothing else, so a card reads as active on the
/// strength of work a volunteer can actually sign up for.
///
/// Moved down from `OrganizationTests` in #2148. The browser original drove the
/// four-step creation wizard to publish a single opportunity and then searched
/// the directory page for the string "1 open opportunity" - the card only ever
/// prints the number the API hands it through
/// `t("organizationsPage.openOpportunities", { count })`, so the count itself is
/// the whole subject. Nothing covered this endpoint at integration level;
/// `ListOrganizationsFilterTests` covers the admin list endpoint instead.
/// </summary>
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class GetPublicOrganizationsTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetPublicOrganizations_ShouldCountOnlyPublishedOpportunities(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var organizationId = await CreateOrganizationAsync(client, "OpenCount", cancellationToken);

		await PublishOpportunityAsync(client, organizationId, "Published one", cancellationToken);

		// A draft alongside it, which is what makes the count meaningful rather
		// than just "the number of opportunities this org has".
		await client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			TitleDe = "Draft, never published",
			DescriptionDe = "Must not be counted as open.",
			OrganizationId = organizationId,
			IsRemote = true,
			Occurrence = "OneTime",
			ParticipationType = "IndividualContact",
			CheckInMethod = "None",
			ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
			IsDraft = true,
		}, cancellationToken);

		var directory = new EinsatzbereitApi(fixture.CreateHttpClient());

		var page = await directory.GetPublicOrganizationsAsync(
			1, 10, search: "OpenCount", cancellationToken: cancellationToken);

		page.Items.Should().ContainSingle()
			.Which.OpenOpportunityCount.Should().Be(1);
	}

	[Test]
	public async Task GetPublicOrganizations_ShouldReportZeroOpenOpportunities_ForABareOrganization(
		CancellationToken cancellationToken)
	{
		// The other side of the same field. Without it, a count that was always
		// 1 would satisfy the case above.
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		await CreateOrganizationAsync(client, "BareOrg", cancellationToken);

		var directory = new EinsatzbereitApi(fixture.CreateHttpClient());

		var page = await directory.GetPublicOrganizationsAsync(
			1, 10, search: "BareOrg", cancellationToken: cancellationToken);

		page.Items.Should().ContainSingle()
			.Which.OpenOpportunityCount.Should().Be(0);
	}

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, string namePrefix, CancellationToken cancellationToken)
	{
		var organization = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"{namePrefix} {Guid.NewGuid():N}" },
			cancellationToken);
		return organization.Id.Value;
	}

	/// <summary>
	/// IndividualContact rather than ScheduledSlots, exactly as the browser
	/// original chose: it is the one participation type that can publish with no
	/// time slots, which keeps this about the directory count rather than about
	/// the slot-creation flow.
	/// </summary>
	private static async Task PublishOpportunityAsync(
		EinsatzbereitApi client, Guid organizationId, string title, CancellationToken cancellationToken)
	{
		var opportunity = await client.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = title,
				DescriptionDe = "Coverage for the directory's open-opportunity count.",
				OrganizationId = organizationId,
				IsRemote = true,
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = "None",
				ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
				IsDraft = true,
			}, cancellationToken);

		await client.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);
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
