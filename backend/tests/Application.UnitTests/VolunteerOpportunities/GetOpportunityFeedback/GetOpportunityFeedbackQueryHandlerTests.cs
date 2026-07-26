using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.GetOpportunityFeedback.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.GetOpportunityFeedback;

public class GetOpportunityFeedbackQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IEngagementReadRepository _engagementReadRepository = Substitute.For<IEngagementReadRepository>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly GetOpportunityFeedbackQueryHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public GetOpportunityFeedbackQueryHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new GetOpportunityFeedbackQueryHandler(_dbContext, _engagementReadRepository);
	}

	private VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", "Beschreibung", true, null, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;

	[Test]
	public async Task Handle_ShouldReturnFeedbackSummary_WhenOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var summary = new OpportunityFeedbackSummary(4.5, 2,
		[
			new FeedbackItemDto(5, "Great!", DateTimeOffset.UtcNow),
			new FeedbackItemDto(4, null, DateTimeOffset.UtcNow),
		]);
		_engagementReadRepository.GetFeedbackByOpportunityAsync(opportunity.Id, cancellationToken).Returns(summary);

		var query = new GetOpportunityFeedbackQuery(opportunity.Id, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().BeEquivalentTo(summary);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange: caller belongs to a different organization than the opportunity's.
		var opportunity = CreateOpportunity();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var query = new GetOpportunityFeedbackQuery(opportunity.Id, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		await _engagementReadRepository.DidNotReceive().GetFeedbackByOpportunityAsync(
			Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>());
	}
}
