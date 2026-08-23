using System.Security.Cryptography;
using Domain.VolunteerOpportunities;

namespace Infrastructure.VolunteerOpportunities;

internal sealed class RandomPinGenerator : IPinGenerator
{
	public string GeneratePin()
	{
		string pin;

		do
		{
			pin = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
		} while (CheckInPinPolicy.IsTrivial(pin));

		return pin;
	}
}
