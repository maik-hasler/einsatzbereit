namespace Api.Common.RateLimiting;

internal sealed class RateLimitingOptions
{
	public ReadOptions Read { get; init; } = new();
	public WriteOptions Write { get; init; } = new();
	public MapTilesOptions MapTiles { get; init; } = new();

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

	// A single pan/zoom can burst through dozens of tile requests, so this
	// needs its own, more generous budget - otherwise it shares (and can
	// exhaust) the anonymous Read bucket that the rest of the browse page's
	// content calls depend on (#2208).
	internal sealed class MapTilesOptions
	{
		public int PermitLimit { get; init; } = 300;
		public int WindowSeconds { get; init; } = 60;
	}
}
