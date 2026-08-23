namespace Application.Common.Email;

public enum EmailTemplateKind
{
	EngagementRequestReceived,
	EngagementWaitlisted,
	EngagementSignupNotifyOrganizer,
	EngagementConfirmed,
	EngagementCancelled,

	EngagementCancelledReasonSuffix,

	EngagementWithdrawnNotifyOrganizer,
	EngagementReminder,
	InvitationReceived,
	OpportunityUpdated,

	EmailFooter,
}
