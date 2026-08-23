using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationTests;

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
