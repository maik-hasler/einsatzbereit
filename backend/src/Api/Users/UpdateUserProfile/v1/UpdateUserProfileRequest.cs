using System.ComponentModel.DataAnnotations;

namespace Api.Users.UpdateUserProfile.v1;

public sealed record UpdateUserProfileRequest(
	[MaxLength(100)] string? FirstName = null,
	[MaxLength(100)] string? LastName = null,
	[MaxLength(1000)] string? Bio = null,
	[MaxLength(30)] string? Phone = null,
	IReadOnlyList<string>? Skills = null,
	IReadOnlyList<string>? Languages = null,
	[MaxLength(200)] string? PreferredContact = null,
	[MaxLength(5)] string? PreferredLanguage = null)
{
	// Skills/Languages carry no DataAnnotations attribute - MaxLengthAttribute
	// only checks a collection's item Count, not each item's string length, so
	// both caps are enforced manually in UpdateUserProfileEndpoint (#1173).
	public const int MaxSkillsCount = 50;

	public const int MaxSkillLength = 100;

	public const int MaxLanguagesCount = 20;

	public const int MaxLanguageLength = 50;
}
