using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.UnpublishVolunteerOpportunity.v1;

public sealed record UnpublishVolunteerOpportunityCommand(
	Guid OpportunityId,
	UserId RequestingUserId)
	: ICommand<bool>;
