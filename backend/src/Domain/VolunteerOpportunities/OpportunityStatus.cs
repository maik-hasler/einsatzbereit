namespace Domain.VolunteerOpportunities;

public enum OpportunityStatus
{
	Draft,
	Published,

	// Taken off public listing by the organizer but reversible - Publish()
	// can bring it back. Active engagements are cascade-cancelled and
	// notified, since the sign-ups they made no longer have a live listing
	// behind them (einsatzbereit#1038).
	Unpublished,

	// Terminal - unlike Unpublished, there is no way back to Published.
	// Active engagements are cascade-cancelled and notified the same way.
	Cancelled,
}
