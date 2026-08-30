namespace Api.Organizations.CreateInvitation.v1;

public sealed record CreateInvitationResponse(Guid InvitationId, DateTimeOffset ExpiresOn);
