using Application.Common.Keycloak;
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
				new Domain.VolunteerOpportunities.Address("Hauptstrasse", "1", "12345", "Musterstadt"),
				Occurrence.OneTime,
				ParticipationType.Waitlist,
				CheckInMethod.Manual);
			opp1.AddTimeSlot(now.AddDays(14), now.AddDays(14).AddHours(8), 20);

			var opp2 = VolunteerOpportunity.Create(
				org1Id,
				"Blutspende-Aktion",
				"Unterstutzen Sie unsere regelmasige Blutspende-Aktion und helfen Sie, Leben zu retten.",
				isRemote: false,
				new Domain.VolunteerOpportunities.Address("Rathausplatz", "1", "12345", "Musterstadt"),
				Occurrence.Recurring,
				ParticipationType.IndividualContact,
				CheckInMethod.None);

			var opp3 = VolunteerOpportunity.Create(
				org2Id,
				"Tierheim Helfer gesucht",
				"Helfen Sie uns bei der Betreuung und Pflege der Tiere in unserem Tierheim.",
				isRemote: false,
				new Domain.VolunteerOpportunities.Address("Tiergartenweg", "5", "12345", "Musterstadt"),
				Occurrence.Recurring,
				ParticipationType.Waitlist,
				CheckInMethod.QRCode);
			opp3.AddTimeSlot(now.AddDays(7), now.AddDays(7).AddHours(4), 5);
			opp3.AddTimeSlot(now.AddDays(21), now.AddDays(21).AddHours(4), 5);

			var opp4 = VolunteerOpportunity.Create(
				org2Id,
				"Online-Fundraising Unterstutzung",
				"Unterstutzen Sie unser Online-Fundraising-Team bequem von zu Hause aus.",
				isRemote: true,
				address: null,
				Occurrence.OneTime,
				ParticipationType.IndividualContact,
				CheckInMethod.None);

			dbContext.Set<VolunteerOpportunity>().AddRange(opp1, opp2, opp3, opp4);

			var veraUserId = new UserId(VeraId);

			dbContext.Set<Engagement>().AddRange(
				Engagement.CreateWaitlistSignUp(opp1.Id, veraUserId, opp1.TimeSlots.First().Id),
				Engagement.CreateIndividualContact(
					opp2.Id,
					veraUserId,
					"Ich wuerde gerne bei der nachsten Blutspende-Aktion als Helfer dabei sein."),
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

	private async Task<OrganizationId> SeedOrg1Async(CancellationToken cancellationToken)
	{
		var keycloakId = await keycloakOrganizationService.CreateOrganizationAsync(
			"Rotes Kreuz Musterstadt",
			cancellationToken);

		await keycloakOrganizationService.AddMemberAsync(keycloakId, OlafId, cancellationToken);
		await keycloakOrganizationService.AssignOrganizerRoleAsync(OlafId, cancellationToken);
		await keycloakOrganizationService.AddMemberAsync(keycloakId, VeraId, cancellationToken);

		var org = Organization.Create(new OrganizationId(keycloakId), "Rotes Kreuz Musterstadt");
		org.Update(
			"Rotes Kreuz Musterstadt",
			"Ihr lokaler Verband des Deutschen Roten Kreuzes - wir helfen Menschen in Not.",
			"info@rk-musterstadt.de",
			"+49 1234 567890",
			"https://www.drk.de",
			new Domain.Common.Address("Hauptstrasse", "1", "12345", "Musterstadt"));

		dbContext.Set<Organization>().Add(org);

		return org.Id;
	}

	private async Task<OrganizationId> SeedOrg2Async(CancellationToken cancellationToken)
	{
		var keycloakId = await keycloakOrganizationService.CreateOrganizationAsync(
			"Tierschutzverein Musterstadt",
			cancellationToken);

		await keycloakOrganizationService.AddMemberAsync(keycloakId, OlafId, cancellationToken);
		await keycloakOrganizationService.AssignOrganizerRoleAsync(OlafId, cancellationToken);

		var org = Organization.Create(new OrganizationId(keycloakId), "Tierschutzverein Musterstadt");
		org.Update(
			"Tierschutzverein Musterstadt",
			"Wir setzen uns fur das Wohl von Tieren in Musterstadt und Umgebung ein.",
			"info@tsv-musterstadt.de",
			"+49 1234 567891",
			null,
			new Domain.Common.Address("Tiergartenweg", "5", "12345", "Musterstadt"));

		dbContext.Set<Organization>().Add(org);

		return org.Id;
	}
}
