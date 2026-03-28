using Domain.Primitives;

namespace Domain.Organisationen;

public sealed class Organisation
    : Entity<OrganisationId>,
    IAuditableEntity
{
    public string Name { get; private set; }

    public Guid KeycloakId { get; private set; }

    public DateTimeOffset CreatedOn { get; }

    public DateTimeOffset? ModifiedOn { get; }

    private Organisation(
        OrganisationId id,
        string name,
        Guid keycloakId)
        : base(id)
    {
        Name = name;
        KeycloakId = keycloakId;
    }

    public static Organisation Create(
        string name,
        Guid keycloakId)
    {
        return new Organisation(
            new OrganisationId(Guid.CreateVersion7()),
            name,
            keycloakId);
    }
}
