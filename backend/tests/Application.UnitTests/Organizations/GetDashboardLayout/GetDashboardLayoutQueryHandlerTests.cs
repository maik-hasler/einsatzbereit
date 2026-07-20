using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Organizations.GetDashboardLayout.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Organizations.GetDashboardLayout;

public class GetDashboardLayoutQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Organization, OrganizationId> _orgRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly GetDashboardLayoutQueryHandler _sut;

	private static readonly Guid DefaultOrgId = Guid.NewGuid();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public GetDashboardLayoutQueryHandlerTests()
	{
		_dbContext.Organizations.Returns(_orgRepo);
		_orgRepo
			.FindAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
			.Returns(Organization.Create(OrganizationId.Create(DefaultOrgId).GetValueOrThrow(), "Sample Fire Department").Value);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new GetDashboardLayoutQueryHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOrganizationNotFound(
		CancellationToken cancellationToken)
	{
		_orgRepo.FindAsync(OrganizationId.Create(DefaultOrgId).GetValueOrThrow(), cancellationToken).Returns((Organization?)null);
		var query = new GetDashboardLayoutQuery(DefaultOrgId, DefaultRequestingUserId);

		var act = async () => await _sut.Handle(query, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*not found*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns(false);
		var query = new GetDashboardLayoutQuery(DefaultOrgId, DefaultRequestingUserId);

		var act = async () => await _sut.Handle(query, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*permission*");
	}

	[Test]
	public async Task Handle_ShouldReturnEmptyWidgetsAndHasCustomLayoutFalse_WhenNoLayoutSaved(
		CancellationToken cancellationToken)
	{
		_dbContext
			.GetDashboardLayoutAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns((OrganizationDashboardLayout?)null);
		var query = new GetDashboardLayoutQuery(DefaultOrgId, DefaultRequestingUserId);

		var result = await _sut.Handle(query, cancellationToken);

		result.Widgets.Should().BeEmpty();
		result.HasCustomLayout.Should().BeFalse(
			"no OrganizationDashboardLayout row exists yet - the frontend should apply its own default layout");
	}

	[Test]
	public async Task Handle_ShouldReturnWidgetsAndHasCustomLayoutTrue_WhenLayoutExists(
		CancellationToken cancellationToken)
	{
		var layout = OrganizationDashboardLayout.Create(
			OrganizationId.Create(DefaultOrgId).GetValueOrThrow(),
			DefaultRequestingUserId,
			[
				new DashboardWidgetPlacement(DashboardWidgetKey.ToDo),
				new DashboardWidgetPlacement(DashboardWidgetKey.Calendar),
			]);
		_dbContext
			.GetDashboardLayoutAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns(layout);
		var query = new GetDashboardLayoutQuery(DefaultOrgId, DefaultRequestingUserId);

		var result = await _sut.Handle(query, cancellationToken);

		result.HasCustomLayout.Should().BeTrue();
		result.Widgets.Should().BeEquivalentTo(
		[
			new DashboardWidgetPlacementResponse("ToDo"),
			new DashboardWidgetPlacementResponse("Calendar"),
		], options => options.WithStrictOrdering());
	}

	[Test]
	public async Task Handle_ShouldReturnHasCustomLayoutTrue_WhenLayoutExistsWithZeroWidgets(
		CancellationToken cancellationToken)
	{
		// Regression guard for #771 review feedback: an organizer who removes
		// every widget and saves that must NOT be indistinguishable from a
		// brand-new organizer who never customized anything - both have an
		// empty Widgets list, but only this one has a saved layout row.
		var layout = OrganizationDashboardLayout.Create(
			OrganizationId.Create(DefaultOrgId).GetValueOrThrow(),
			DefaultRequestingUserId,
			[]);
		_dbContext
			.GetDashboardLayoutAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns(layout);
		var query = new GetDashboardLayoutQuery(DefaultOrgId, DefaultRequestingUserId);

		var result = await _sut.Handle(query, cancellationToken);

		result.HasCustomLayout.Should().BeTrue();
		result.Widgets.Should().BeEmpty();
	}
}
