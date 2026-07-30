using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.GetOpportunityCheckInPin.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.GetOpportunityCheckInPin;

public class GetOpportunityCheckInPinQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly GetOpportunityCheckInPinQueryHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public GetOpportunityCheckInPinQueryHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new GetOpportunityCheckInPinQueryHandler(_dbContext);
	}

	private VolunteerOpportunity CreateOpportunityWithPin() =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", "Beschreibung", true, null, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.PINCode, _pinGenerator,
			status: OpportunityStatus.Draft, checkInPin: "12345").Value;

	[Test]
	public async Task Handle_ShouldReturnCheckInPin_WhenOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunityWithPin();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var query = new GetOpportunityCheckInPinQuery(opportunity.Id, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().Be("12345");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange: caller belongs to a different organization than the opportunity's -
		// this is the highest-priority gap called out by the audit, since a leaked PIN
		// lets an outsider forge check-ins for another org's opportunity.
		var opportunity = CreateOpportunityWithPin();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var query = new GetOpportunityCheckInPinQuery(opportunity.Id, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
	}
}
