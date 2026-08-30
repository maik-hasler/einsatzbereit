using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Organizations.ResetDashboardLayout.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Organizations.ResetDashboardLayout;

public class ResetDashboardLayoutCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Organization, OrganizationId> _orgRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly IAggregateRepository<OrganizationDashboardLayout, OrganizationDashboardLayoutId> _layoutRepo =
		Substitute.For<IAggregateRepository<OrganizationDashboardLayout, OrganizationDashboardLayoutId>>();
	private readonly ResetDashboardLayoutCommandHandler _sut;

	private static readonly Guid DefaultOrgId = Guid.NewGuid();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	private static OrganizationDashboardLayout SavedLayout() =>
		OrganizationDashboardLayout.Create(
			OrganizationId.Create(DefaultOrgId).GetValueOrThrow(),
			DefaultRequestingUserId,
			[new DashboardWidgetPlacement(DashboardWidgetKey.ToDo, 1, 1, 4, 2)]);

	public ResetDashboardLayoutCommandHandlerTests()
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
			.Returns(SavedLayout());
		_sut = new ResetDashboardLayoutCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOrganizationNotFound(
		CancellationToken cancellationToken)
	{
		_orgRepo
			.FindAsync(OrganizationId.Create(DefaultOrgId).GetValueOrThrow(), cancellationToken)
			.Returns((Organization?)null);
		var command = new ResetDashboardLayoutCommand(DefaultOrgId, DefaultRequestingUserId);

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
		var command = new ResetDashboardLayoutCommand(DefaultOrgId, DefaultRequestingUserId);

		var act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*permission*");
	}

	[Test]
	public async Task Handle_ShouldDeleteTheRequestingUsersSavedLayout(
		CancellationToken cancellationToken)
	{
		var layout = SavedLayout();
		_dbContext
			.GetDashboardLayoutAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns(layout);
		var command = new ResetDashboardLayoutCommand(DefaultOrgId, DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		_layoutRepo.Received(1).Delete(layout);
	}

	// Resetting a dashboard that is already on the default is the same
	// request with the same outcome - it must not 404 or throw.
	[Test]
	public async Task Handle_ShouldSucceedWithoutDeleting_WhenNoLayoutIsSaved(
		CancellationToken cancellationToken)
	{
		_dbContext
			.GetDashboardLayoutAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns((OrganizationDashboardLayout?)null);
		var command = new ResetDashboardLayoutCommand(DefaultOrgId, DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		_layoutRepo.DidNotReceive().Delete(Arg.Any<OrganizationDashboardLayout>());
	}
}
