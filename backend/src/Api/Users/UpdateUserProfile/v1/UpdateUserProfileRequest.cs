namespace Api.Users.UpdateUserProfile.v1;

public sealed record UpdateUserProfileRequest(
	string? FirstName = null,
	string? LastName = null);
