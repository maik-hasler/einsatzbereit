using System.Security.Cryptography;
using Domain.VolunteerOpportunities;

namespace Infrastructure.VolunteerOpportunities;

internal sealed class RandomPinGenerator : IPinGenerator
{
	// RandomNumberGenerator (not Random.Shared, a non-cryptographic xoshiro
	// PRNG) over a 6-digit space (#1176) - 1,000,000 combinations instead of
	// the previous 9,000. Looping past a CheckInPinPolicy-trivial draw is
	// cheap: fewer than 30 of the 1,000,000 possible values are trivial, so
	// this essentially never iterates more than once.
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
