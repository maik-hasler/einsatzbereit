using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.SetOpportunityColor.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.SetOpportunityColor;

public class SetOpportunityColorCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly SetOpportunityColorCommandHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public SetOpportunityColorCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new SetOpportunityColorCommandHandler(_dbContext);
	}

	private VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", "Beschreibung", true, null, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;

	[Test]
	public async Task Handle_ShouldSetColor_WhenOpportunityExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		// #c10007, not #ff0000: pure red's best text contrast falls short of
		// the 4.5:1 floor added for einsatzbereit#1726 - see
		// VolunteerOpportunityTests.SetColor_ShouldFail_WhenTextContrastIsBelowMinimum.
		var command = new SetOpportunityColorCommand(opportunityId, "#c10007", DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		opportunity.Color.Should().Be("#c10007");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange: caller belongs to a different organization than the opportunity's.
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var command = new SetOpportunityColorCommand(opportunityId, "#ff0000", DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		opportunity.Color.Should().BeNull();
	}
}
