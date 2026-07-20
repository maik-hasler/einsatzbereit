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

	public async ValueTask SeedAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			if (await dbContext.Set<Organization>().AnyAsync(cancellationToken))
				return;

			var org1Id = await SeedOrg1Async(cancellationToken);
			var org2Id = await SeedOrg2Async(cancellationToken);

			var now = DateTimeOffset.UtcNow;

			var opp1 = VolunteerOpportunity.Create(
				org1Id,
				"Erste Hilfe Kurs",
				"Lernen Sie lebensrettende Erste-Hilfe-Massnahmen in unserem praxisnahen Tageskurs.",
				isRemote: false,
				Address.Create("Hauptstrasse", "1", "12345", "Musterstadt").GetValueOrThrow(),
				Occurrence.OneTime,
				ParticipationType.Waitlist,
				CheckInMethod.Manual,
				pinGenerator,
				status: OpportunityStatus.Draft).GetValueOrThrow();
			opp1.AddTimeSlot(now.AddDays(14), now.AddDays(14).AddHours(8), 20, now).GetValueOrThrow();
			opp1.Publish().ThrowIfFailure();

			var opp2 = VolunteerOpportunity.Create(
				org1Id,
				"Blutspende-Aktion",
				"Unterstutzen Sie unsere regelmasige Blutspende-Aktion und helfen Sie, Leben zu retten.",
				isRemote: false,
				Address.Create("Rathausplatz", "1", "12345", "Musterstadt").GetValueOrThrow(),
				Occurrence.Recurring,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator).GetValueOrThrow();

			var opp2b = VolunteerOpportunity.Create(
				org1Id,
				"Sanitatsdienst bei Stadtfest",
				"Begleiten Sie unser Sanitatsteam beim jahrlichen Stadtfest und leisten Sie Erste Hilfe vor Ort.",
				isRemote: false,
				Address.Create("Marktplatz", "2", "12345", "Musterstadt").GetValueOrThrow(),
				Occurrence.OneTime,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator).GetValueOrThrow();

			var opp2c = VolunteerOpportunity.Create(
				org1Id,
				"Kleidersammlung fur Bedurftige",
				"Helfen Sie beim Sortieren und Verteilen gespendeter Kleidung an Bedurftige in der Region.",
				isRemote: false,
				Address.Create("Lagerstrasse", "10", "12345", "Musterstadt").GetValueOrThrow(),
				Occurrence.Recurring,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator).GetValueOrThrow();

			var opp2d = VolunteerOpportunity.Create(
				org1Id,
				"Erste-Hilfe-Schulung fur Vereine",
				"Vermitteln Sie Vereinen und Ehrenamtlichen die Grundlagen der Ersten Hilfe in Online-Schulungen.",
				isRemote: true,
				address: null,
				Occurrence.Recurring,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator).GetValueOrThrow();

			var opp3 = VolunteerOpportunity.Create(
				org2Id,
				"Tierheim Helfer gesucht",
				"Helfen Sie uns bei der Betreuung und Pflege der Tiere in unserem Tierheim.",
				isRemote: false,
				Address.Create("Tiergartenweg", "5", "12345", "Musterstadt").GetValueOrThrow(),
				Occurrence.Recurring,
				ParticipationType.Waitlist,
				CheckInMethod.QRCode,
				pinGenerator,
				status: OpportunityStatus.Draft).GetValueOrThrow();
			opp3.AddTimeSlot(now.AddDays(7), now.AddDays(7).AddHours(4), 5, now).GetValueOrThrow();
			opp3.AddTimeSlot(now.AddDays(21), now.AddDays(21).AddHours(4), 5, now).GetValueOrThrow();
			opp3.Publish().ThrowIfFailure();

			var opp4 = VolunteerOpportunity.Create(
				org2Id,
				"Online-Fundraising Unterstutzung",
				"Unterstutzen Sie unser Online-Fundraising-Team bequem von zu Hause aus.",
				isRemote: true,
				address: null,
				Occurrence.OneTime,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator).GetValueOrThrow();

			var opp4b = VolunteerOpportunity.Create(
				org2Id,
				"Gassi-Service fur Tierheim-Hunde",
				"Drehen Sie regelmasig Ihre Runden mit unseren Tierheim-Hunden und schenken Sie ihnen Auslauf.",
				isRemote: false,
				Address.Create("Tiergartenweg", "5", "12345", "Musterstadt").GetValueOrThrow(),
				Occurrence.Recurring,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator).GetValueOrThrow();

			var opp4c = VolunteerOpportunity.Create(
				org2Id,
				"Spendenaktion Tierfutter",
				"Sammeln Sie Futter- und Sachspenden fur unsere Schutzlinge bei einer lokalen Aktion.",
				isRemote: false,
				Address.Create("Feldweg", "3", "12345", "Musterstadt").GetValueOrThrow(),
				Occurrence.OneTime,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator).GetValueOrThrow();

			var opp4d = VolunteerOpportunity.Create(
				org2Id,
				"Patenschaften fur Pflegetiere",
				"Ubernehmen Sie eine Tierpatenschaft und begleiten Sie ein Pflegetier bei seiner Vermittlung.",
				isRemote: true,
				address: null,
				Occurrence.Recurring,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator).GetValueOrThrow();

			dbContext.Set<VolunteerOpportunity>().AddRange(
				opp1, opp2, opp2b, opp2c, opp2d, opp3, opp4, opp4b, opp4c, opp4d);

			var veraUserId = UserId.Create(VeraId).GetValueOrThrow();

			dbContext.Set<Engagement>().AddRange(
				Engagement.CreateWaitlistSignUp(opp1.Id, veraUserId, opp1.TimeSlots.First().Id),
				Engagement.CreateIndividualContact(
					opp2.Id,
					veraUserId,
					"Ich wuerde gerne bei der nachsten Blutspende-Aktion als Helfer dabei sein.").GetValueOrThrow(),
				Engagement.CreateWaitlistSignUp(opp3.Id, veraUserId, opp3.TimeSlots.First().Id));

			await dbContext.SaveChangesAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"An exception occurred while seeding the database");
		}
	}

	public async ValueTask BackfillOrganizationMembershipsAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			var backfilledOrganizationIds = await dbContext.Set<Organization>()
				.Where(o => !dbContext.Set<OrganizationMembership>().Any(m => m.OrganizationId == o.Id))
				.Select(o => o.Id)
				.ToListAsync(cancellationToken);

			if (backfilledOrganizationIds.Count == 0)
				return;

			foreach (var organizationId in backfilledOrganizationIds)
			{
				var members = await keycloakOrganizationService.GetMembersAsync(
					organizationId.Value, cancellationToken);

				foreach (var member in members.Where(m => m.IsOrganisator).DistinctBy(m => m.UserId))
				{
					dbContext.Set<OrganizationMembership>().Add(
						OrganizationMembership.Create(
							organizationId, UserId.Create(member.UserId).GetValueOrThrow(), OrganizationMemberRole.Organizer));
				}
			}

			await dbContext.SaveChangesAsync(cancellationToken);

			logger.LogInformation(
				"Backfilled organization_membership rows for {Count} pre-existing organization(s).",
				backfilledOrganizationIds.Count);
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"An exception occurred while backfilling organization memberships");
		}
	}

	private async Task<OrganizationId> SeedOrg1Async(CancellationToken cancellationToken)
	{
		var keycloakId = await keycloakOrganizationService.CreateOrganizationAsync(
			"Rotes Kreuz Musterstadt",
			cancellationToken);

		await keycloakOrganizationService.AddMemberAsync(keycloakId, OlafId, cancellationToken);
		await keycloakOrganizationService.AssignOrganizerRoleAsync(OlafId, cancellationToken);
		await keycloakOrganizationService.AddMemberAsync(keycloakId, VeraId, cancellationToken);

		var org = Organization.Create(OrganizationId.Create(keycloakId).GetValueOrThrow(), "Rotes Kreuz Musterstadt")
			.GetValueOrThrow();
		org.ChangeDescription("Ihr lokaler Verband des Deutschen Roten Kreuzes - wir helfen Menschen in Not.");
		org.ChangeContactInfo("info@rk-musterstadt.de", "+49 1234 567890", "https://www.drk.de");
		org.Relocate(Address.Create("Hauptstrasse", "1", "12345", "Musterstadt").GetValueOrThrow());

		dbContext.Set<Organization>().Add(org);

		dbContext.Set<OrganizationMembership>().Add(
			OrganizationMembership.Create(org.Id, UserId.Create(OlafId).GetValueOrThrow(), OrganizationMemberRole.Organizer));

		return org.Id;
	}

	private async Task<OrganizationId> SeedOrg2Async(CancellationToken cancellationToken)
	{
		var keycloakId = await keycloakOrganizationService.CreateOrganizationAsync(
			"Tierschutzverein Musterstadt",
			cancellationToken);

		await keycloakOrganizationService.AddMemberAsync(keycloakId, OlafId, cancellationToken);
		await keycloakOrganizationService.AssignOrganizerRoleAsync(OlafId, cancellationToken);

		var org = Organization.Create(OrganizationId.Create(keycloakId).GetValueOrThrow(), "Tierschutzverein Musterstadt")
			.GetValueOrThrow();
		org.ChangeDescription("Wir setzen uns fur das Wohl von Tieren in Musterstadt und Umgebung ein.");
		org.ChangeContactInfo("info@tsv-musterstadt.de", "+49 1234 567891", null);
		org.Relocate(Address.Create("Tiergartenweg", "5", "12345", "Musterstadt").GetValueOrThrow());

		dbContext.Set<Organization>().Add(org);

		dbContext.Set<OrganizationMembership>().Add(
			OrganizationMembership.Create(org.Id, UserId.Create(OlafId).GetValueOrThrow(), OrganizationMemberRole.Organizer));

		return org.Id;
	}
}
