using Application.Common.Messaging;
using Application.Engagements.Common;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.BulkConfirmEngagements.v1;

public sealed record BulkConfirmEngagementsCommand(
	VolunteerOpportunityId OpportunityId,
	IReadOnlyList<EngagementId> EngagementIds,
	UserId RequestingUserId)
	: ICommand<BulkEngagementActionResult>;
