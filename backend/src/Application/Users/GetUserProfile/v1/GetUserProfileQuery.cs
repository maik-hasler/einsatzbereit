using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.GetUserProfile.v1;

// RequestLanguage seeds PreferredLanguage the first time a User row is
// created for this account (e.g. the frontend's UI language on first login) -
// it is never applied to an already-existing row, so a later request from a
// different browser/session can't silently override a user's own choice.
public sealed record GetUserProfileQuery(UserId UserId, string? RequestLanguage)
	: IQuery<MyProfileResponse>;

public sealed record MyProfileResponse(
	Guid Id,
	string Username,
	string? FirstName,
	string? LastName,
	string Email,
	string? AvatarUrl,
	string? Bio,
	string? Phone,
	IReadOnlyList<string> Skills,
	IReadOnlyList<string> Languages,
	string? PreferredContact,
	string PreferredLanguage);
