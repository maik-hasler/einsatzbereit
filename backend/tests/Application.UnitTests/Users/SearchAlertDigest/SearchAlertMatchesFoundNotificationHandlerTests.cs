using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Users.SearchAlertDigest.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Notifications;
using Domain.Organizations;
using Domain.SearchAlerts;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.UnitTests.Users.SearchAlertDigest;

public class SearchAlertMatchesFoundNotificationHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IAggregateRepository<Notification, NotificationId> _notificationRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly SearchAlertMatchesFoundNotificationHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;
	private static readonly IPinGenerator PinGenerator = Substitute.For<IPinGenerator>();

	public SearchAlertMatchesFoundNotificationHandlerTests()
	{
		_dbContext.Notifications.Returns(_notificationRepo);
		_dbContext
			.GetVolunteerOpportunitiesByIdsAsync(Arg.Any<IReadOnlyCollection<VolunteerOpportunityId>>(), Arg.Any<CancellationToken>())
			.Returns([CreateOpportunity()]);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "user", null, null, "user@example.com"));
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(new EmailContent("Test Subject", "Test Body"));
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns(call => ((IReadOnlyCollection<UserId>)call[0]!).Select(User.Create).ToList());
		_emailService
			.SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), Arg.Any<CancellationToken>())
			.Returns([true]);
		_sut = new SearchAlertMatchesFoundNotificationHandler(
			_dbContext, _unitOfWork, _keycloakUserService, _emailService, _emailTemplateRenderer,
			NullLogger<SearchAlertMatchesFoundNotificationHandler>.Instance);
	}

	private static VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Test Opportunity", "Test", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Published, validUntil: DateTimeOffset.UtcNow.AddDays(14)).Value;

	[Test]
	public async Task Handle_ShouldCreateOneNotificationPerMatchedOpportunity(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		_dbContext
			.GetVolunteerOpportunitiesByIdsAsync(Arg.Any<IReadOnlyCollection<VolunteerOpportunityId>>(), cancellationToken)
			.Returns([opportunity]);
		var recipientId = UserId.New();
		var notification = new SearchAlertMatchesFoundDomainEvent(SearchAlertId.New(), recipientId, [opportunity.Id.Value]);

		await _sut.Handle(notification, cancellationToken);

		await _notificationRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.RecipientId == recipientId &&
				n.Kind == NotificationKind.NewMatchingOpportunity &&
				n.RelatedEntityId == opportunity.Id.Value),
			cancellationToken);
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldSendOneDigestEmail_ForMultipleMatches(
		CancellationToken cancellationToken)
	{
		var first = CreateOpportunity();
		var second = CreateOpportunity();
		_dbContext
			.GetVolunteerOpportunitiesByIdsAsync(Arg.Any<IReadOnlyCollection<VolunteerOpportunityId>>(), cancellationToken)
			.Returns([first, second]);
		var notification = new SearchAlertMatchesFoundDomainEvent(
			SearchAlertId.New(), UserId.New(), [first.Id.Value, second.Id.Value]);

		await _sut.Handle(notification, cancellationToken);

		await _emailService.Received(1).SendBatchAsync(
			Arg.Is<IReadOnlyList<EmailMessage>>(m => m!.Count == 1 && m[0].To == "user@example.com"),
			cancellationToken);
		await _notificationRepo.Received(2).AddAsync(Arg.Any<Notification>(), cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldSkip_WhenNoneOfTheMatchedOpportunitiesStillExist(
		CancellationToken cancellationToken)
	{
		_dbContext
			.GetVolunteerOpportunitiesByIdsAsync(Arg.Any<IReadOnlyCollection<VolunteerOpportunityId>>(), cancellationToken)
			.Returns([]);
		var notification = new SearchAlertMatchesFoundDomainEvent(SearchAlertId.New(), UserId.New(), [Guid.NewGuid()]);

		await _sut.Handle(notification, cancellationToken);

		await _notificationRepo.DidNotReceive().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
		await _emailService.DidNotReceive().SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrowAndNotPersistNotifications_WhenEmailSendFails(
		CancellationToken cancellationToken)
	{
		// Regression guard: the email send must be checked before any
		// Notification rows are added, so a failed send (retried by the
		// outbox) doesn't leave duplicate in-app rows behind on a later
		// successful retry.
		var opportunity = CreateOpportunity();
		_dbContext
			.GetVolunteerOpportunitiesByIdsAsync(Arg.Any<IReadOnlyCollection<VolunteerOpportunityId>>(), cancellationToken)
			.Returns([opportunity]);
		_emailService
			.SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), cancellationToken)
			.Returns([false]);
		var notification = new SearchAlertMatchesFoundDomainEvent(SearchAlertId.New(), UserId.New(), [opportunity.Id.Value]);

		var act = async () => await _sut.Handle(notification, cancellationToken);

		await act.Should().ThrowAsync<InvalidOperationException>();
		await _notificationRepo.DidNotReceive().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
	}
}
