using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Organizations.SaveDashboardLayout.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Organizations.SaveDashboardLayout;

public class SaveDashboardLayoutCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Organization, OrganizationId> _orgRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly IAggregateRepository<OrganizationDashboardLayout, OrganizationDashboardLayoutId> _layoutRepo =
		Substitute.For<IAggregateRepository<OrganizationDashboardLayout, OrganizationDashboardLayoutId>>();
	private readonly SaveDashboardLayoutCommandHandler _sut;

	private static readonly Guid DefaultOrgId = Guid.NewGuid();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	private static readonly IReadOnlyList<DashboardWidgetPlacementInput> ValidWidgets =
	[
		new DashboardWidgetPlacementInput("ToDo", 1, 1, 4, 2),
		new DashboardWidgetPlacementInput("Calendar", 1, 3, 8, 6),
	];

	public SaveDashboardLayoutCommandHandlerTests()
	{
		_dbContext.Organizations.Returns(_orgRepo);
		_dbContext.OrganizationDashboardLayouts.Returns(_layoutRepo);
		_orgRepo
			.FindAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
			.Returns(Organization.Create(OrganizationId.Create(DefaultOrgId).GetValueOrThrow(), "Sample Fire Department").Value);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_dbContext
			.GetDashboardLayoutAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns((OrganizationDashboardLayout?)null);
		_sut = new SaveDashboardLayoutCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOrganizationNotFound(
		CancellationToken cancellationToken)
	{
		_orgRepo.FindAsync(OrganizationId.Create(DefaultOrgId).GetValueOrThrow(), cancellationToken).Returns((Organization?)null);
		var command = new SaveDashboardLayoutCommand(DefaultOrgId, DefaultRequestingUserId, ValidWidgets);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*not found*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns(false);
		var command = new SaveDashboardLayoutCommand(DefaultOrgId, DefaultRequestingUserId, ValidWidgets);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*permission*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenWidgetKeyIsInvalid(
		CancellationToken cancellationToken)
	{
		var command = new SaveDashboardLayoutCommand(
			DefaultOrgId, DefaultRequestingUserId, [new DashboardWidgetPlacementInput("NotAWidget", 1, 1, 1, 1)]);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*widget key*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenWidgetKeyIsDuplicated(
		CancellationToken cancellationToken)
	{
		var command = new SaveDashboardLayoutCommand(
			DefaultOrgId,
			DefaultRequestingUserId,
			[
				new DashboardWidgetPlacementInput("ToDo", 1, 1, 4, 2),
				new DashboardWidgetPlacementInput("ToDo", 5, 1, 4, 2),
			]);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*more than once*");
	}

	[Test]
	[Arguments(0, 1, 4, 2)] // X below 1
	[Arguments(1, 0, 4, 2)] // Y below 1
	[Arguments(1, 1, 0, 2)] // Width below 1
	[Arguments(1, 1, 4, 0)] // Height below 1
	[Arguments(6, 1, 4, 2)] // X + Width - 1 exceeds the 8-column grid
	public async Task Handle_ShouldThrow_WhenPlacementIsOutOfBounds(
		int x, int y, int width, int height, CancellationToken cancellationToken)
	{
		var command = new SaveDashboardLayoutCommand(
			DefaultOrgId,
			DefaultRequestingUserId,
			[new DashboardWidgetPlacementInput("ToDo", x, y, width, height)]);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*invalid grid placement*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenYExceedsMaxRows(
		CancellationToken cancellationToken)
	{
		var command = new SaveDashboardLayoutCommand(
			DefaultOrgId,
			DefaultRequestingUserId,
			// Y + Height - 1 = MaxRows + 1, one past the ceiling.
			[new DashboardWidgetPlacementInput("ToDo", 1, DashboardGrid.MaxRows, 4, 2)]);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*invalid grid placement*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenXAndWidthWouldOverflowInt32InTheBoundsCheck(
		CancellationToken cancellationToken)
	{
		// Regression guard: X + Width - 1 computed in plain int arithmetic
		// wraps around for a large enough X (int.MaxValue + a positive width
		// overflows past int.MaxValue back into negative territory), which
		// would wrongly pass a "> DashboardGrid.Columns" check done entirely
		// in int - the handler must widen to long before adding.
		var command = new SaveDashboardLayoutCommand(
			DefaultOrgId,
			DefaultRequestingUserId,
			[new DashboardWidgetPlacementInput("ToDo", int.MaxValue - 1, 1, 10, 2)]);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*invalid grid placement*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenWidgetsOverlap(
		CancellationToken cancellationToken)
	{
		var command = new SaveDashboardLayoutCommand(
			DefaultOrgId,
			DefaultRequestingUserId,
			[
				new DashboardWidgetPlacementInput("ToDo", 1, 1, 4, 2),
				new DashboardWidgetPlacementInput("Calendar", 3, 1, 4, 2),
			]);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*overlap*");
	}

	[Test]
	public async Task Handle_ShouldSucceed_WhenWidgetsShareAnEdgeButDoNotOverlap(
		CancellationToken cancellationToken)
	{
		// Regression guard for an off-by-one in the overlap check: two widgets
		// placed edge-to-edge (one starting exactly where the other ends) must
		// be accepted, not rejected as overlapping.
		var command = new SaveDashboardLayoutCommand(
			DefaultOrgId,
			DefaultRequestingUserId,
			[
				new DashboardWidgetPlacementInput("ToDo", 1, 1, 4, 2),
				new DashboardWidgetPlacementInput("Calendar", 5, 1, 4, 2),
			]);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldCreateNewLayout_WhenNoneExists(
		CancellationToken cancellationToken)
	{
		var command = new SaveDashboardLayoutCommand(DefaultOrgId, DefaultRequestingUserId, ValidWidgets);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		await _layoutRepo.Received(1).AddAsync(
			Arg.Is<OrganizationDashboardLayout>(l =>
				l!.OrganizationId == OrganizationId.Create(DefaultOrgId).GetValueOrThrow() &&
				l.UserId == DefaultRequestingUserId &&
				l.Widgets.Count == 2),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCreateEmptyLayout_WhenWidgetsListIsEmpty(
		CancellationToken cancellationToken)
	{
		// Regression guard for #771 review feedback (see the mirrored test on
		// the Get side, GetDashboardLayoutQueryHandlerTests): saving a
		// deliberately emptied layout must persist a real (empty) layout row,
		// not be silently skipped - that's what lets HasCustomLayout later
		// distinguish "never customized" from "customized to empty".
		var command = new SaveDashboardLayoutCommand(DefaultOrgId, DefaultRequestingUserId, []);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		await _layoutRepo.Received(1).AddAsync(
			Arg.Is<OrganizationDashboardLayout>(l =>
				l!.OrganizationId == OrganizationId.Create(DefaultOrgId).GetValueOrThrow() &&
				l.UserId == DefaultRequestingUserId &&
				l.Widgets.Count == 0),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldReplaceWidgetsWithEmptyList_WhenLayoutAlreadyExists(
		CancellationToken cancellationToken)
	{
		var existing = OrganizationDashboardLayout.Create(
			OrganizationId.Create(DefaultOrgId).GetValueOrThrow(),
			DefaultRequestingUserId,
			[new DashboardWidgetPlacement(DashboardWidgetKey.Settings, 1, 1, 8, 2)]);
		_dbContext
			.GetDashboardLayoutAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns(existing);
		var command = new SaveDashboardLayoutCommand(DefaultOrgId, DefaultRequestingUserId, []);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		existing.Widgets.Should().BeEmpty();
		await _layoutRepo.DidNotReceive().AddAsync(Arg.Any<OrganizationDashboardLayout>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldReplaceWidgets_WhenLayoutAlreadyExists(
		CancellationToken cancellationToken)
	{
		var existing = OrganizationDashboardLayout.Create(
			OrganizationId.Create(DefaultOrgId).GetValueOrThrow(),
			DefaultRequestingUserId,
			[new DashboardWidgetPlacement(DashboardWidgetKey.Settings, 1, 1, 8, 2)]);
		_dbContext
			.GetDashboardLayoutAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns(existing);
		var command = new SaveDashboardLayoutCommand(DefaultOrgId, DefaultRequestingUserId, ValidWidgets);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		existing.Widgets.Should().BeEquivalentTo(
		[
			new DashboardWidgetPlacement(DashboardWidgetKey.ToDo, 1, 1, 4, 2),
			new DashboardWidgetPlacement(DashboardWidgetKey.Calendar, 1, 3, 8, 6),
		], options => options.WithStrictOrdering());
		await _layoutRepo.DidNotReceive().AddAsync(Arg.Any<OrganizationDashboardLayout>(), Arg.Any<CancellationToken>());
	}
}
