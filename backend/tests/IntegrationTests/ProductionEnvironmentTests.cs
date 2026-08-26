using AwesomeAssertions;

namespace IntegrationTests;

// Covers the non-Development branch of Program.cs that no other test runs (#2204):
// RequiredConfigurationValidator doesn't throw and the app boots successfully in
// Production, Database:MigrateOnStartup applies the schema instead of the
// Development branch's unconditional migrate+seed, and the HSTS header is added.
[ClassDataSource<ProductionEnvironmentFixture>(Shared = SharedType.PerClass)]
public class ProductionEnvironmentTests(ProductionEnvironmentFixture fixture)
{
	[Test]
	public async Task Alive_ShouldReturnHealthy_InProduction(CancellationToken cancellationToken)
	{
		using var client = fixture.CreateHttpClient();

		var response = await client.GetAsync("/alive", cancellationToken);

		response.IsSuccessStatusCode.Should().BeTrue();
	}

	[Test]
	public async Task Health_ShouldReturnHealthy_InProduction(CancellationToken cancellationToken)
	{
		using var client = fixture.CreateHttpClient();

		var response = await client.GetAsync("/health", cancellationToken);

		response.IsSuccessStatusCode.Should().BeTrue();
	}

	[Test]
	public async Task Health_ShouldCarryHstsHeader_InProduction(CancellationToken cancellationToken)
	{
		using var client = fixture.CreateHttpClient();

		var response = await client.GetAsync("/health", cancellationToken);

		response.Headers.Contains("Strict-Transport-Security").Should().BeTrue();
	}
}
