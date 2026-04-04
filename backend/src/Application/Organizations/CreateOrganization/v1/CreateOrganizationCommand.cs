using Application.Common.Messaging;
using Domain.Organizations;

namespace Application.Organizations.CreateOrganization.v1;

public sealed record CreateOrganizationCommand(
    string Name,
    Guid UserId)
    : ICommand<Organization>;
