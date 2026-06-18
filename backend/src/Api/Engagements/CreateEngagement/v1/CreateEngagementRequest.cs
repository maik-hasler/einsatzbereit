using System.ComponentModel.DataAnnotations;

namespace Api.Engagements.CreateEngagement.v1;

public sealed record CreateEngagementRequest(
	string Type,
	Guid? TimeSlotId,
	[MaxLength(500)] string? Message);
