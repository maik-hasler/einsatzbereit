using Domain.Primitives;

namespace Domain.Organizations;

public sealed class Organization
    : AggregateRoot<OrganizationId>,
      IAuditableEntity
{
    public string Name { get; private set; }

    public DateTimeOffset CreatedOn { get; private set; }

    public DateTimeOffset? ModifiedOn { get; private set; }

    #pragma warning disable CS8618
    private Organization() : base(default) { }
    #pragma warning restore CS8618

    private Organization(
        OrganizationId id,
        string name)
        : base(id)
    {
        Name = name;
    }

    public static Organization Create(
        OrganizationId id,
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name must not be empty.");

        return new Organization(id, name);
    }
}
