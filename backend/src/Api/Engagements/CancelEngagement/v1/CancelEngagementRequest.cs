using System.ComponentModel.DataAnnotations;

namespace Api.Engagements.CancelEngagement.v1;

public sealed record CancelEngagementRequest([MaxLength(500)] string? Reason);
