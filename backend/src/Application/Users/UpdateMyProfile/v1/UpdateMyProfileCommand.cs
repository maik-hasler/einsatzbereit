using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.UpdateMyProfile.v1;

public sealed record UpdateMyProfileCommand(
	UserId UserId,
	string? FirstName,
	string? LastName)
	: ICommand<bool>;
