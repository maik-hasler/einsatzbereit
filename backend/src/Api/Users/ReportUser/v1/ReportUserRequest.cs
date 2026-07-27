using System.ComponentModel.DataAnnotations;

namespace Api.Users.ReportUser.v1;

public sealed record ReportUserRequest(
	string Reason,
	[MaxLength(1000)] string? Details);
