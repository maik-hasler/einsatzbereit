using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.SearchAlerts;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.SearchAlerts;

public class SearchAlertTests
{
	private static readonly OrganizationId TestOrganizationId = OrganizationId.New();
	private static readonly UserId TestUserId = UserId.New();
	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
	private static readonly IPinGenerator PinGenerator = Substitute.For<IPinGenerator>();

	private const double BerlinLatitude = 52.5200;
	private const double BerlinLongitude = 13.4050;

	// Munich - roughly 500km from Berlin, well outside a 50km radius.
	private const double MunichLatitude = 48.1351;
	private const double MunichLongitude = 11.5820;

	private static VolunteerOpportunity CreateOpportunity(
		Occurrence occurrence = Occurrence.OneTime,
		ParticipationType participationType = ParticipationType.IndividualContact,
		bool isRemote = false,
		Category? category = null,
		IReadOnlyCollection<string>? tags = null,
		double? latitude = null,
		double? longitude = null)
	{
		Address? address = null;
		if (!isRemote)
		{
			address = Address.Create("Sample Street", "1", "12345", "Berlin").Value;
			if (latitude is double lat && longitude is double lon)
				address = address.WithCoordinates(lat, lon).Value;
		}

		return VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			isRemote,
			address,
			occurrence,
			participationType,
			CheckInMethod.None,
			PinGenerator,
			category: category,
			tags: tags,
			status: OpportunityStatus.Published,
			validUntil: participationType == ParticipationType.IndividualContact ? Now.AddDays(14) : null,
			now: Now).Value;
	}

	[Test]
	public void Matches_ShouldReturnTrue_WhenNoCriteriaSet()
	{
		var alert = SearchAlert.Create(TestUserId, null, null, null, null, null, null, now: Now);
		var opportunity = CreateOpportunity();

		alert.Matches(opportunity).Should().BeTrue();
	}

	[Test]
	public void Matches_ShouldReturnFalse_WhenOccurrenceDiffers()
	{
		var alert = SearchAlert.Create(TestUserId, Occurrence.Recurring, null, null, null, null, null, now: Now);
		var opportunity = CreateOpportunity(occurrence: Occurrence.OneTime);

		alert.Matches(opportunity).Should().BeFalse();
	}

	[Test]
	public void Matches_ShouldReturnFalse_WhenParticipationTypeDiffers()
	{
		var alert = SearchAlert.Create(TestUserId, null, ParticipationType.ScheduledSlots, null, null, null, null, now: Now);
		var opportunity = CreateOpportunity(participationType: ParticipationType.IndividualContact);

		alert.Matches(opportunity).Should().BeFalse();
	}

	[Test]
	public void Matches_ShouldReturnFalse_WhenIsRemoteDiffers()
	{
		var alert = SearchAlert.Create(TestUserId, null, null, true, null, null, null, now: Now);
		var opportunity = CreateOpportunity(isRemote: false);

		alert.Matches(opportunity).Should().BeFalse();
	}

	[Test]
	public void Matches_ShouldReturnFalse_WhenCategoryNotInList()
	{
		var alert = SearchAlert.Create(TestUserId, null, null, null, null, null, null, categories: ["Environment", "Health"], now: Now);
		var opportunity = CreateOpportunity(category: Category.Sport);

		alert.Matches(opportunity).Should().BeFalse();
	}

	[Test]
	public void Matches_ShouldReturnFalse_WhenCategoryListSetButOpportunityHasNoCategory()
	{
		var alert = SearchAlert.Create(TestUserId, null, null, null, null, null, null, categories: ["Environment"], now: Now);
		var opportunity = CreateOpportunity(category: null);

		alert.Matches(opportunity).Should().BeFalse();
	}

	[Test]
	public void Matches_ShouldReturnTrue_WhenCategoryInList()
	{
		var alert = SearchAlert.Create(TestUserId, null, null, null, null, null, null, categories: ["Environment", "Sport"], now: Now);
		var opportunity = CreateOpportunity(category: Category.Sport);

		alert.Matches(opportunity).Should().BeTrue();
	}

	[Test]
	public void Matches_ShouldReturnFalse_WhenTagMissing()
	{
		var alert = SearchAlert.Create(TestUserId, null, null, null, null, null, null, tag: "cleanup", now: Now);
		var opportunity = CreateOpportunity(tags: ["gardening"]);

		alert.Matches(opportunity).Should().BeFalse();
	}

	[Test]
	public void Matches_ShouldReturnTrue_WhenTagPresent()
	{
		var alert = SearchAlert.Create(TestUserId, null, null, null, null, null, null, tag: "cleanup", now: Now);
		var opportunity = CreateOpportunity(tags: ["gardening", "cleanup"]);

		alert.Matches(opportunity).Should().BeTrue();
	}

	[Test]
	public void Matches_ShouldReturnTrue_WhenWithinRadius()
	{
		var alert = SearchAlert.Create(TestUserId, null, null, null, BerlinLatitude, BerlinLongitude, 50, now: Now);
		var opportunity = CreateOpportunity(latitude: BerlinLatitude, longitude: BerlinLongitude);

		alert.Matches(opportunity).Should().BeTrue();
	}

	[Test]
	public void Matches_ShouldReturnFalse_WhenOutsideRadius()
	{
		var alert = SearchAlert.Create(TestUserId, null, null, null, BerlinLatitude, BerlinLongitude, 50, now: Now);
		var opportunity = CreateOpportunity(latitude: MunichLatitude, longitude: MunichLongitude);

		alert.Matches(opportunity).Should().BeFalse();
	}

	[Test]
	public void Matches_ShouldReturnFalse_WhenRadiusSetButOpportunityHasNoCoordinates()
	{
		var alert = SearchAlert.Create(TestUserId, null, null, null, BerlinLatitude, BerlinLongitude, 50, now: Now);
		var opportunity = CreateOpportunity();

		alert.Matches(opportunity).Should().BeFalse();
	}

	[Test]
	public void Create_ShouldSetLastNotifiedAtToNow()
	{
		var alert = SearchAlert.Create(TestUserId, null, null, null, null, null, null, now: Now);

		alert.LastNotifiedAt.Should().Be(Now);
	}

	[Test]
	public void ReplaceCriteria_ShouldResetLastNotifiedAt()
	{
		// Regression guard (#1090): re-saving an alert must not carry over the
		// old cursor - otherwise an opportunity published under the old (or
		// same) criteria before the re-save could be reported as "new" even
		// though it already existed when the user saved again.
		var alert = SearchAlert.Create(TestUserId, Occurrence.OneTime, null, null, null, null, null, now: Now);
		var later = Now.AddDays(1);

		alert.ReplaceCriteria(Occurrence.Recurring, null, null, null, null, null, null, null, later);

		alert.Occurrence.Should().Be(Occurrence.Recurring);
		alert.LastNotifiedAt.Should().Be(later);
	}
}
