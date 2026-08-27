namespace Application.Common.Time;

public static class CanonicalTimeZone
{
	public const string Id = "Europe/Berlin";

	public static readonly TimeZoneInfo Value = TimeZoneInfo.FindSystemTimeZoneById(Id);

	public static TimeZoneInfo Resolve(string? ianaId)
	{
		if (string.IsNullOrWhiteSpace(ianaId))
			return Value;
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
		}
		catch
		{
			return Value;
		}
	}
}
