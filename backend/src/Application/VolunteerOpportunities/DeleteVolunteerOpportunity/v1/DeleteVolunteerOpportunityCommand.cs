using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.DeleteVolunteerOpportunity.v1;

public sealed record DeleteVolunteerOpportunityCommand(Guid OpportunityId, UserId RequestingUserId, bool IsAdmin = false)
	: ICommand<bool>;
