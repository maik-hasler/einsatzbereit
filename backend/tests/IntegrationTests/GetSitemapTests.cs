using System.Net.Http.Headers;
using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class GetSitemapTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetSitemap_ShouldReturnEmptyUrlset_WhenNothingPublished(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		var response = await httpClient.GetAsync(SitemapRoute(), cancellationToken);
		var xml = await response.Content.ReadAsStringAsync(cancellationToken);

		response.EnsureSuccessStatusCode();
		response.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");
		xml.Should().Contain("<urlset").And.NotContain("<url>");
	}

	[Test]
	public async Task GetSitemap_ShouldIncludeOrganization(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		using var httpClient = fixture.CreateHttpClient();
		var response = await httpClient.GetAsync(SitemapRoute(), cancellationToken);
		var xml = await response.Content.ReadAsStringAsync(cancellationToken);

		xml.Should().Contain($"/organizations/{orgId}</loc>");
	}

	[Test]
	public async Task GetSitemap_ShouldIncludePublishedOpportunity(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);
		var opportunity = await CreatePublishedOpportunityAsync(authenticatedClient, orgId, cancellationToken);

		using var httpClient = fixture.CreateHttpClient();
		var response = await httpClient.GetAsync(SitemapRoute(), cancellationToken);
		var xml = await response.Content.ReadAsStringAsync(cancellationToken);

		xml.Should().Contain($"/volunteer-opportunities/{opportunity.Id}</loc>");
	}

	[Test]
	public async Task GetSitemap_ShouldNotIncludeDraftOpportunity(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);
		var draft = await CreateDraftOpportunityAsync(authenticatedClient, orgId, cancellationToken);

		using var httpClient = fixture.CreateHttpClient();
		var response = await httpClient.GetAsync(SitemapRoute(), cancellationToken);
		var xml = await response.Content.ReadAsStringAsync(cancellationToken);

		xml.Should().NotContain($"/volunteer-opportunities/{draft.Id}</loc>");
	}

	// CreateTimeSlotAsync's domain validation rejects a past StartDateTime, so there is no
	// API path to create an already-expired slot - seeded directly through the aggregate,
	// the same way GetVolunteerOpportunitiesTests.CreateOpportunityWithExpiredTimeSlotAsync
	// does for the equivalent public-listing expiry filter this method reuses (#1086).
	[Test]
	public async Task GetSitemap_ShouldNotIncludeExpiredOpportunity(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);
		var expired = await CreateOpportunityWithExpiredTimeSlotAsync(authenticatedClient, orgId, cancellationToken);

		using var httpClient = fixture.CreateHttpClient();
		var response = await httpClient.GetAsync(SitemapRoute(), cancellationToken);
		var xml = await response.Content.ReadAsStringAsync(cancellationToken);

		xml.Should().NotContain($"/volunteer-opportunities/{expired.Id}</loc>");
	}

	// The output cache key is method + path + query string, and this route has no eviction
	// tag wired up (unlike the volunteer-opportunity listing's dedicated policy, #1543) - a
	// unique, otherwise-unused query string per call keeps every test in this class from
	// colliding on one shared cached response within ShortPublicReadSeconds.
	private static string SitemapRoute() => $"/v1/sitemap.xml?_={Guid.NewGuid()}";

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
	{
		var token = await fixture.GetAccessTokenAsync("olaf", "olaf123");
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, CancellationToken cancellationToken)
	{
		var uniqueName = $"Testorg_{Guid.NewGuid()}";
		var organization = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = uniqueName }, cancellationToken);
		return organization.Id.Value;
	}

	private static Task<CreateVolunteerOpportunityResponse> CreatePublishedOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken) =>
		client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			TitleDe = "Published opportunity",
			DescriptionDe = "IndividualContact - no time slots required to publish",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "IndividualContact",
			CheckInMethod = "None",
			ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
		}, cancellationToken);

	private static Task<CreateVolunteerOpportunityResponse> CreateDraftOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken) =>
		client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			TitleDe = "Draft opportunity",
			DescriptionDe = "Never published",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "IndividualContact",
			CheckInMethod = "None",
			ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
			IsDraft = true,
		}, cancellationToken);

	private async Task<CreateVolunteerOpportunityResponse> CreateOpportunityWithExpiredTimeSlotAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken)
	{
		var opportunity = await client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			TitleDe = "Expired opportunity",
			DescriptionDe = "Only has a time slot that already ended",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "ScheduledSlots",
			CheckInMethod = "None",
			IsDraft = true,
		}, cancellationToken);

		await using (var dbContext = fixture.CreateApplicationDbContext())
		{
			var opportunityId = VolunteerOpportunityId.Create(opportunity.Id).GetValueOrThrow();
			var aggregate = await dbContext.VolunteerOpportunities.FindAsync(opportunityId, cancellationToken)
				?? throw new InvalidOperationException($"Seeded opportunity '{opportunity.Id}' not found.");

			var start = DateTimeOffset.UtcNow.AddDays(-7);
			aggregate.AddTimeSlot(start, start.AddHours(2), maxParticipants: 10, now: start.AddDays(-1)).GetValueOrThrow();

			await dbContext.SaveChangesAsync(cancellationToken);
		}

		await client.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		return opportunity;
	}
}
