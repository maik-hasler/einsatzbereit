namespace Domain.VolunteerOpportunities;

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
