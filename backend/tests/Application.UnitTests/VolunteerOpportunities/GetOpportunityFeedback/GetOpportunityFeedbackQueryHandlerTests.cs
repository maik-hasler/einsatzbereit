using Application.Common.Exceptions;
using Application.Common.Pagination;
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
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(CreateOpportunity());
		_dbContext
			.IsMemberAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_engagementReadRepository
			.GetFeedbackByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(new OpportunityFeedbackSummary(null, 0, new PagedList<FeedbackItemDto>([], 0, 1, 10)));
		_sut = new GetOpportunityFeedbackQueryHandler(_dbContext, _engagementReadRepository);
	}

	private static VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", null, "Beschreibung", null, true, null, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, Substitute.For<IPinGenerator>(),
			status: OpportunityStatus.Draft).Value;

	private async Task<(int PageNumber, int PageSize)> CapturedArgsAsync(int pageNumber, int pageSize)
	{
		var capturedPageNumber = 0;
		var capturedPageSize = 0;
		_engagementReadRepository
			.GetFeedbackByOpportunityAsync(
				Arg.Any<VolunteerOpportunityId>(),
				Arg.Do<int>(p => capturedPageNumber = p),
				Arg.Do<int>(s => capturedPageSize = s),
				Arg.Any<CancellationToken>())
			.Returns(new OpportunityFeedbackSummary(null, 0, new PagedList<FeedbackItemDto>([], 0, 1, 10)));

		var opportunity = CreateOpportunity();
		_opportunityRepo.FindAsync(opportunity.Id, Arg.Any<CancellationToken>()).Returns(opportunity);

		await _sut.Handle(new GetOpportunityFeedbackQuery(opportunity.Id, DefaultRequestingUserId, pageNumber, pageSize), CancellationToken.None);

		return (capturedPageNumber, capturedPageSize);
	}

	[Test]
	public async Task Handle_ShouldReturnFeedbackSummary_WhenOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var summary = new OpportunityFeedbackSummary(4.5, 2, new PagedList<FeedbackItemDto>(
			[
				new FeedbackItemDto(5, "Great!", DateTimeOffset.UtcNow),
				new FeedbackItemDto(4, null, DateTimeOffset.UtcNow),
			],
			2, 1, 10));
		_engagementReadRepository.GetFeedbackByOpportunityAsync(opportunity.Id, 1, 10, cancellationToken).Returns(summary);

		var query = new GetOpportunityFeedbackQuery(opportunity.Id, DefaultRequestingUserId, 1, 10);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().BeEquivalentTo(summary);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageNumber_ToOne()
	{
		var (pageNumber, _) = await CapturedArgsAsync(pageNumber: 0, pageSize: 10);
		pageNumber.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldClampNegativePageNumber_ToOne()
	{
		var (pageNumber, _) = await CapturedArgsAsync(pageNumber: -5, pageSize: 10);
		pageNumber.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageSize_ToOne()
	{
		var (_, pageSize) = await CapturedArgsAsync(pageNumber: 1, pageSize: 0);
		pageSize.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldCapExcessivePageSize_ToHundred()
	{
		var (_, pageSize) = await CapturedArgsAsync(pageNumber: 1, pageSize: 5000);
		pageSize.Should().Be(100);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotAMember(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_dbContext
			.IsMemberAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var query = new GetOpportunityFeedbackQuery(opportunity.Id, DefaultRequestingUserId, 1, 10);

		// Act
		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		await _engagementReadRepository.DidNotReceive().GetFeedbackByOpportunityAsync(
			Arg.Any<VolunteerOpportunityId>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns((VolunteerOpportunity?)null);

		var query = new GetOpportunityFeedbackQuery(VolunteerOpportunityId.New(), DefaultRequestingUserId, 1, 10);

		// Act
		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
	}
}
