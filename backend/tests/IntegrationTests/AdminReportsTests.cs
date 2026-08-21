using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

/// <summary>
/// The platform-admin abuse-report list (<c>GET /v1/admin/reports</c>).
///
/// Moved down from `AdminReportsTests` in `VisualTests` in #2148. The
/// regression it guards is entirely backend: `AdminReportReadRepository`
/// filtered on <c>x.Id.Value</c> inside a still-queryable Where/Select, so
/// `ListFlaggedTargets` threw at EF query-translation time and the endpoint
/// answered 500. The browser half only observed the consequence - "Failed to
/// load reports." not rendering, and the row's Opportunity/Active cells being
/// the successful response printed out - which is the same fact one layer up,
/// at the cost of a Chromium context and an admin sign-in.
/// </summary>
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class AdminReportsTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task ListFlaggedTargets_ShouldReturnTheFlaggedOpportunity(
		CancellationToken cancellationToken)
	{
		var organizer = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var organization = await organizer.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Admin Reports Org {suffix}" }, cancellationToken);

		var title = $"Flagged Opportunity {suffix}";
		var opportunity = await organizer.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = title,
				DescriptionDe = "Regression coverage for the admin reports 500.",
				OrganizationId = organization.Id.Value,
				IsRemote = true,
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = "None",
				ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
				IsDraft = true,
			}, cancellationToken);
		await organizer.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var reporter = await CreateAuthenticatedClientAsync("vera", "vera123");
		await reporter.ReportVolunteerOpportunityAsync(
			opportunity.Id,
			new ReportVolunteerOpportunityRequest
			{
				Reason = "Spam",
				Details = $"Regression coverage {suffix}.",
			},
			cancellationToken);

		var admin = await CreateAuthenticatedClientAsync("admin", "admin123");

		// The call itself is most of the test: before the fix this threw at
		// query-translation time rather than returning a page at all.
		var page = await admin.ListFlaggedTargetsAsync(1, 20, cancellationToken);

		var flagged = page.Items.Should().ContainSingle(item => item.TargetTitle == title).Which;
		// The two cells the browser original read off the row.
		flagged.TargetType.Should().Be("Opportunity");
		flagged.IsDeleted.Should().BeFalse();
		flagged.TargetId.Should().Be(opportunity.Id);
		flagged.OpenReportCount.Should().Be(1);
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
