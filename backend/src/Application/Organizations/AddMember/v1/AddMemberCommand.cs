using Application.Common.Messaging;

namespace Application.Organizations.AddMember.v1;

public sealed record AddMemberCommand(
    Guid OrganizationId,
    Guid UserId)
    : ICommand<bool>;