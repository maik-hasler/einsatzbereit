namespace Application.Common.Email;

public enum EmailTemplateKind
{
	EngagementRequestReceived,
	EngagementWaitlisted,
	EngagementSignupNotifyOrganizer,
	EngagementConfirmed,
	EngagementCancelled,

	// Body-only fragment rendered separately and spliced into
	// EngagementCancelled's {ReasonBlock} placeholder when a reason was given -
	// keeps the optional-reason wording localized without a separate schema
	// just for template fragments.
	EngagementCancelledReasonSuffix,

	EngagementWithdrawnNotifyOrganizer,
	EngagementReminder,
	InvitationReceived,
	OpportunityUpdated,
	SearchAlertNewMatches,

	// Body-only fragment appended to every outgoing notification email via
	// EmailFooter.Append - same "render separately, splice in" pattern as
	// EngagementCancelledReasonSuffix above.
	EmailFooter,
}
