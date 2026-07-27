using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.AdminRestoreVolunteerOpportunity.v1;

/// <summary>
/// Undoes an admin shadow delete (<see cref="AdminShadowDeleteVolunteerOpportunity.v1.AdminShadowDeleteVolunteerOpportunityCommandHandler"/>).
/// </summary>
internal sealed class AdminRestoreVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<AdminRestoreVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		AdminRestoreVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunityId = VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow();

		var opportunity = await dbContext.FindVolunteerOpportunityIncludingDeletedAsync(opportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		opportunity.Restore().ThrowIfFailure();

		return true;
	}
}
