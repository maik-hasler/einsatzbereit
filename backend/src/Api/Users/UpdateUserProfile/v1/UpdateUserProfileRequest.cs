using System.ComponentModel.DataAnnotations;

namespace Api.Users.UpdateUserProfile.v1;

public sealed record UpdateUserProfileRequest(
	[MaxLength(100)] string? FirstName = null,
	[MaxLength(100)] string? LastName = null,
	[MaxLength(1000)] string? Bio = null,
	[MaxLength(30)] string? Phone = null,
	IReadOnlyList<string>? Skills = null,
	IReadOnlyList<string>? Languages = null,
	[MaxLength(200)] string? PreferredContact = null);
