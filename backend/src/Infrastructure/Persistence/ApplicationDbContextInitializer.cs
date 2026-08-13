using Application.Common.Exceptions;
using Application.Common.Keycloak;
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

	// Exceptions are intentionally left to propagate (#1212) - the caller (Program.cs)
	// decides whether that means logging and continuing (Development) or failing
	// startup outright (everywhere else). Silently swallowing here used to mean a
	// SaveChangesAsync failure after the Keycloak-dependent seeding below had already
	// run left orphaned Keycloak organizations with nothing pointing at them locally,
	// and re-seeding on the next boot (the empty-database guard below still sees an
	// empty table) made it worse by risking creating a *second* orphaned set. SeedOrg1Async/
	// SeedOrg2Async now look up an existing organization by name before creating one,
	// so a retry after a partial failure reuses what already exists instead.
	public async ValueTask SeedAsync(
		CancellationToken cancellationToken = default)
	{
		// Skipping is still the right behavior - re-seeding a populated database
		// would have to delete rows that are no longer demo data - but it is no
		// longer silent (#1776). Staging kept serving an English demo data set for
		// months after the seed set was translated to German, because this guard
		// trips on every restart of a long-lived environment and nothing anywhere
		// said the seed set had not been applied. The only way to pick up a changed
		// seed set is to wipe the database (staging: reset-staging.yml), so the one
		// signal that makes that decidable is this log line.
		var existingOrganizations = await dbContext.Set<Organization>().CountAsync(cancellationToken);
		if (existingOrganizations > 0)
		{
			logger.LogWarning(
				"Seeding skipped: {OrganizationCount} organization(s) already exist, so the current seed set has NOT "
				+ "been applied and this environment is serving whatever it was first seeded with. Wipe the database "
				+ "to pick up a changed seed set (staging: .github/workflows/reset-staging.yml).",
				existingOrganizations);
			return;
		}

		var org1Id = await SeedOrg1Async(cancellationToken);
		var org2Id = await SeedOrg2Async(cancellationToken);

		var now = DateTimeOffset.UtcNow;

		var opp1 = VolunteerOpportunity.Create(
			org1Id,
			"Erste-Hilfe-Kurs",
			"Lerne lebensrettende Sofortmaßnahmen in unserem eintägigen Praxiskurs.",
			isRemote: false,
			Address.Create("Karl-Heine-Straße", "12", "04177", "Leipzig").GetValueOrThrow(),
			Occurrence.OneTime,
			ParticipationType.ScheduledSlots,
			CheckInMethod.Manual,
			pinGenerator,
			category: Category.Health,
			status: OpportunityStatus.Draft).GetValueOrThrow();
		opp1.AddTimeSlot(DayAt(now, 14, 9), DayAt(now, 14, 17), 20, now).GetValueOrThrow();
		opp1.Publish().ThrowIfFailure();

		var opp2 = VolunteerOpportunity.Create(
			org1Id,
			"Blutspendetermin begleiten",
			"Unterstütze unseren regelmäßigen Blutspendetermin und hilf mit, Leben zu retten.",
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
			"Verstärke unser Sanitätsteam beim Stadtteilfest und leiste Erste Hilfe vor Ort.",
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
			"Hilf mit, gespendete Kleidung zu sortieren und an Menschen in der Region auszugeben.",
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
			"Vermittle Vereinen und Gruppen online die Grundlagen der Ersten Hilfe.",
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
			"Hilf uns dabei, die Tiere in unserem Tierheim zu versorgen und zu betreuen.",
			isRemote: false,
			Address.Create("Tierparkweg", "5", "04177", "Leipzig").GetValueOrThrow(),
			Occurrence.Recurring,
			ParticipationType.ScheduledSlots,
			CheckInMethod.QRCode,
			pinGenerator,
			category: Category.Animals,
			status: OpportunityStatus.Draft).GetValueOrThrow();
		opp3.AddTimeSlot(DayAt(now, 7, 10), DayAt(now, 7, 14), 5, now).GetValueOrThrow();
		// Unlimited capacity - demonstrates the "no cap" option locally (#1066).
		opp3.AddTimeSlot(DayAt(now, 21, 10), DayAt(now, 21, 14), null, now).GetValueOrThrow();
		opp3.Publish().ThrowIfFailure();

		var opp4 = VolunteerOpportunity.Create(
			org2Id,
			"Online-Fundraising unterstützen",
			"Unterstütze unser Fundraising-Team bequem von zu Hause aus.",
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
			"Geh regelmäßig mit unseren Tierheimhunden spazieren und gib ihnen Auslauf.",
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
			"Sammle Futter- und Sachspenden für unsere Tiere bei einer Aktion vor Ort.",
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
			"Übernimm eine Patenschaft für ein Pflegetier und begleite es bis zur Vermittlung.",
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

		dbContext.Set<Engagement>().AddRange(
			Engagement.CreateSlotSignUp(opp1.Id, veraUserId, opp1.TimeSlots.First().Id),
			Engagement.CreateIndividualContact(
				opp2.Id,
				veraUserId,
				"Ich würde beim nächsten Blutspendetermin gerne als Freiwillige mithelfen.").GetValueOrThrow(),
			Engagement.CreateSlotSignUp(opp3.Id, veraUserId, opp3.TimeSlots.First().Id));

		await dbContext.SaveChangesAsync(cancellationToken);
	}

	// Slot times are pinned to a fixed hour of day instead of inheriting
	// DateTimeOffset.UtcNow's time-of-day. Otherwise every seeded shift starts at
	// whatever o'clock the seeder happened to run - staging ended up advertising
	// 23:05-03:05 "shifts" across the whole demo data set, which reads as a bug
	// rather than as sample content.
	private static DateTimeOffset DayAt(DateTimeOffset from, int daysAhead, int hourUtc) =>
		new DateTimeOffset(from.UtcDateTime.Date, TimeSpan.Zero)
			.AddDays(daysAhead)
			.AddHours(hourUtc);

	private async Task<OrganizationId> SeedOrg1Async(CancellationToken cancellationToken)
	{
		// Both seeded orgs deliberately share a leading word and first letter -
		// OrgAppMobileResponsiveTests (#809) covers switcher truncation for exactly
		// that case, so renaming these apart would silently defeat that test.
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

	// Checked-before-added rather than a bare AddMemberAsync call, so re-running
	// seeding after a partial failure (the organization was already reused via
	// FindOrganizationByNameAsync, but a prior attempt had already added this member
	// too) doesn't depend on Keycloak's add-member endpoint tolerating a duplicate
	// add - we just never call it a second time for the same member.
	private async Task EnsureMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
	{
		var members = await keycloakOrganizationService.GetMembersAsync(organizationId, cancellationToken);
		if (members.Any(m => m.UserId == userId))
			return;

		await keycloakOrganizationService.AddMemberAsync(organizationId, userId, cancellationToken);
	}

	// Same reasoning as EnsureMemberAsync above, for the realm-wide organisator role.
	private async Task EnsureOrganizerRoleAsync(Guid userId, CancellationToken cancellationToken)
	{
		var organisatorIds = await keycloakOrganizationService.GetRealmOrganisatorUserIdsAsync(cancellationToken);
		if (organisatorIds.Contains(userId))
			return;

		await keycloakOrganizationService.AssignOrganizerRoleAsync(userId, cancellationToken);
	}
}
