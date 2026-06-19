using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.GetPublicUserProfile.v1;

public sealed record GetPublicUserProfileQuery(UserId UserId)
	: IQuery<PublicUserProfileResponse?>;
