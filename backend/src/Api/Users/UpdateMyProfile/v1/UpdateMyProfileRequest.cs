namespace Api.Users.UpdateMyProfile.v1;

public sealed record UpdateMyProfileRequest(
	string? FirstName,
	string? LastName);
