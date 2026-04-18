using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.UpdateOrganization.v1;

internal sealed class UpdateOrganizationCommandHandler(
    IApplicationDbContext dbContext)
    : ICommandHandler<UpdateOrganizationCommand, bool>
{
    public async ValueTask<bool> Handle(
        UpdateOrganizationCommand request,
        CancellationToken cancellationToken = default)
    {
        var organization = await dbContext.Organizations.FindAsync(
            new OrganizationId(request.OrganizationId), cancellationToken)
            ?? throw new DomainException($"Organization '{request.OrganizationId}' not found.");

        var address = request.Address is null
            ? null
            : new Address(
                request.Address.Street,
                request.Address.HouseNumber,
                request.Address.ZipCode,
                request.Address.City);

        organization.Update(
            request.Name,
            request.Description,
            request.ContactEmail,
            request.ContactPhone,
            request.Website,
            address);

        return true;
    }
}
