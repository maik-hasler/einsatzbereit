using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.CreateVolunteerOpportunity.v1;
using Domain.Organizations;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.CreateVolunteerOpportunity.v1;

internal sealed class CreateVolunteerOpportunityEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPost("/volunteer-opportunities", CreateVolunteerOpportunityAsync)
			.WithName("CreateVolunteerOpportunity")
			.Produces<CreateVolunteerOpportunityResponse>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> CreateVolunteerOpportunityAsync(
		[FromBody] CreateVolunteerOpportunityRequest request,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		if (!Enum.TryParse<Occurrence>(request.Occurrence, ignoreCase: true, out var occurrence))
		{
			return Results.Problem(
				"Invalid occurrence. Allowed values: OneTime, Recurring.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (!Enum.TryParse<ParticipationType>(request.ParticipationType, ignoreCase: true, out var participationType))
		{
			return Results.Problem(
				"Invalid participation type. Allowed values: Waitlist, IndividualContact.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (!Enum.TryParse<CheckInMethod>(request.CheckInMethod, ignoreCase: true, out var checkInMethod))
		{
			return Results.Problem(
				"Invalid check-in method. Allowed values: None, QRCode, PINCode, Manual.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		Category? category = null;
		if (!string.IsNullOrWhiteSpace(request.Category))
		{
			if (!Enum.TryParse<Category>(request.Category, ignoreCase: true, out var parsedCategory))
			{
				return Results.Problem(
					"Invalid category.",
					statusCode: StatusCodes.Status400BadRequest);
			}
			category = parsedCategory;
		}

		var status = request.IsDraft == true
			? OpportunityStatus.Draft
			: OpportunityStatus.Published;

		// Drafts may be saved without an address; published opportunities require one.
		var hasAnyAddressField =
			!string.IsNullOrWhiteSpace(request.Street) ||
			!string.IsNullOrWhiteSpace(request.HouseNumber) ||
			!string.IsNullOrWhiteSpace(request.ZipCode) ||
			!string.IsNullOrWhiteSpace(request.City);

		var address = status == OpportunityStatus.Draft && !hasAnyAddressField
			? null
			: new Address(
				request.Street ?? string.Empty,
				request.HouseNumber ?? string.Empty,
				request.ZipCode ?? string.Empty,
				request.City ?? string.Empty);

		var title = status == OpportunityStatus.Draft && string.IsNullOrWhiteSpace(request.Title)
			? "Unbenannt"
			: request.Title ?? string.Empty;

		var command = new CreateVolunteerOpportunityCommand(
			title,
			request.Description ?? string.Empty,
			new OrganizationId(request.OrganizationId),
			false,
			address,
			occurrence,
			participationType,
			checkInMethod,
			category,
			[.. request.Tags ?? []],
			status);

		var opportunity = await sender.Send(command, cancellationToken);

		var response = new CreateVolunteerOpportunityResponse(
			opportunity.Id.Value,
			opportunity.Title,
			opportunity.Description,
			opportunity.OrganizationId.Value,
			opportunity.Address?.Street,
			opportunity.Address?.HouseNumber,
			opportunity.Address?.ZipCode,
			opportunity.Address?.City,
			opportunity.Address?.Latitude,
			opportunity.Address?.Longitude,
			opportunity.IsRemote,
			opportunity.Occurrence.ToString(),
			opportunity.ParticipationType.ToString(),
			opportunity.CheckInMethod.ToString(),
			opportunity.Category?.ToString(),
			opportunity.Tags,
			opportunity.CreatedOn,
			opportunity.Status.ToString());

		return Results.Ok(response);
	}
}
