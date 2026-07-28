using System.Net;
using System.Net.Sockets;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
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
		var mainPort = GetFreeTcpPort();
		var metricsPort = GetFreeTcpPort();

		var builder = WebApplication.CreateBuilder();
		builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["ASPNETCORE_HTTP_PORTS"] = mainPort.ToString(),
			["Metrics:Port"] = metricsPort.ToString(),
		});

		builder.AddServiceDefaults();

		await using var app = builder.Build();
		app.MapDefaultEndpoints();

		await app.StartAsync();
		try
		{
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

	private static int GetFreeTcpPort()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}
}
