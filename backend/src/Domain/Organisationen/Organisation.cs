using Domain.Primitives;

namespace Domain.Organisationen;

public sealed class Organisation
    : AggregateRoot<OrganisationId>,
    IAuditableEntity
{
    public string Name { get; private set; }

    public DateTimeOffset CreatedOn { get; private set; }

    public DateTimeOffset? ModifiedOn { get; private set; }

    private Organisation(
        OrganisationId id,
        string name)
        : base(id)
    {
        Name = name;
    }

    public static Organisation Create(
        OrganisationId id,
        string name)
    {
        return new Organisation(
            id,
            name);
    }
}
