namespace Domain.AuditLogs;

public enum AuditActionType
{
	UserPromotedToAdmin,
	UserDemotedFromAdmin,
	UserEnabled,
	UserDisabled,
	UserShadowDeleted,
	UserRestored,
	OrganizationShadowDeleted,
	OrganizationRestored,
	VolunteerOpportunityShadowDeleted,
	VolunteerOpportunityRestored,
	EngagementCancelled
}
