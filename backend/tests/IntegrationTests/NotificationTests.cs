using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class NotificationTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetMyNotifications_EngagementCreated_HasRelatedTitleAndDeepLink(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Notification Deep-Link Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken);

		var notification = olafNotifications.Single(n => n.Kind == "EngagementCreated");
		notification.RelatedTitle.Should().Be(opportunityTitle);
		notification.ActionUrl.Should()
			.StartWith($"/volunteer-opportunities/{opportunity.Id}/engagements");
	}

	[Test]
	public async Task GetMyNotifications_EngagementConfirmed_HasRelatedTitleAndMyEngagementsUrl(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Notification Confirm Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Ready to help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		var veraNotifications = await veraClient.GetMyNotificationsAsync(cancellationToken);

		var notification = veraNotifications.Single(n => n.Kind == "EngagementConfirmed");
		notification.RelatedTitle.Should().Be(opportunityTitle);
		notification.ActionUrl.Should().Be("/my-engagements");
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(
		string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, CancellationToken cancellationToken)
	{
		var org = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"NotifTestOrg_{Guid.NewGuid()}" },
			cancellationToken);
		return org.Id.Value;
	}

	private static async Task<CreateVolunteerOpportunityResponse> CreateOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, string title, CancellationToken cancellationToken)
	{
		return await client.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				Title = title,
				Description = "Integration test opportunity for notifications",
				OrganizationId = orgId,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = "None",
			},
			cancellationToken);
	}
}
