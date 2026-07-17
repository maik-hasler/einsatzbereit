using Domain.VolunteerOpportunities;

namespace Infrastructure.VolunteerOpportunities;

internal sealed class RandomPinGenerator : IPinGenerator
{
	public string GeneratePin() =>
		Random.Shared.Next(1000, 10000).ToString("D4");
}
