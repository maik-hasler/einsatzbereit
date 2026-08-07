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

	// Count-independent phrasing for the single-match case (#1731) - selected at the
	// send site (SearchAlertMatchesFoundNotificationHandler) instead of interpolating
	// {Count} into SearchAlertNewMatches, which reads as "1 new opportunities".
	SearchAlertNewMatchesSingle,

	// Body-only fragment appended to every outgoing notification email via
	// EmailFooter.Append - same "render separately, splice in" pattern as
	// EngagementCancelledReasonSuffix above.
	EmailFooter,
}
