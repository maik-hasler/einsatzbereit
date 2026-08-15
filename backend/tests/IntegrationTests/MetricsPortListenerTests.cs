using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace IntegrationTests;

// Regression coverage for a staging outage: AddMetricsPortListener
// (Aspire/ServiceDefaults/Extensions.cs) used to call ConfigureKestrel with only
// the metrics port's ListenAnyIP, which replaces ASP.NET Core's default HTTP
// binding instead of adding to it, so the app stopped listening on its main port
// entirely - unreachable to both Traefik and the docker-compose healthcheck even
// though the process itself was healthy. Builds a real Kestrel host directly
// (like SmtpEmailServiceTests, no Aspire/Testcontainers fixture needed) so it
// runs fast and would have caught this before it reached staging.
public class MetricsPortListenerTests
{
	[Test]
	public async Task AddServiceDefaults_WithMetricsPortConfigured_StillListensOnMainHttpPort()
	{
		var builder = WebApplication.CreateBuilder();
		builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
		{
			// Port 0 asks the OS to assign a free ephemeral port at bind time,
			// read back below via app.Urls once Kestrel has actually bound it -
			// unlike pre-selecting a "free" port with a close-then-reopen probe,
			// there is no window between choosing the port and binding it for
			// something else (a parallel test, the metrics listener itself) to
			// grab first, which used to fail this test under CI's parallel test
			// load with "address already in use".
			["ASPNETCORE_HTTP_PORTS"] = "0",
			["Metrics:Port"] = "0",
		});

		builder.AddServiceDefaults();

		await using var app = builder.Build();
		app.MapDefaultEndpoints();

		await app.StartAsync();
		try
		{
			// AddMetricsPortListener always registers the main listener before the
			// metrics one (see its own comment), and Kestrel binds - and reports
			// back via app.Urls - each configured listener in that same order.
			app.Urls.Should().HaveCount(2,
				"AddMetricsPortListener should bind exactly one main and one metrics listener");
			var mainPort = new Uri(app.Urls.ElementAt(0)).Port;
			var metricsPort = new Uri(app.Urls.ElementAt(1)).Port;

			using var client = new HttpClient();

			var mainResponse = await client.GetAsync($"http://localhost:{mainPort}/alive");
			mainResponse.StatusCode.Should().Be(HttpStatusCode.OK);

			var metricsResponse = await client.GetAsync($"http://localhost:{metricsPort}/alive");
			metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		}
		finally
		{
			await app.StopAsync();
		}
	}
}
