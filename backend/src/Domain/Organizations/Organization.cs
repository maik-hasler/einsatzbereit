using Domain.Common;
using Domain.Primitives;

namespace Domain.Organizations;

public sealed class Organization
    : AggregateRoot<OrganizationId>,
      IAuditableEntity
{
    public string Name { get; private set; }

    public string? Description { get; private set; }

    public string? ContactEmail { get; private set; }

    public string? ContactPhone { get; private set; }

    public string? Website { get; private set; }

    public Address? Address { get; private set; }

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

    public void Update(
        string name,
        string? description,
        string? contactEmail,
        string? contactPhone,
        string? website,
        Address? address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name must not be empty.");

        Name = name;
        Description = description;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        Website = website;
        Address = address;
    }
}
