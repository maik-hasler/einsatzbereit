using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

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

		var page = await admin.ListFlaggedTargetsAsync(1, 20, cancellationToken);

		var flagged = page.Items.Should().ContainSingle(item => item.TargetTitle == title).Which;
		flagged.TargetType.Should().Be("VolunteerOpportunity");
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
