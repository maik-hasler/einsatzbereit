namespace Api.Users.UpdateUserProfile.v1;

public sealed record UpdateUserProfileRequest(
	string? FirstName,
	string? LastName);
