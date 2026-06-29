using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.DeleteOpportunityBanner.v1;

public sealed record DeleteOpportunityBannerCommand(
	Guid OpportunityId,
	UserId RequestingUserId)
	: ICommand<bool>;
