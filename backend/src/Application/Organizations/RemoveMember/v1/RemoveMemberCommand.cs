using Application.Common.Messaging;

namespace Application.Organizations.RemoveMember.v1;

public sealed record RemoveMemberCommand(
    Guid OrganizationId,
    Guid UserId)
    : ICommand<bool>;
