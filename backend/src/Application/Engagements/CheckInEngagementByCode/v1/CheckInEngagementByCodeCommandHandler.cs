using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.CheckInEngagementByCode.v1;

internal sealed class CheckInEngagementByCodeCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<CheckInEngagementByCodeCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		CheckInEngagementByCodeCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(request.OpportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId.Value}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		if (opportunity.CheckInMethod != CheckInMethod.QRCode)
			throw new ResultFailureException(Error.Conflict(
				"Engagement.CheckInMethodNotQrCode", "This opportunity does not use QR code check-in."));

		var code = request.Code.Trim();
		if (code.Length != 8 || !code.All(char.IsAsciiHexDigit))
			throw new ResultFailureException(Error.Validation(
				"Engagement.InvalidCheckInCode", "Check-in code must be 8 hexadecimal characters."));

		// EngagementId is a UUIDv7 (Domain/Engagements/EngagementId.cs), so its
		// first 8 hex characters are a millisecond timestamp segment shared by
		// every engagement created anywhere within the same ~65-second window -
		// not 32 bits of randomness. A burst of sign-ups can legitimately share
		// a code, so more than one match is an expected case to handle, not a
		// defensive-programming edge case.
		var candidates = await dbContext.GetActiveEngagementsForOpportunityAsync(request.OpportunityId, cancellationToken);
		var matches = candidates
			.Where(e => e.Id.Value.ToString().StartsWith(code, StringComparison.OrdinalIgnoreCase))
			.ToList();

		if (matches.Count > 1)
			throw new ResultFailureException(Error.Conflict(
				"Engagement.CheckInCodeAmbiguous",
				"Multiple sign-ups match this code. Use the QR scanner instead."));

		var engagement = matches.Count == 1
			? matches[0]
			: throw new ResultFailureException(Error.NotFound(
				"Engagement.NotFound", $"No engagement matching code '{code}' was found."));

		engagement.CheckIn().ThrowIfFailure();

		return engagement;
	}
}
