namespace Domain.VolunteerOpportunities;

// Shared by VolunteerOpportunity.EnsureValidPin (organizer-supplied PINs) and
// Infrastructure's RandomPinGenerator (auto-generated PINs), so both reject the
// same set of easy-to-guess values instead of maintaining two definitions of
// "trivial" (#1176). A 4-6 digit PIN already has a small combination space; a
// repeated digit, a run of consecutive digits, or one of a handful of
// well-known common PINs (DataGenetics' 2012 analysis of leaked 4-digit PINs)
// would let an attacker skip straight to the lockout threshold on the first
// few guesses.
public static class CheckInPinPolicy
{
	private static readonly HashSet<string> WellKnownWeakPins = new(StringComparer.Ordinal)
	{
		"1212", "6969", "1313", "2001", "1010", "1998", "1004", "2000", "1999", "2580", "0852",
	};

	public static bool IsTrivial(string pin) =>
		IsRepeatedDigit(pin) || IsConsecutiveRun(pin) || WellKnownWeakPins.Contains(pin);

	private static bool IsRepeatedDigit(string pin) =>
		pin.Distinct().Count() == 1;

	private static bool IsConsecutiveRun(string pin)
	{
		var ascending = true;
		var descending = true;

		for (var i = 1; i < pin.Length; i++)
		{
			var diff = pin[i] - pin[i - 1];
			ascending &= diff == 1;
			descending &= diff == -1;
		}

		return ascending || descending;
	}
}
