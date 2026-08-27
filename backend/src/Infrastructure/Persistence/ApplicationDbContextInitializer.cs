using Application.Achievements.BadgeCatalog;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Domain.Achievements;
using Domain.Common;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

internal sealed class ApplicationDbContextInitializer(
	ApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService,
	IPinGenerator pinGenerator,
	IBadgeCatalogService badgeCatalogService,
	ILogger<ApplicationDbContextInitializer> logger)
	: IApplicationDbContextInitializer
{
	private static readonly Guid OlafId = new("00000000-0000-0000-0000-000000000001");
	private static readonly Guid VeraId = new("00000000-0000-0000-0000-000000000002");

	public async ValueTask MigrateAsync(
		CancellationToken cancellationToken = default)
	{
		const int maxAttempts = 5;
		for (var attempt = 1; attempt <= maxAttempts; attempt++)
		{
			try
			{
				await dbContext.Database.MigrateAsync(cancellationToken);
				return;
			}
			catch (Exception ex) when (attempt < maxAttempts)
			{
				logger.LogWarning(
					ex,
					"Database migration attempt {Attempt}/{Max} failed, retrying in {Delay}s...",
					attempt,
					maxAttempts,
					attempt * 3);
				await Task.Delay(TimeSpan.FromSeconds(attempt * 3), cancellationToken);
			}
		}
	}

	public async ValueTask SeedAsync(
		CancellationToken cancellationToken = default)
	{
		var existingOrganizations = await dbContext.Set<Organization>().CountAsync(cancellationToken);
		if (existingOrganizations > 0)
		{
			logger.LogWarning(
				"Seeding skipped: {OrganizationCount} organization(s) already exist, so the current seed set has NOT "
				+ "been applied and this environment is serving whatever it was first seeded with. Wipe the database "
				+ "to pick up a changed seed set.",
				existingOrganizations);
			return;
		}

		var org1Id = await SeedOrg1Async(cancellationToken);
		var org2Id = await SeedOrg2Async(cancellationToken);

		var now = DateTimeOffset.UtcNow;

		var opp1 = VolunteerOpportunity.Create(
			org1Id,
			"Erste-Hilfe-Kurs",
			"First Aid Course",
			"Lerne lebensrettende Sofortmaßnahmen in unserem eintägigen Praxiskurs.",
			"Learn life-saving emergency techniques in our one-day hands-on course.",
			isRemote: false,
			Address.Create("Karl-Heine-Straße", "12", "04177", "Leipzig").GetValueOrThrow(),
			Occurrence.OneTime,
			ParticipationType.ScheduledSlots,
			CheckInMethod.Manual,
			pinGenerator,
			category: Category.Health,
			status: OpportunityStatus.Draft).GetValueOrThrow();
		opp1.AddTimeSlot(DayAt(now, 14, 9), DayAt(now, 14, 17), 20, now).GetValueOrThrow();

		var opp1PastSlotStart = DayAt(now, -3, 9);
		var opp1PastSlotEnd = DayAt(now, -3, 17);
		var opp1PastSlot = opp1.AddTimeSlot(opp1PastSlotStart, opp1PastSlotEnd, 20, opp1PastSlotStart.AddDays(-1)).GetValueOrThrow();
		opp1.Publish().ThrowIfFailure();

		var opp2 = VolunteerOpportunity.Create(
			org1Id,
			"Blutspendetermin begleiten",
			"Support a Blood Donation Drive",
			"Unterstütze unseren regelmäßigen Blutspendetermin und hilf mit, Leben zu retten.",
			"Help out at our regular blood donation drive and help save lives.",
			isRemote: false,
			Address.Create("Rathausplatz", "1", "04416", "Markkleeberg").GetValueOrThrow(),
			Occurrence.Recurring,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			pinGenerator,
			category: Category.Health,
			validUntil: now.AddDays(60)).GetValueOrThrow();

		var opp2b = VolunteerOpportunity.Create(
			org1Id,
			"Sanitätsdienst beim Stadtteilfest",
			"First Aid Team at the Neighborhood Festival",
			"Verstärke unser Sanitätsteam beim Stadtteilfest und leiste Erste Hilfe vor Ort.",
			"Join our first aid team at the neighborhood festival and provide on-site first aid.",
			isRemote: false,
			Address.Create("Lindenauer Markt", "5", "04177", "Leipzig").GetValueOrThrow(),
			Occurrence.OneTime,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			pinGenerator,
			category: Category.Health,
			validUntil: now.AddDays(30)).GetValueOrThrow();

		var opp2c = VolunteerOpportunity.Create(
			org1Id,
			"Kleiderausgabe für Bedürftige",
			"Clothing Distribution for Those in Need",
			"Hilf mit, gespendete Kleidung zu sortieren und an Menschen in der Region auszugeben.",
			"Help sort donated clothing and hand it out to people in the region.",
			isRemote: false,
			Address.Create("Lagerstraße", "10", "06108", "Halle (Saale)").GetValueOrThrow(),
			Occurrence.Recurring,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			pinGenerator,
			category: Category.Social,
			validUntil: now.AddDays(90)).GetValueOrThrow();

		var opp2d = VolunteerOpportunity.Create(
			org1Id,
			"Erste-Hilfe-Schulung für Vereine",
			"First Aid Training for Clubs",
			"Vermittle Vereinen und Gruppen online die Grundlagen der Ersten Hilfe.",
			"Teach clubs and groups the basics of first aid online.",
			isRemote: true,
			address: null,
			Occurrence.Recurring,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			pinGenerator,
			category: Category.Education,
			validUntil: now.AddDays(45)).GetValueOrThrow();

		var opp3 = VolunteerOpportunity.Create(
			org2Id,
			"Helfer:innen für das Tierheim",
			"Volunteers for the Animal Shelter",
			"Hilf uns dabei, die Tiere in unserem Tierheim zu versorgen und zu betreuen.",
			"Help us care for and look after the animals in our shelter.",
			isRemote: false,
			Address.Create("Tierparkweg", "5", "04177", "Leipzig").GetValueOrThrow(),
			Occurrence.Recurring,
			ParticipationType.ScheduledSlots,
			CheckInMethod.QRCode,
			pinGenerator,
			category: Category.Animals,
			status: OpportunityStatus.Draft).GetValueOrThrow();
		opp3.AddTimeSlot(DayAt(now, 7, 10), DayAt(now, 7, 14), 5, now).GetValueOrThrow();

		opp3.AddTimeSlot(DayAt(now, 21, 10), DayAt(now, 21, 14), null, now).GetValueOrThrow();
		opp3.Publish().ThrowIfFailure();

		var opp4 = VolunteerOpportunity.Create(
			org2Id,
			"Online-Fundraising unterstützen",
			"Support Online Fundraising",
			"Unterstütze unser Fundraising-Team bequem von zu Hause aus.",
			"Support our fundraising team comfortably from home.",
			isRemote: true,
			address: null,
			Occurrence.OneTime,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			pinGenerator,
			category: Category.Social,
			validUntil: now.AddDays(30)).GetValueOrThrow();

		var opp4b = VolunteerOpportunity.Create(
			org2Id,
			"Gassi-Dienst für Tierheimhunde",
			"Dog Walking for Shelter Dogs",
			"Geh regelmäßig mit unseren Tierheimhunden spazieren und gib ihnen Auslauf.",
			"Take our shelter dogs for regular walks and give them some exercise.",
			isRemote: false,
			Address.Create("Tierparkweg", "5", "04177", "Leipzig").GetValueOrThrow(),
			Occurrence.Recurring,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			pinGenerator,
			category: Category.Animals,
			validUntil: now.AddDays(60)).GetValueOrThrow();

		var opp4c = VolunteerOpportunity.Create(
			org2Id,
			"Futterspenden-Sammlung",
			"Pet Food Donation Drive",
			"Sammle Futter- und Sachspenden für unsere Tiere bei einer Aktion vor Ort.",
			"Collect food and supply donations for our animals at an on-site event.",
			isRemote: false,
			Address.Create("Rathausplatz", "3", "04416", "Markkleeberg").GetValueOrThrow(),
			Occurrence.OneTime,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			pinGenerator,
			category: Category.Animals,
			validUntil: now.AddDays(30)).GetValueOrThrow();

		var opp4d = VolunteerOpportunity.Create(
			org2Id,
			"Patenschaft für Pflegetiere",
			"Foster Animal Sponsorship",
			"Übernimm eine Patenschaft für ein Pflegetier und begleite es bis zur Vermittlung.",
			"Sponsor a foster animal and support it until it finds a new home.",
			isRemote: true,
			address: null,
			Occurrence.Recurring,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			pinGenerator,
			category: Category.Animals,
			validUntil: now.AddDays(90)).GetValueOrThrow();

		dbContext.Set<VolunteerOpportunity>().AddRange(
			opp1, opp2, opp2b, opp2c, opp2d, opp3, opp4, opp4b, opp4c, opp4d);

		var veraUserId = UserId.Create(VeraId).GetValueOrThrow();

		var opp1PastEngagement = Engagement.CreateSlotSignUp(opp1.Id, veraUserId, opp1PastSlot.Id);
		opp1PastEngagement.Confirm().ThrowIfFailure();
		opp1PastEngagement.CheckIn(now).ThrowIfFailure();

		dbContext.Set<Engagement>().AddRange(
			Engagement.CreateSlotSignUp(opp1.Id, veraUserId, opp1.TimeSlots.First().Id),
			opp1PastEngagement,
			Engagement.CreateIndividualContact(
				opp2.Id,
				veraUserId,
				"Ich würde beim nächsten Blutspendetermin gerne als Freiwillige mithelfen.").GetValueOrThrow(),
			Engagement.CreateSlotSignUp(opp3.Id, veraUserId, opp3.TimeSlots.First().Id));

		await SeedFirstStepAchievementAsync(veraUserId, cancellationToken);

		await dbContext.SaveChangesAsync(cancellationToken);
	}

	// opp1PastEngagement above is seeded directly as Confirmed rather than through
	// ConfirmEngagementCommandHandler, so it must also seed the streak/achievement
	// state that handler would have produced - otherwise the profile shows 100%
	// progress toward "first-step" while the achievement was never granted (#2229).
	private async Task SeedFirstStepAchievementAsync(
		UserId volunteerId,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var streak = await dbContext.GetOrCreateUserStreakAsync(volunteerId, cancellationToken);
		streak.RecordActivity(
			System.Globalization.ISOWeek.GetYear(now.UtcDateTime),
			System.Globalization.ISOWeek.GetWeekOfYear(now.UtcDateTime));
		streak.RecordConfirmedEngagement();

		var definition = badgeCatalogService.FindByKey("first-step");
		if (definition is null)
			return;

		var achievement = Achievement.Create(
			volunteerId,
			definition.Type,
			definition.Key,
			definition.Name,
			definition.Description,
			now);

		await dbContext.TryAwardAchievementAsync(achievement, cancellationToken);
	}

	private static DateTimeOffset DayAt(DateTimeOffset from, int daysAhead, int hourUtc) =>
		new DateTimeOffset(from.UtcDateTime.Date, TimeSpan.Zero)
			.AddDays(daysAhead)
			.AddHours(hourUtc);

	private async Task<OrganizationId> SeedOrg1Async(CancellationToken cancellationToken)
	{
		const string name = "Lindenauer Nachbarschaftshilfe e.V.";

		var keycloakId = await keycloakOrganizationService.FindOrganizationByNameAsync(name, cancellationToken)
			?? await keycloakOrganizationService.CreateOrganizationAsync(name, cancellationToken);

		await EnsureMemberAsync(keycloakId, OlafId, cancellationToken);
		await EnsureOrganizerRoleAsync(OlafId, cancellationToken);
		await EnsureMemberAsync(keycloakId, VeraId, cancellationToken);

		var org = Organization.Create(OrganizationId.Create(keycloakId).GetValueOrThrow(), name)
			.GetValueOrThrow();
		org.ChangeDescription(
			"Wir unterstützen Menschen in Leipzig und Umgebung - von der Nachbarschaftshilfe bis zum Sanitätsdienst.");
		org.ChangeContactInfo(
			"info@nachbarschaftshilfe-lindenau.example",
			"+49 341 1234560",
			"https://www.nachbarschaftshilfe-lindenau.example");
		org.Relocate(Address.Create("Karl-Heine-Straße", "12", "04177", "Leipzig").GetValueOrThrow());

		dbContext.Set<Organization>().Add(org);

		dbContext.Set<OrganizationMembership>().Add(
			OrganizationMembership.Create(org.Id, UserId.Create(OlafId).GetValueOrThrow(), OrganizationMemberRole.Organizer));
		dbContext.Set<OrganizationMembership>().Add(
			OrganizationMembership.Create(org.Id, UserId.Create(VeraId).GetValueOrThrow(), OrganizationMemberRole.Member));

		return org.Id;
	}

	private async Task<OrganizationId> SeedOrg2Async(CancellationToken cancellationToken)
	{
		const string name = "Lindenauer Tierschutzverein e.V.";

		var keycloakId = await keycloakOrganizationService.FindOrganizationByNameAsync(name, cancellationToken)
			?? await keycloakOrganizationService.CreateOrganizationAsync(name, cancellationToken);

		await EnsureMemberAsync(keycloakId, OlafId, cancellationToken);
		await EnsureOrganizerRoleAsync(OlafId, cancellationToken);

		var org = Organization.Create(OrganizationId.Create(keycloakId).GetValueOrThrow(), name)
			.GetValueOrThrow();
		org.ChangeDescription("Wir setzen uns für das Wohl der Tiere in Leipzig und Umgebung ein.");
		org.ChangeContactInfo("info@tierschutz-lindenau.example", "+49 341 1234561", null);
		org.Relocate(Address.Create("Tierparkweg", "5", "04177", "Leipzig").GetValueOrThrow());

		dbContext.Set<Organization>().Add(org);

		dbContext.Set<OrganizationMembership>().Add(
			OrganizationMembership.Create(org.Id, UserId.Create(OlafId).GetValueOrThrow(), OrganizationMemberRole.Organizer));

		return org.Id;
	}

	private async Task EnsureMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
	{
		var members = await keycloakOrganizationService.GetMembersAsync(organizationId, cancellationToken);
		if (members.Any(m => m.UserId == userId))
			return;

		await keycloakOrganizationService.AddMemberAsync(organizationId, userId, cancellationToken);
	}

	private async Task EnsureOrganizerRoleAsync(Guid userId, CancellationToken cancellationToken)
	{
		var organisatorIds = await keycloakOrganizationService.GetRealmOrganisatorUserIdsAsync(cancellationToken);
		if (organisatorIds.Contains(userId))
			return;

		await keycloakOrganizationService.AssignOrganizerRoleAsync(userId, cancellationToken);
	}
}
