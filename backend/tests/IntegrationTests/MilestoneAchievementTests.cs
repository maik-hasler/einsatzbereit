using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

/// <summary>
/// Moved down from <c>VisualTests</c> in einsatzbereit#2148: this test never
/// opened a browser either, so it was paying for Playwright and a frontend it
/// never touched.
///
/// Regression for #668: milestone achievements ("First Step" / "Dedicated" /
/// "Century") were awarded by matching a volunteer's live "currently
/// confirmed" engagement count against an exact threshold. That count is not
/// monotonic - it drops whenever a confirmed engagement is cancelled,
/// including when an organizer deletes the opportunity behind it, which is a
/// normal supported action. A volunteer whose live count was pulled back down
/// this way could permanently skip past a threshold and never land on it
/// again.
///
/// The fix tracks a separate, monotonically-increasing lifetime counter
/// (<c>UserStreak.TotalConfirmedEngagements</c>, never decremented) and
/// evaluates milestones with <c>&gt;=</c> against it.
///
/// Needs a volunteer with a guaranteed-clean history so a 4-to-5 crossing can
/// be isolated - the seeded vera/olaf accounts accumulate real usage across a
/// session and cannot answer that (see #668's own note on why a live repro
/// against them was not practical).
/// </summary>
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class MilestoneAchievementTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task DedicatedBadge_IsAwarded_OnFifthConfirmation_EvenAfterAnEarlierConfirmedOpportunityWasDeleted(
		CancellationToken cancellationToken)
	{
		var (_, username, password) = await fixture.CreateEphemeralUserAsync(cancellationToken);
		var volunteerClient = await CreateAuthenticatedClientAsync(username, password);
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var engagementIds = new List<Guid>();
		var opportunityIds = new List<Guid>();
		for (var i = 0; i < 5; i++)
		{
			var opportunityId = await CreateIndividualContactOpportunityAsync(
				olafClient, $"Milestone668-{i}", cancellationToken);
			opportunityIds.Add(opportunityId);

			var engagement = await volunteerClient.CreateEngagementAsync(
				opportunityId,
				new CreateEngagementRequest { Message = "Please let me help." },
				cancellationToken);
			engagementIds.Add(engagement.Id);
		}

		// Confirm the first four - lifetime counter reaches 4, no badge yet.
		for (var i = 0; i < 4; i++)
			await olafClient.ConfirmEngagementAsync(engagementIds[i], cancellationToken);

		// Delete one of the already-confirmed opportunities. That cancels its
		// engagement (DeleteVolunteerOpportunityCommandHandler), pulling the
		// volunteer's *live* confirmed count back down to 3 - and must not
		// touch the lifetime counter.
		await olafClient.DeleteVolunteerOpportunityAsync(opportunityIds[0], cancellationToken);

		// Confirm the fifth: the live count is only 4 now (3 remaining plus
		// this one), but the lifetime counter reaches 5 and must award it.
		await olafClient.ConfirmEngagementAsync(engagementIds[4], cancellationToken);

		var achievements = await volunteerClient.GetMyAchievementsAsync(cancellationToken);

		achievements.Select(a => a.Name).Should().Contain("Dedicated",
			"5 lifetime confirmations must award the Dedicated badge even though an "
			+ "earlier deletion left the live confirmed count at only 4");
	}

	private static async Task<Guid> CreateIndividualContactOpportunityAsync(
		EinsatzbereitApi olafClient, string label, CancellationToken cancellationToken)
	{
		var suffix = Guid.NewGuid().ToString("N");

		// A fresh organization per opportunity, matching what the Playwright
		// original did: the seeded organizations are mutated by other tests in
		// the same session.
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Milestone668 Org {suffix}" }, cancellationToken);

		var opportunity = await olafClient.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = $"Milestone668 {label} {suffix}",
				DescriptionDe = "Created by MilestoneAchievementTests",
				OrganizationId = org.Id.Value,
				IsRemote = true,
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = "None",
				ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
			},
			cancellationToken);

		return opportunity.Id;
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
}
