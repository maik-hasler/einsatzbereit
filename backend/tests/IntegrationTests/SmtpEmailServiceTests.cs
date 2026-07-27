using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AwesomeAssertions;
using Infrastructure.Email;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IntegrationTests;

// Exercises Infrastructure.Email.SmtpEmailService directly (InternalsVisibleTo,
// see Infrastructure.csproj) against a loopback socket instead of a real relay,
// so - unlike the rest of this project - it needs no Aspire/Testcontainers
// fixture and runs as a plain fast unit test.
//
// Regression coverage for #1341: a send failure must be swallowed (callers
// like EngagementReminderJob rely on this - see its MarkReminderSent right
// after SendAsync) but still observable, now via a metric rather than only a
// log line nothing was watching.
public class SmtpEmailServiceTests
{
	[Test]
	public async Task SendAsync_ServerAcceptsMail_RecordsSucceededMetricAndDoesNotThrow()
	{
		await using var server = await FakeSmtpServer.StartAsync();
		using var meterFactory = new TestMeterFactory();
		var metrics = new EmailMetrics(meterFactory);
		var recorded = RecordEmailSendMeasurements(meterFactory);

		var sut = CreateService(server.Port, metrics);
		var act = () => sut.SendAsync("volunteer@example.com", "Test subject", "Test body");

		await act.Should().NotThrowAsync();

		recorded.Should().ContainSingle(m => m.Status == "succeeded" && m.Value == 1);
	}

	[Test]
	public async Task SendAsync_ServerUnreachable_RecordsFailedMetricAndDoesNotThrow()
	{
		var unusedPort = GetUnusedLoopbackPort();
		using var meterFactory = new TestMeterFactory();
		var metrics = new EmailMetrics(meterFactory);
		var recorded = RecordEmailSendMeasurements(meterFactory);

		var sut = CreateService(unusedPort, metrics);
		var act = () => sut.SendAsync("volunteer@example.com", "Test subject", "Test body");

		await act.Should().NotThrowAsync();

		recorded.Should().ContainSingle(m => m.Status == "failed" && m.Value == 1);
	}

	private static SmtpEmailService CreateService(int port, EmailMetrics metrics) =>
		new(
			Options.Create(new SmtpOptions
			{
				Host = "127.0.0.1",
				Port = port,
				FromAddress = "noreply@test.local",
				FromName = "Test",
				EnableSsl = false,
			}),
			NullLogger<SmtpEmailService>.Instance,
			metrics);

	private static List<(string Status, long Value)> RecordEmailSendMeasurements(IMeterFactory meterFactory)
	{
		var recorded = new List<(string, long)>();

		var listener = new MeterListener
		{
			InstrumentPublished = (instrument, l) =>
			{
				if (instrument.Meter.Name == EmailMetrics.MeterName)
					l.EnableMeasurementEvents(instrument);
			},
		};
		listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
		{
			var status = tags.ToArray()
				.FirstOrDefault(t => t.Key == "status").Value?.ToString()
				?? throw new InvalidOperationException("Measurement missing 'status' tag.");
			recorded.Add((status, measurement));
		});
		listener.Start();

		// Meter creation (inside EmailMetrics' constructor) must happen after the
		// listener is listening, or InstrumentPublished never fires for it - but
		// callers construct EmailMetrics before this returns, so replay isn't
		// needed here; RecordEmailSendMeasurements is always called first in
		// these tests. Recording the listener disposal isn't necessary either:
		// it's process/test-local and TUnit tears down between tests.
		return recorded;
	}

	private static int GetUnusedLoopbackPort()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		try
		{
			return ((IPEndPoint)listener.LocalEndpoint).Port;
		}
		finally
		{
			listener.Stop();
		}
	}

	private sealed class TestMeterFactory : IMeterFactory
	{
		private readonly List<Meter> _meters = [];

		public Meter Create(MeterOptions options)
		{
			var meter = new Meter(options);
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters)
				meter.Dispose();
		}
	}

	// Minimal but protocol-correct SMTP responder: greets, accepts EHLO/MAIL
	// FROM/RCPT TO/DATA/QUIT with no auth or TLS (matching EnableSsl=false,
	// Username=null in these tests), just enough for SmtpClient.SendMailAsync
	// to consider the send successful.
	private sealed class FakeSmtpServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly Task _acceptTask;

		private FakeSmtpServer(TcpListener listener, int port)
		{
			_listener = listener;
			Port = port;
			_acceptTask = AcceptAsync();
		}

		public int Port { get; }

		public static Task<FakeSmtpServer> StartAsync()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var port = ((IPEndPoint)listener.LocalEndpoint).Port;
			return Task.FromResult(new FakeSmtpServer(listener, port));
		}

		private async Task AcceptAsync()
		{
			using var client = await _listener.AcceptTcpClientAsync();
			await using var stream = client.GetStream();
			using var reader = new StreamReader(stream, Encoding.ASCII);
			await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

			await writer.WriteLineAsync("220 fake.local ESMTP");

			string? line;
			while ((line = await reader.ReadLineAsync()) is not null)
			{
				if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
				{
					await writer.WriteLineAsync("250 fake.local");
				}
				else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase)
					|| line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
				{
					await writer.WriteLineAsync("250 OK");
				}
				else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
				{
					await writer.WriteLineAsync("354 Start mail input");
					while ((line = await reader.ReadLineAsync()) is not null && line != ".")
					{
					}

					await writer.WriteLineAsync("250 OK queued");
				}
				else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
				{
					await writer.WriteLineAsync("221 Bye");
					break;
				}
			}
		}

		public async ValueTask DisposeAsync()
		{
			_listener.Stop();
			try
			{
				await _acceptTask.WaitAsync(TimeSpan.FromSeconds(5));
			}
			catch (Exception)
			{
			}
		}
	}
}
