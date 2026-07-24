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
				"First Aid Course",
				"Learn life-saving first aid techniques in our hands-on one-day course.",
				isRemote: false,
				Address.Create("Main Street", "1", "12345", "Fairview").GetValueOrThrow(),
				Occurrence.OneTime,
				ParticipationType.Waitlist,
				CheckInMethod.Manual,
				pinGenerator,
				category: Category.Health,
				status: OpportunityStatus.Draft).GetValueOrThrow();
			opp1.AddTimeSlot(now.AddDays(14), now.AddDays(14).AddHours(8), 20, now).GetValueOrThrow();
			opp1.Publish().ThrowIfFailure();

			var opp2 = VolunteerOpportunity.Create(
				org1Id,
				"Blood Donation Drive",
				"Support our regular blood donation drive and help save lives.",
				isRemote: false,
				Address.Create("Town Hall Square", "1", "12345", "Fairview").GetValueOrThrow(),
				Occurrence.Recurring,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator,
				category: Category.Health).GetValueOrThrow();

			var opp2b = VolunteerOpportunity.Create(
				org1Id,
				"Paramedic Service at the Town Festival",
				"Join our paramedic team at the annual town festival and provide first aid on site.",
				isRemote: false,
				Address.Create("Market Square", "2", "12345", "Fairview").GetValueOrThrow(),
				Occurrence.OneTime,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator,
				category: Category.Health).GetValueOrThrow();

			var opp2c = VolunteerOpportunity.Create(
				org1Id,
				"Clothing Collection for People in Need",
				"Help sort and distribute donated clothing to people in need in the region.",
				isRemote: false,
				Address.Create("Warehouse Street", "10", "12345", "Fairview").GetValueOrThrow(),
				Occurrence.Recurring,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator,
				category: Category.Social).GetValueOrThrow();

			var opp2d = VolunteerOpportunity.Create(
				org1Id,
				"First Aid Training for Clubs and Associations",
				"Teach clubs and volunteer groups the basics of first aid in online training sessions.",
				isRemote: true,
				address: null,
				Occurrence.Recurring,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator,
				category: Category.Education).GetValueOrThrow();

			var opp3 = VolunteerOpportunity.Create(
				org2Id,
				"Animal Shelter Helpers Wanted",
				"Help us care for and look after the animals in our shelter.",
				isRemote: false,
				Address.Create("Animal Park Lane", "5", "12345", "Fairview").GetValueOrThrow(),
				Occurrence.Recurring,
				ParticipationType.Waitlist,
				CheckInMethod.QRCode,
				pinGenerator,
				category: Category.Animals,
				status: OpportunityStatus.Draft).GetValueOrThrow();
			opp3.AddTimeSlot(now.AddDays(7), now.AddDays(7).AddHours(4), 5, now).GetValueOrThrow();
			opp3.AddTimeSlot(now.AddDays(21), now.AddDays(21).AddHours(4), 5, now).GetValueOrThrow();
			opp3.Publish().ThrowIfFailure();

			var opp4 = VolunteerOpportunity.Create(
				org2Id,
				"Online Fundraising Support",
				"Support our online fundraising team from the comfort of your own home.",
				isRemote: true,
				address: null,
				Occurrence.OneTime,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator,
				category: Category.Social).GetValueOrThrow();

			var opp4b = VolunteerOpportunity.Create(
				org2Id,
				"Dog Walking Service for Shelter Dogs",
				"Take our shelter dogs for regular walks and give them the exercise they need.",
				isRemote: false,
				Address.Create("Animal Park Lane", "5", "12345", "Fairview").GetValueOrThrow(),
				Occurrence.Recurring,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator,
				category: Category.Animals).GetValueOrThrow();

			var opp4c = VolunteerOpportunity.Create(
				org2Id,
				"Pet Food Donation Drive",
				"Collect food and supply donations for our animals at a local drive.",
				isRemote: false,
				Address.Create("Field Lane", "3", "12345", "Fairview").GetValueOrThrow(),
				Occurrence.OneTime,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator,
				category: Category.Animals).GetValueOrThrow();

			var opp4d = VolunteerOpportunity.Create(
				org2Id,
				"Foster Animal Sponsorships",
				"Take on a sponsorship for a foster animal and support them through their placement.",
				isRemote: true,
				address: null,
				Occurrence.Recurring,
				ParticipationType.IndividualContact,
				CheckInMethod.None,
				pinGenerator,
				category: Category.Animals).GetValueOrThrow();

			dbContext.Set<VolunteerOpportunity>().AddRange(
				opp1, opp2, opp2b, opp2c, opp2d, opp3, opp4, opp4b, opp4c, opp4d);

			var veraUserId = UserId.Create(VeraId).GetValueOrThrow();

			dbContext.Set<Engagement>().AddRange(
				Engagement.CreateWaitlistSignUp(opp1.Id, veraUserId, opp1.TimeSlots.First().Id),
				Engagement.CreateIndividualContact(
					opp2.Id,
					veraUserId,
					"I would love to help out as a volunteer at the next blood donation drive.").GetValueOrThrow(),
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
			"Fairview Red Cross",
			cancellationToken);

		await keycloakOrganizationService.AddMemberAsync(keycloakId, OlafId, cancellationToken);
		await keycloakOrganizationService.AssignOrganizerRoleAsync(OlafId, cancellationToken);
		await keycloakOrganizationService.AddMemberAsync(keycloakId, VeraId, cancellationToken);

		var org = Organization.Create(OrganizationId.Create(keycloakId).GetValueOrThrow(), "Fairview Red Cross")
			.GetValueOrThrow();
		org.ChangeDescription("Your local Red Cross chapter - we help people in need.");
		org.ChangeContactInfo("info@fairview-redcross.org", "+1 555 0100", "https://www.fairview-redcross.example");
		org.Relocate(Address.Create("Main Street", "1", "12345", "Fairview").GetValueOrThrow());

		dbContext.Set<Organization>().Add(org);

		dbContext.Set<OrganizationMembership>().Add(
			OrganizationMembership.Create(org.Id, UserId.Create(OlafId).GetValueOrThrow(), OrganizationMemberRole.Organizer));

		return org.Id;
	}

	private async Task<OrganizationId> SeedOrg2Async(CancellationToken cancellationToken)
	{
		var keycloakId = await keycloakOrganizationService.CreateOrganizationAsync(
			"Fairview Animal Welfare Association",
			cancellationToken);

		await keycloakOrganizationService.AddMemberAsync(keycloakId, OlafId, cancellationToken);
		await keycloakOrganizationService.AssignOrganizerRoleAsync(OlafId, cancellationToken);

		var org = Organization.Create(OrganizationId.Create(keycloakId).GetValueOrThrow(), "Fairview Animal Welfare Association")
			.GetValueOrThrow();
		org.ChangeDescription("We are committed to the wellbeing of animals in Fairview and the surrounding area.");
		org.ChangeContactInfo("info@fairview-animalwelfare.org", "+1 555 0101", null);
		org.Relocate(Address.Create("Animal Park Lane", "5", "12345", "Fairview").GetValueOrThrow());

		dbContext.Set<Organization>().Add(org);

		dbContext.Set<OrganizationMembership>().Add(
			OrganizationMembership.Create(org.Id, UserId.Create(OlafId).GetValueOrThrow(), OrganizationMemberRole.Organizer));

		return org.Id;
	}
}
