namespace Api.Common.RateLimiting;

internal sealed class RateLimitingOptions
{
	public ReadOptions Read { get; init; } = new();
	public WriteOptions Write { get; init; } = new();

	internal sealed class ReadOptions
	{
		public int AuthenticatedPermitLimit { get; init; } = 200;
		public int AnonymousPermitLimit { get; init; } = 60;
		public int WindowSeconds { get; init; } = 60;
	}

	internal sealed class WriteOptions
	{
		public int PermitLimit { get; init; } = 100;
		public int WindowSeconds { get; init; } = 60;
	}
}
