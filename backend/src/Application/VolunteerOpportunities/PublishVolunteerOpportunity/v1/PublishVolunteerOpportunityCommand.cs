using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.PublishVolunteerOpportunity.v1;

public sealed record PublishVolunteerOpportunityCommand(
	Guid OpportunityId,
	UserId RequestingUserId)
	: ICommand<bool>;
