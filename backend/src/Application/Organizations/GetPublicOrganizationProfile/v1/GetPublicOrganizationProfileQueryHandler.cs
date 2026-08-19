using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.VolunteerOpportunities;
using Domain.Organizations;
using Domain.VolunteerOpportunities;

namespace Application.Organizations.GetPublicOrganizationProfile.v1;

internal sealed class GetPublicOrganizationProfileQueryHandler(
	IApplicationDbContext dbContext,
	IVolunteerOpportunityReadRepository volunteerOpportunityReadRepository)
	: IQueryHandler<GetPublicOrganizationProfileQuery, PublicOrganizationProfileResponse?>
{
	public async ValueTask<PublicOrganizationProfileResponse?> Handle(
		GetPublicOrganizationProfileQuery request,
		CancellationToken cancellationToken = default)
	{
		var organization = await dbContext.Organizations.FindAsync(
			OrganizationId.Create(request.OrganizationId).GetValueOrThrow(), cancellationToken);

		if (organization is null)
			return null;

		var opportunities = await volunteerOpportunityReadRepository
			.GetSummariesByOrganizationAsync(
				organization.Id.Value,
				OpportunityStatus.Published,
				cancellationToken);

		var address = organization.Address is null
			? null
			: new PublicAddressDto(
				organization.Address.Street,
				organization.Address.HouseNumber,
				organization.Address.ZipCode,
				organization.Address.City);

		var openOpportunities = opportunities
			.Select(o => new PublicOpportunitySummaryDto(
				o.Id,
				o.TitleDe,
				o.TitleEn,
				o.DescriptionDe,
				o.DescriptionEn,
				o.Street,
				o.HouseNumber,
				o.ZipCode,
				o.City,
				o.IsRemote,
				o.Occurrence,
				o.ParticipationType,
				o.CreatedOn,
				o.ValidUntil,
				o.NextTimeSlotStart,
				o.Category,
				o.TotalMaxParticipants,
				o.CurrentParticipantCount))
			.ToList();

		return new PublicOrganizationProfileResponse(
			organization.Id.Value,
			organization.Name,
			organization.Description,
			organization.ContactEmail,
			organization.ContactPhone,
			organization.Website,
			address,
			openOpportunities,
			organization.LogoUrl);
	}
}
