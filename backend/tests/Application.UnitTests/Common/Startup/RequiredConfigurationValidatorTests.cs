using Application.Common.Startup;
using AwesomeAssertions;

namespace Application.UnitTests.Common.Startup;

public class RequiredConfigurationValidatorTests
{
	[Test]
	public void FindMissing_ShouldReturnEmpty_WhenDevelopment_EvenWithNothingConfigured()
	{
		var missing = RequiredConfigurationValidator.FindMissing(
			isDevelopment: true,
			connectionString: null,
			keycloakClientSecret: null,
			authenticationAuthority: null,
			corsOrigins: null);

		missing.Should().BeEmpty();
	}

	[Test]
	public void FindMissing_ShouldReturnEmpty_WhenNotDevelopment_AndEverythingConfigured()
	{
		var missing = RequiredConfigurationValidator.FindMissing(
			isDevelopment: false,
			connectionString: "Host=postgres;Database=einsatzbereit",
			keycloakClientSecret: "real-secret",
			authenticationAuthority: "https://login.example.com/realms/einsatzbereit",
			corsOrigins: ["https://einsatzbereit.example.com"]);

		missing.Should().BeEmpty();
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public void FindMissing_ShouldFlagConnectionString_WhenNotDevelopment_AndBlank(string? connectionString)
	{
		var missing = RequiredConfigurationValidator.FindMissing(
			isDevelopment: false,
			connectionString: connectionString,
			keycloakClientSecret: "real-secret",
			authenticationAuthority: "https://login.example.com/realms/einsatzbereit",
			corsOrigins: ["https://einsatzbereit.example.com"]);

		missing.Should().ContainSingle().Which.Should().Be("ConnectionStrings:einsatzbereit");
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public void FindMissing_ShouldFlagKeycloakClientSecret_WhenNotDevelopment_AndBlank(string? keycloakClientSecret)
	{
		var missing = RequiredConfigurationValidator.FindMissing(
			isDevelopment: false,
			connectionString: "Host=postgres;Database=einsatzbereit",
			keycloakClientSecret: keycloakClientSecret,
			authenticationAuthority: "https://login.example.com/realms/einsatzbereit",
			corsOrigins: ["https://einsatzbereit.example.com"]);

		missing.Should().ContainSingle().Which.Should().Be("Keycloak:ClientSecret");
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public void FindMissing_ShouldFlagAuthenticationAuthority_WhenNotDevelopment_AndBlank(string? authenticationAuthority)
	{
		var missing = RequiredConfigurationValidator.FindMissing(
			isDevelopment: false,
			connectionString: "Host=postgres;Database=einsatzbereit",
			keycloakClientSecret: "real-secret",
			authenticationAuthority: authenticationAuthority,
			corsOrigins: ["https://einsatzbereit.example.com"]);

		missing.Should().ContainSingle().Which.Should().Be("Authentication:Authority");
	}

	[Test]
	public void FindMissing_ShouldFlagCorsOrigins_WhenNotDevelopment_AndNull()
	{
		var missing = RequiredConfigurationValidator.FindMissing(
			isDevelopment: false,
			connectionString: "Host=postgres;Database=einsatzbereit",
			keycloakClientSecret: "real-secret",
			authenticationAuthority: "https://login.example.com/realms/einsatzbereit",
			corsOrigins: null);

		missing.Should().ContainSingle().Which.Should().Be("Cors:Origins");
	}

	[Test]
	public void FindMissing_ShouldFlagCorsOrigins_WhenNotDevelopment_AndEmpty()
	{
		var missing = RequiredConfigurationValidator.FindMissing(
			isDevelopment: false,
			connectionString: "Host=postgres;Database=einsatzbereit",
			keycloakClientSecret: "real-secret",
			authenticationAuthority: "https://login.example.com/realms/einsatzbereit",
			corsOrigins: []);

		missing.Should().ContainSingle().Which.Should().Be("Cors:Origins");
	}

	[Test]
	public void FindMissing_ShouldFlagAll_WhenNotDevelopment_AndNothingConfigured()
	{
		var missing = RequiredConfigurationValidator.FindMissing(
			isDevelopment: false,
			connectionString: null,
			keycloakClientSecret: null,
			authenticationAuthority: null,
			corsOrigins: null);

		missing.Should().BeEquivalentTo(
			"ConnectionStrings:einsatzbereit",
			"Keycloak:ClientSecret",
			"Authentication:Authority",
			"Cors:Origins");
	}
}
