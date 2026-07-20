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
		new DashboardWidgetPlacementInput("ToDo", "Medium"),
		new DashboardWidgetPlacementInput("Calendar", "Large"),
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
			DefaultOrgId, DefaultRequestingUserId, [new DashboardWidgetPlacementInput("NotAWidget", "Medium")]);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*widget key*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenSizeIsInvalid(
		CancellationToken cancellationToken)
	{
		var command = new SaveDashboardLayoutCommand(
			DefaultOrgId, DefaultRequestingUserId, [new DashboardWidgetPlacementInput("ToDo", "Huge")]);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*widget size*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenWidgetKeyIsDuplicated(
		CancellationToken cancellationToken)
	{
		var command = new SaveDashboardLayoutCommand(
			DefaultOrgId,
			DefaultRequestingUserId,
			[
				new DashboardWidgetPlacementInput("ToDo", "Medium"),
				new DashboardWidgetPlacementInput("ToDo", "Large"),
			]);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*more than once*");
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
	public async Task Handle_ShouldReplaceWidgets_WhenLayoutAlreadyExists(
		CancellationToken cancellationToken)
	{
		var existing = OrganizationDashboardLayout.Create(
			OrganizationId.Create(DefaultOrgId).GetValueOrThrow(),
			DefaultRequestingUserId,
			[new DashboardWidgetPlacement(DashboardWidgetKey.Settings, DashboardWidgetSize.Large)]);
		_dbContext
			.GetDashboardLayoutAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns(existing);
		var command = new SaveDashboardLayoutCommand(DefaultOrgId, DefaultRequestingUserId, ValidWidgets);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		existing.Widgets.Should().BeEquivalentTo(
		[
			new DashboardWidgetPlacement(DashboardWidgetKey.ToDo, DashboardWidgetSize.Medium),
			new DashboardWidgetPlacement(DashboardWidgetKey.Calendar, DashboardWidgetSize.Large),
		], options => options.WithStrictOrdering());
		await _layoutRepo.DidNotReceive().AddAsync(Arg.Any<OrganizationDashboardLayout>(), Arg.Any<CancellationToken>());
	}
}
