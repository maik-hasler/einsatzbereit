using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.UploadOpportunityBanner.v1;

public sealed record UploadOpportunityBannerCommand(
	Guid OpportunityId,
	byte[] Content,
	string ContentType,
	UserId RequestingUserId)
	: ICommand<bool>;
