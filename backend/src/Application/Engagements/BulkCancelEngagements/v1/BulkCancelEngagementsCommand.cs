using Application.Common.Messaging;
using Application.Engagements.Common;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.BulkCancelEngagements.v1;

public sealed record BulkCancelEngagementsCommand(
	VolunteerOpportunityId OpportunityId,
	IReadOnlyList<EngagementId> EngagementIds,
	UserId RequestingUserId,
	string? Reason = null)
	: ICommand<BulkEngagementActionResult>;
