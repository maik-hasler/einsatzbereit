using Application.AuditLogs;
using Application.AuditLogs.ListAuditLogs.v1;
using Application.Common.Pagination;
using AwesomeAssertions;
using NSubstitute;

namespace Application.UnitTests.AuditLogs.ListAuditLogs;

public class ListAuditLogsQueryHandlerTests
{
	private readonly IAdminAuditLogReadRepository _readRepo =
		Substitute.For<IAdminAuditLogReadRepository>();
	private readonly ListAuditLogsQueryHandler _sut;

	public ListAuditLogsQueryHandlerTests()
	{
		_readRepo
			.GetAuditLogsPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<AuditLogEntry>([], 0, 1, 10));
		_sut = new ListAuditLogsQueryHandler(_readRepo);
	}

	[Test]
	public async Task Handle_ShouldReturnAuditLogEntries_FromReadRepository(
		CancellationToken cancellationToken)
	{
		var entry = new AuditLogEntry(
			Guid.NewGuid(), Guid.NewGuid(), "Admina Admin", "UserShadowDeleted", "User", Guid.NewGuid(), "Volunteera Vera", null, DateTimeOffset.UtcNow);

		_readRepo
			.GetAuditLogsPagedAsync(1, 10, cancellationToken)
			.Returns(new PagedList<AuditLogEntry>([entry], 1, 1, 10));

		var result = await _sut.Handle(new ListAuditLogsQuery(1, 10), cancellationToken);

		result.Items.Should().ContainSingle().Which.Should().Be(entry);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListAuditLogsQuery(0, 10), cancellationToken);

		await _readRepo.Received(1).GetAuditLogsPagedAsync(1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampNegativePageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListAuditLogsQuery(-5, 10), cancellationToken);

		await _readRepo.Received(1).GetAuditLogsPagedAsync(1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageSize_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListAuditLogsQuery(1, 0), cancellationToken);

		await _readRepo.Received(1).GetAuditLogsPagedAsync(1, 1, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCapExcessivePageSize_ToHundred(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListAuditLogsQuery(1, 5000), cancellationToken);

		await _readRepo.Received(1).GetAuditLogsPagedAsync(1, 100, cancellationToken);
	}
}
