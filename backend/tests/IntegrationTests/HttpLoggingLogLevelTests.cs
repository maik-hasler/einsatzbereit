using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationTests;

// Regression coverage for #1352: "Microsoft.AspNetCore": "Warning" in appsettings.json's
// LogLevel map was the longest matching prefix for the
// "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware" category, so Program.cs's
// AddHttpLogging/UseHttpLogging calls produced no output outside Development - the
// middleware ran, but every entry it emitted (at Information) was filtered out before
// reaching any provider. Builds a real host directly (like SmtpEmailServiceTests, no
// Aspire/Testcontainers fixture needed) with the exact LogLevel keys from
// Api/appsettings.json, so it catches the suppression from a plain unit-style run.
public class HttpLoggingLogLevelTests
{
	private static readonly Dictionary<string, string?> ProductionLogLevelConfig = new()
	{
		["Logging:LogLevel:Default"] = "Information",
		["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
		["Logging:LogLevel:Microsoft.AspNetCore.HttpLogging"] = "Information",
	};

	[Test]
	public async Task HttpLoggingCategory_IsEnabledAtInformation_WithProductionLogLevelConfig()
	{
		var builder = WebApplication.CreateBuilder();
		builder.Configuration.AddInMemoryCollection(ProductionLogLevelConfig);

		await using var app = builder.Build();

		var logger = app.Services.GetRequiredService<ILoggerFactory>()
			.CreateLogger("Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware");

		logger.IsEnabled(LogLevel.Information).Should().BeTrue();
	}

	// Guards against overcorrecting: the fix adds a more specific override for the
	// HttpLogging subcategory, it must not blanket re-enable Information logs for the rest
	// of "Microsoft.AspNetCore" - that category was set to Warning deliberately, to keep
	// framework-internal noise (routing, hosting, etc.) out of the shipped logs.
	[Test]
	public async Task OtherAspNetCoreCategories_StayFilteredAtWarning_WithProductionLogLevelConfig()
	{
		var builder = WebApplication.CreateBuilder();
		builder.Configuration.AddInMemoryCollection(ProductionLogLevelConfig);

		await using var app = builder.Build();

		var logger = app.Services.GetRequiredService<ILoggerFactory>()
			.CreateLogger("Microsoft.AspNetCore.Routing.EndpointMiddleware");

		logger.IsEnabled(LogLevel.Information).Should().BeFalse();
		logger.IsEnabled(LogLevel.Warning).Should().BeTrue();
	}
}
