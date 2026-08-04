using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Users.SaveSearchAlert.v1;
using AwesomeAssertions;
using Domain.SearchAlerts;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.SaveSearchAlert;

public class SaveSearchAlertCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<SearchAlert, SearchAlertId> _searchAlertRepo =
		Substitute.For<IAggregateRepository<SearchAlert, SearchAlertId>>();
	private readonly SaveSearchAlertCommandHandler _sut;

	private static readonly UserId TestUserId = UserId.New();

	public SaveSearchAlertCommandHandlerTests()
	{
		_dbContext.SearchAlerts.Returns(_searchAlertRepo);
		_dbContext
			.GetSearchAlertForUserAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns((SearchAlert?)null);
		_sut = new SaveSearchAlertCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldCreateNewAlert_WhenNoneExists(
		CancellationToken cancellationToken)
	{
		var command = new SaveSearchAlertCommand(
			TestUserId, "OneTime", "IndividualContact", true, null, null, null, ["Environment"], "cleanup");

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		await _searchAlertRepo.Received(1).AddAsync(
			Arg.Is<SearchAlert>(a =>
				a!.UserId == TestUserId &&
				a.Occurrence == Domain.VolunteerOpportunities.Occurrence.OneTime &&
				a.ParticipationType == Domain.VolunteerOpportunities.ParticipationType.IndividualContact &&
				a.IsRemote == true &&
				a.Tag == "cleanup" &&
				a.Categories.Count == 1),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldReplaceCriteria_WhenAlertAlreadyExists(
		CancellationToken cancellationToken)
	{
		var existing = SearchAlert.Create(TestUserId, Domain.VolunteerOpportunities.Occurrence.OneTime, null, null, null, null, null);
		_dbContext
			.GetSearchAlertForUserAsync(TestUserId, cancellationToken)
			.Returns(existing);
		var command = new SaveSearchAlertCommand(
			TestUserId, "Recurring", null, null, null, null, null, null, null);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		existing.Occurrence.Should().Be(Domain.VolunteerOpportunities.Occurrence.Recurring);
		await _searchAlertRepo.DidNotReceive().AddAsync(Arg.Any<SearchAlert>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOccurrenceIsInvalid(
		CancellationToken cancellationToken)
	{
		var command = new SaveSearchAlertCommand(
			TestUserId, "NotAnOccurrence", null, null, null, null, null, null, null);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*occurrence*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenCategoryIsInvalid(
		CancellationToken cancellationToken)
	{
		var command = new SaveSearchAlertCommand(
			TestUserId, null, null, null, null, null, null, ["NotACategory"], null);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*category*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOnlyLatitudeIsSupplied(
		CancellationToken cancellationToken)
	{
		var command = new SaveSearchAlertCommand(
			TestUserId, null, null, null, 52.5, null, 10, null, null);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*together*");
	}

	[Test]
	[Arguments(0)]
	[Arguments(501)]
	public async Task Handle_ShouldThrow_WhenRadiusIsOutOfRange(
		double radiusKm, CancellationToken cancellationToken)
	{
		var command = new SaveSearchAlertCommand(
			TestUserId, null, null, null, 52.5, 13.4, radiusKm, null, null);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*Radius*");
	}
}
