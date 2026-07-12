using Application.Common.Pagination;
using Application.Engagements;
using Application.Engagements.GetMyEngagements.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Engagements.GetMyEngagements;

public class GetMyEngagementsQueryHandlerTests
{
	private readonly IEngagementReadRepository _readRepository =
		Substitute.For<IEngagementReadRepository>();
	private readonly GetMyEngagementsQueryHandler _sut;
	private readonly UserId _volunteerId = new(Guid.CreateVersion7());

	public GetMyEngagementsQueryHandlerTests()
	{
		_readRepository
			.GetByVolunteerAsync(Arg.Any<UserId>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([], 0, 1, 10));
		_sut = new GetMyEngagementsQueryHandler(_readRepository);
	}

	private async Task<(int PageNumber, int PageSize, bool Upcoming)> CapturedArgsAsync(
		int pageNumber, int pageSize, bool upcoming)
	{
		var capturedUpcoming = false;
		var capturedPageNumber = 0;
		var capturedPageSize = 0;
		_readRepository
			.GetByVolunteerAsync(
				Arg.Any<UserId>(),
				Arg.Do<bool>(u => capturedUpcoming = u),
				Arg.Do<int>(p => capturedPageNumber = p),
				Arg.Do<int>(s => capturedPageSize = s),
				Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([], 0, 1, 10));

		await _sut.Handle(new GetMyEngagementsQuery(_volunteerId, pageNumber, pageSize, upcoming), CancellationToken.None);

		return (capturedPageNumber, capturedPageSize, capturedUpcoming);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageNumber_ToOne()
	{
		var (pageNumber, _, _) = await CapturedArgsAsync(pageNumber: 0, pageSize: 10, upcoming: true);
		pageNumber.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldClampNegativePageNumber_ToOne()
	{
		var (pageNumber, _, _) = await CapturedArgsAsync(pageNumber: -5, pageSize: 10, upcoming: true);
		pageNumber.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageSize_ToOne()
	{
		var (_, pageSize, _) = await CapturedArgsAsync(pageNumber: 1, pageSize: 0, upcoming: true);
		pageSize.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldCapExcessivePageSize_ToHundred()
	{
		var (_, pageSize, _) = await CapturedArgsAsync(pageNumber: 1, pageSize: 5000, upcoming: true);
		pageSize.Should().Be(100);
	}

	[Test]
	public async Task Handle_ShouldPassThroughUpcomingFlag_WhenTrue()
	{
		var (_, _, upcoming) = await CapturedArgsAsync(pageNumber: 1, pageSize: 10, upcoming: true);
		upcoming.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldPassThroughUpcomingFlag_WhenFalse()
	{
		var (_, _, upcoming) = await CapturedArgsAsync(pageNumber: 1, pageSize: 10, upcoming: false);
		upcoming.Should().BeFalse();
	}
}
