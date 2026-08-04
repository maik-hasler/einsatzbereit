using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Users.DeleteSearchAlert.v1;
using AwesomeAssertions;
using Domain.SearchAlerts;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.DeleteSearchAlert;

public class DeleteSearchAlertCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<SearchAlert, SearchAlertId> _searchAlertRepo =
		Substitute.For<IAggregateRepository<SearchAlert, SearchAlertId>>();
	private readonly DeleteSearchAlertCommandHandler _sut;

	private static readonly UserId TestUserId = UserId.New();

	public DeleteSearchAlertCommandHandlerTests()
	{
		_dbContext.SearchAlerts.Returns(_searchAlertRepo);
		_sut = new DeleteSearchAlertCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldDeleteAlert_WhenItExists(
		CancellationToken cancellationToken)
	{
		var alert = SearchAlert.Create(TestUserId, null, null, null, null, null, null);
		_dbContext
			.GetSearchAlertForUserAsync(TestUserId, cancellationToken)
			.Returns(alert);

		var result = await _sut.Handle(new DeleteSearchAlertCommand(TestUserId), cancellationToken);

		result.Should().BeTrue();
		_searchAlertRepo.Received(1).Delete(alert);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenNoAlertExists(
		CancellationToken cancellationToken)
	{
		_dbContext
			.GetSearchAlertForUserAsync(TestUserId, cancellationToken)
			.Returns((SearchAlert?)null);

		var act = async () => await _sut.Handle(new DeleteSearchAlertCommand(TestUserId), cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*No active search alert*");
	}
}
