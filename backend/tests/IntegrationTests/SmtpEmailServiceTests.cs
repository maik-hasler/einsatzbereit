using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Application.Common.Email;
using AwesomeAssertions;
using Infrastructure.Email;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace IntegrationTests;

// Exercises Infrastructure.Email.SmtpEmailService directly (InternalsVisibleTo,
// see Infrastructure.csproj) against a loopback socket instead of a real relay,
// so - unlike the rest of this project - it needs no Aspire/Testcontainers
// fixture and runs as a plain fast unit test.
//
// Regression coverage for #1341: a send failure must be swallowed - SendAsync
// never throws, by design (see the comment on IEmailService.SendBatchAsync) -
// but still observable, now via a metric rather than only a log line nothing
// was watching.
//
// [NotInParallel] with a key unique to this class (not the "IntegrationDb"
// key other IntegrationTests classes share): EmailMetrics.MeterName is a
// process-wide constant, so a MeterListener started in one test backfires
// on every not-yet-disposed Meter of that name from a concurrently-running
// test in this class too - it must not also serialize against the unrelated
// Aspire/Testcontainers-backed suite this class deliberately avoids needing.
[NotInParallel(nameof(SmtpEmailServiceTests))]
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
		var act = () => sut.SendAsync("volunteer@example.com", "Test subject", "Test body", "test-correlation-id");

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
		var act = () => sut.SendAsync("volunteer@example.com", "Test subject", "Test body", "test-correlation-id");

		await act.Should().NotThrowAsync();

		recorded.Should().ContainSingle(m => m.Status == "failed" && m.Value == 1);
	}

	// Regression coverage for einsatzbereit#1189: a failed send used to log the
	// recipient's raw email address plus the subject line (which can itself carry
	// a volunteer's name), landing real PII in container logs and the OTLP sink on
	// every misconfigured-SMTP send. The failure log must now carry only the
	// correlation id the caller supplied, never the address or subject.
	[Test]
	public async Task SendAsync_ServerUnreachable_LogsCorrelationIdWithoutRecipientOrSubject()
	{
		var unusedPort = GetUnusedLoopbackPort();
		using var meterFactory = new TestMeterFactory();
		var metrics = new EmailMetrics(meterFactory);
		var logger = new FakeLogger<SmtpEmailService>();

		var sut = CreateService(unusedPort, metrics, logger);
		await sut.SendAsync("volunteer@example.com", "New sign-up: Vera joined \"Beach Cleanup\"", "Test body", "engagement-correlation-id");

		var record = logger.Collector.GetSnapshot().Should().ContainSingle(r => r.Level == LogLevel.Error).Subject;
		record.Message.Should().Contain("engagement-correlation-id");
		record.Message.Should().NotContain("volunteer@example.com");
		record.Message.Should().NotContain("Vera");
		record.Message.Should().NotContain("Beach Cleanup");
	}

	// Regression coverage for #1400: SendBatchAsync must share one SMTP connection
	// across every message in the batch, not reconnect per message. FakeSmtpServer
	// only ever accepts one TCP connection (see AcceptAsync below), so if
	// SendBatchAsync tried to reconnect for message 2 or 3 there would be nothing
	// listening and those sends would fail - all three succeeding here is itself
	// proof the batch shares one connection.
	[Test]
	public async Task SendBatchAsync_ServerAcceptsAllMessages_SendsEveryMessageOverOneConnection()
	{
		await using var server = await FakeSmtpServer.StartAsync();
		using var meterFactory = new TestMeterFactory();
		var metrics = new EmailMetrics(meterFactory);
		var recorded = RecordEmailSendMeasurements(meterFactory);

		var sut = CreateService(server.Port, metrics);
		var messages = new[]
		{
			new EmailMessage("vera@example.com", "Reminder 1", "Body 1", "corr-1"),
			new EmailMessage("olaf@example.com", "Reminder 2", "Body 2", "corr-2"),
			new EmailMessage("admin@example.com", "Reminder 3", "Body 3", "corr-3"),
		};

		var results = await sut.SendBatchAsync(messages);

		results.Should().Equal(true, true, true);
		recorded.Count(m => m.Status == "succeeded").Should().Be(3);
		recorded.Should().NotContain(m => m.Status == "failed");
	}

	[Test]
	public async Task SendBatchAsync_ServerRejectsOneRecipient_ReportsThatMessageAsFailedAndKeepsSendingTheRest()
	{
		await using var server = await FakeSmtpServer.StartAsync(rejectRecipient: "olaf@example.com");
		using var meterFactory = new TestMeterFactory();
		var metrics = new EmailMetrics(meterFactory);
		var recorded = RecordEmailSendMeasurements(meterFactory);

		var sut = CreateService(server.Port, metrics);
		var messages = new[]
		{
			new EmailMessage("vera@example.com", "Reminder 1", "Body 1", "corr-1"),
			new EmailMessage("olaf@example.com", "Reminder 2", "Body 2", "corr-2"),
			new EmailMessage("admin@example.com", "Reminder 3", "Body 3", "corr-3"),
		};

		var results = await sut.SendBatchAsync(messages);

		results.Should().Equal(true, false, true);
		recorded.Count(m => m.Status == "succeeded").Should().Be(2);
		recorded.Count(m => m.Status == "failed").Should().Be(1);
	}

	[Test]
	public async Task SendBatchAsync_ServerRejectsOneRecipient_LogsCorrelationIdWithoutRecipientOrSubject()
	{
		await using var server = await FakeSmtpServer.StartAsync(rejectRecipient: "olaf@example.com");
		using var meterFactory = new TestMeterFactory();
		var metrics = new EmailMetrics(meterFactory);
		var logger = new FakeLogger<SmtpEmailService>();

		var sut = CreateService(server.Port, metrics, logger);
		var messages = new[]
		{
			new EmailMessage("vera@example.com", "Reminder 1", "Body 1", "corr-1"),
			new EmailMessage("olaf@example.com", "New sign-up: Vera joined \"Beach Cleanup\"", "Body 2", "corr-2-rejected"),
			new EmailMessage("admin@example.com", "Reminder 3", "Body 3", "corr-3"),
		};

		await sut.SendBatchAsync(messages);

		var record = logger.Collector.GetSnapshot().Should().ContainSingle(r => r.Level == LogLevel.Error).Subject;
		record.Message.Should().Contain("corr-2-rejected");
		record.Message.Should().NotContain("olaf@example.com");
		record.Message.Should().NotContain("Vera");
		record.Message.Should().NotContain("Beach Cleanup");
	}

	[Test]
	public async Task SendBatchAsync_ServerUnreachable_AllMessagesFailWithoutThrowing()
	{
		var unusedPort = GetUnusedLoopbackPort();
		using var meterFactory = new TestMeterFactory();
		var metrics = new EmailMetrics(meterFactory);
		var recorded = RecordEmailSendMeasurements(meterFactory);

		var sut = CreateService(unusedPort, metrics);
		var messages = new[]
		{
			new EmailMessage("vera@example.com", "Reminder 1", "Body 1", "corr-1"),
			new EmailMessage("olaf@example.com", "Reminder 2", "Body 2", "corr-2"),
		};

		var results = await sut.SendBatchAsync(messages);

		results.Should().Equal(false, false);
		recorded.Count(m => m.Status == "failed").Should().Be(2);
	}

	[Test]
	public async Task SendBatchAsync_EmptyMessageList_ReturnsEmptyResultsWithoutConnecting()
	{
		using var meterFactory = new TestMeterFactory();
		var metrics = new EmailMetrics(meterFactory);
		var recorded = RecordEmailSendMeasurements(meterFactory);

		// Deliberately no FakeSmtpServer started: an empty batch must not attempt
		// a connection at all, so an unused port never being listened on is fine.
		var sut = CreateService(GetUnusedLoopbackPort(), metrics);

		var results = await sut.SendBatchAsync([]);

		results.Should().BeEmpty();
		recorded.Should().BeEmpty();
	}

	private static SmtpEmailService CreateService(int port, EmailMetrics metrics, ILogger<SmtpEmailService>? logger = null) =>
		new(
			Options.Create(new SmtpOptions
			{
				Host = "127.0.0.1",
				Port = port,
				FromAddress = "noreply@test.local",
				FromName = "Test",
				EnableSsl = false,
			}),
			logger ?? NullLogger<SmtpEmailService>.Instance,
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
	// Username=null in these tests), just enough for MailKit's SmtpClient.SendAsync
	// to consider the send successful.
	private sealed class FakeSmtpServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly Task _acceptTask;
		private readonly string? _rejectRecipient;

		private FakeSmtpServer(TcpListener listener, int port, string? rejectRecipient)
		{
			_listener = listener;
			Port = port;
			_rejectRecipient = rejectRecipient;
			_acceptTask = AcceptAsync();
		}

		public int Port { get; }

		public static Task<FakeSmtpServer> StartAsync(string? rejectRecipient = null)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var port = ((IPEndPoint)listener.LocalEndpoint).Port;
			return Task.FromResult(new FakeSmtpServer(listener, port, rejectRecipient));
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
				else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase))
				{
					await writer.WriteLineAsync("250 OK");
				}
				else if (line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
				{
					if (_rejectRecipient is not null &&
						line.Contains(_rejectRecipient, StringComparison.OrdinalIgnoreCase))
					{
						await writer.WriteLineAsync("550 Requested action not taken: mailbox unavailable");
					}
					else
					{
						await writer.WriteLineAsync("250 OK");
					}
				}
				else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
				{
					await writer.WriteLineAsync("354 Start mail input");
					while ((line = await reader.ReadLineAsync()) is not null && line != ".")
					{
					}

					await writer.WriteLineAsync("250 OK queued");
				}
				else if (line.StartsWith("RSET", StringComparison.OrdinalIgnoreCase))
				{
					// MailKit issues RSET to clear the transaction after a rejected
					// RCPT TO before starting the next message - without a reply
					// here the client blocks reading a response that never comes.
					await writer.WriteLineAsync("250 OK");
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
