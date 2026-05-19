namespace Api.Users.UpdateUserProfile.v1;

public sealed record UpdateUserProfileRequest(
	string? FirstName = null,
	string? LastName = null,
	string? Bio = null,
	IReadOnlyList<string>? Skills = null,
	IReadOnlyList<string>? Languages = null,
	string? PreferredContact = null);
