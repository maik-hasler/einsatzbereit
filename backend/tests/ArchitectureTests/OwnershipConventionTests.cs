using Application.Common.Messaging;
using AwesomeAssertions;
using NetArchTest.Rules;

namespace ArchitectureTests;

public sealed class OwnershipConventionTests
{
	// Handlers that carry RequestingUserId but are a deliberate exception to the
	// rule below - they check ownership of a per-user resource (a notification,
	// an engagement) rather than organization membership, so OwnershipGuard does
	// not apply. Add to this list only with a comment explaining why.
	private static readonly HashSet<string> AllowListedExceptions = new()
	{
		// Public read - RequestingUserId is optional and only personalizes the
		// response (e.g. whether the caller already applied); the opportunity
		// itself is publicly readable.
		"Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1.GetVolunteerOpportunityDetailsQueryHandler",
		// Ownership is checked against notification.RecipientId - a per-user
		// resource, not an organization.
		"Application.Notifications.MarkNotificationRead.v1.MarkNotificationReadCommandHandler",
		"Application.Notifications.MarkNotificationUnread.v1.MarkNotificationUnreadCommandHandler",
		"Application.Notifications.DeleteNotification.v1.DeleteNotificationCommandHandler",
		// Ownership is checked against engagement.VolunteerId - a per-user
		// resource, not an organization.
		"Application.Engagements.SubmitFeedback.v1.SubmitFeedbackCommandHandler",
		"Application.Engagements.UpdateFeedback.v1.UpdateFeedbackCommandHandler",
		"Application.Engagements.DeleteFeedback.v1.DeleteFeedbackCommandHandler",
		"Application.Engagements.CheckInWithPin.v1.CheckInWithPinCommandHandler",
	};

	[Test]
	public void OrgScopedHandlers_ShouldCallOwnershipGuard()
	{
		var handlersWithOwnershipGuard = Types
			.InAssembly(AssemblyAnchors.ApplicationLayer)
			.That()
			.HaveDependencyOn("Application.Common.Authorization.OwnershipGuard")
			.GetTypes()
			.Select(t => t.FullName)
			.ToHashSet();

		var violators = TypeDiscovery
			.GetImplementationPairs(AssemblyAnchors.ApplicationLayer, typeof(ICommandHandler<,>), typeof(IQueryHandler<,>))
			.Where(pair => pair.FirstTypeArg.GetProperty("RequestingUserId") is not null)
			.Select(pair => pair.Implementation)
			.Where(handler => !handlersWithOwnershipGuard.Contains(handler.FullName))
			.Where(handler => !AllowListedExceptions.Contains(handler.FullName!))
			.Select(handler => handler.FullName)
			.ToList();

		violators.Should().BeEmpty(
			"a handler whose command/query carries RequestingUserId is presumed to be organization-scoped " +
			"and must call OwnershipGuard.EnsureIsOrganizerAsync, or a new endpoint can ship with no tenant " +
			"check at all - if this is a deliberate exception (e.g. a per-user resource check instead of an " +
			"organization one), add it to OwnershipConventionTests.AllowListedExceptions with a comment " +
			"explaining why");
	}
}
