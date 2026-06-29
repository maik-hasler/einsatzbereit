using Application.Common.Messaging;
using Domain.Users;

namespace Application.Invitations.GetMyInvitations.v1;

public sealed record GetMyInvitationsQuery(UserId UserId) : IQuery<List<MyInvitationDto>>;
