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
		var (opportunity, title) = await CreateReportedOpportunityAsync(cancellationToken);
		var admin = await CreateAuthenticatedClientAsync("admin", "admin123");

		var page = await admin.ListFlaggedTargetsAsync(1, 20, null, cancellationToken);

		var flagged = page.Items.Should().ContainSingle(item => item.TargetTitle == title).Which;
		flagged.TargetType.Should().Be("VolunteerOpportunity");
		flagged.IsDeleted.Should().BeFalse();
		flagged.TargetId.Should().Be(opportunity.Id);
		flagged.OpenReportCount.Should().Be(1);
	}

	[Test]
	public async Task ReportVolunteerOpportunity_ShouldReturn409_WhenAPriorReportByTheSameReporterWasDismissed(
		CancellationToken cancellationToken)
	{
		var (opportunity, _) = await CreateReportedOpportunityAsync(cancellationToken);

		var admin = await CreateAuthenticatedClientAsync("admin", "admin123");
		var history = await admin.GetReportHistoryForTargetAsync("VolunteerOpportunity", opportunity.Id, cancellationToken);
		await admin.DismissReportAsync(history.Single().Id, cancellationToken);

		var reporter = await CreateAuthenticatedClientAsync("vera", "vera123");
		var act = () => reporter.ReportVolunteerOpportunityAsync(
			opportunity.Id,
			new ReportVolunteerOpportunityRequest { Reason = "Spam", Details = "Re-filed after dismissal." },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(409);
	}

	// The queue is outstanding work, not a permanent ledger. Without the open-only default a
	// fully-dismissed target stayed listed at "0 open flags" forever and the "all caught up"
	// empty state became unreachable after the very first report (#2326).
	[Test]
	public async Task ListFlaggedTargets_ShouldDropTheTarget_AfterItsOnlyReportIsDismissed(
		CancellationToken cancellationToken)
	{
		var (opportunity, title) = await CreateReportedOpportunityAsync(cancellationToken);
		var admin = await CreateAuthenticatedClientAsync("admin", "admin123");

		var history = await admin.GetReportHistoryForTargetAsync("VolunteerOpportunity", opportunity.Id, cancellationToken);
		await admin.DismissReportAsync(history.Single().Id, cancellationToken);

		var openOnly = await admin.ListFlaggedTargetsAsync(1, 20, null, cancellationToken);
		openOnly.Items.Should().NotContain(item => item.TargetId == opportunity.Id);

		var withResolved = await admin.ListFlaggedTargetsAsync(1, 20, true, cancellationToken);
		var resolved = withResolved.Items.Should().ContainSingle(item => item.TargetId == opportunity.Id).Which;
		resolved.TargetTitle.Should().Be(title);
		resolved.OpenReportCount.Should().Be(0);
		resolved.TotalReportCount.Should().Be(1);
	}

	// Hiding the target was audited from the start; dismissing was not, which left the log a
	// partial account of moderation (#2326).
	[Test]
	public async Task DismissReport_ShouldWriteAuditLogEntry_AgainstTheReportedTarget(
		CancellationToken cancellationToken)
	{
		var (opportunity, _) = await CreateReportedOpportunityAsync(cancellationToken);
		var admin = await CreateAuthenticatedClientAsync("admin", "admin123");
		var adminProfile = await admin.GetUserProfileAsync(cancellationToken);

		var history = await admin.GetReportHistoryForTargetAsync("VolunteerOpportunity", opportunity.Id, cancellationToken);
		await admin.DismissReportAsync(history.Single().Id, cancellationToken);

		var auditLog = await admin.ListAuditLogsAsync(
			1, 20, "ReportDismissed", cancellationToken: cancellationToken);

		auditLog.Items.Should().ContainSingle(a => a.SubjectId == opportunity.Id)
			.Which.Should().BeEquivalentTo(new
			{
				ActorUserId = adminProfile.Id,
				ActionType = "ReportDismissed",
				SubjectType = "VolunteerOpportunity",
				SubjectId = opportunity.Id,
			});
	}

	private async Task<(CreateVolunteerOpportunityResponse Opportunity, string Title)> CreateReportedOpportunityAsync(
		CancellationToken cancellationToken)
	{
		var organizer = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var organization = await organizer.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Admin Reports Org {suffix}" }, cancellationToken);

		var title = $"Reported Opportunity {suffix}";
		var opportunity = await organizer.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = title,
				DescriptionDe = "Regression coverage for the moderation queue.",
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
			new ReportVolunteerOpportunityRequest { Reason = "Spam", Details = $"Regression coverage {suffix}." },
			cancellationToken);

		return (opportunity, title);
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
