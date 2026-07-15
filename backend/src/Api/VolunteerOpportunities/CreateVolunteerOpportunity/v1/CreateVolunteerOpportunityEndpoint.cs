using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.CreateVolunteerOpportunity.v1;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid) ? new UserId(uid) : throw new DomainException("Invalid user.");

		if (request.Title is { Length: > 200 })
			return Results.Problem("Title must not exceed 200 characters.", statusCode: StatusCodes.Status400BadRequest);

		if (request.Description is { Length: > 5000 })
			return Results.Problem("Description must not exceed 5000 characters.", statusCode: StatusCodes.Status400BadRequest);

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

		if (request.CheckInPin is { Length: > 0 } pin && (pin.Length < 4 || pin.Length > 6 || !pin.All(char.IsAsciiDigit)))
		{
			return Results.Problem(
				"Check-in PIN must be 4 to 6 digits.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		var status = request.IsDraft == true
			? OpportunityStatus.Draft
			: OpportunityStatus.Published;

		// Remote opportunities have no address. Drafts may omit address fields too.
		var hasAnyAddressField =
			!string.IsNullOrWhiteSpace(request.Street) ||
			!string.IsNullOrWhiteSpace(request.HouseNumber) ||
			!string.IsNullOrWhiteSpace(request.ZipCode) ||
			!string.IsNullOrWhiteSpace(request.City);

		var address = request.IsRemote || (status == OpportunityStatus.Draft && !hasAnyAddressField)
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
			request.IsRemote,
			address,
			occurrence,
			participationType,
			checkInMethod,
			category,
			[.. request.Tags ?? []],
			status,
			userId,
			string.IsNullOrWhiteSpace(request.CheckInPin) ? null : request.CheckInPin);

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
