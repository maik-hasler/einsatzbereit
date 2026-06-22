using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.SetOpportunityColor.v1;

public sealed record SetOpportunityColorCommand(
	Guid OpportunityId,
	string? Color,
	UserId RequestingUserId)
	: ICommand<bool>;
