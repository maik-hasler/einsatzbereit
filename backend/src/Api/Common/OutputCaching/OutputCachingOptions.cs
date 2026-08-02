namespace Api.Common.OutputCaching;

internal sealed class OutputCachingOptions
{
	public int LongPublicReadSeconds { get; init; } = 3600;
	public int ShortPublicReadSeconds { get; init; } = 60;
}
