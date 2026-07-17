using Domain.Common;
using Domain.Primitives;

namespace Domain.Organizations;

public sealed class Organization
	: AggregateRoot<OrganizationId>,
		IAuditableEntity
{
	public string Name { get; private set; }

	public string? Slug { get; private set; }

	public string? Description { get; private set; }

	public string? ContactEmail { get; private set; }

	public string? ContactPhone { get; private set; }

	public string? Website { get; private set; }

	public Address? Address { get; private set; }

	public string? LogoUrl { get; private set; }

	public bool IsVerified { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

#pragma warning disable CS8618
	private Organization() : base(default) { }
#pragma warning restore CS8618

	private Organization(
		OrganizationId id,
		string name,
		string? slug)
		: base(id)
	{
		Name = name;
		Slug = slug;
	}

	public static Result<Organization> Create(
		OrganizationId id,
		string name,
		string? slug = null)
	{
		if (string.IsNullOrWhiteSpace(name))
			return Result.Failure<Organization>(Error.Validation("Organization.NameRequired", "Name must not be empty."));

		return new Organization(id, name, slug);
	}

	public void SetLogoUrl(string? url)
	{
		LogoUrl = url;
	}

	public Result Verify()
	{
		if (IsVerified)
			return Result.Failure(Error.Conflict("Organization.AlreadyVerified", "Organization is already verified."));

		IsVerified = true;
		AddEvent(new OrganizationVerifiedDomainEvent(Id));
		return Result.Success();
	}

	public Result RevokeVerification()
	{
		if (!IsVerified)
			return Result.Failure(Error.Conflict("Organization.NotVerified", "Organization is not verified."));

		IsVerified = false;
		AddEvent(new OrganizationVerificationRevokedDomainEvent(Id));
		return Result.Success();
	}

	public Result Rename(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return Result.Failure(Error.Validation("Organization.NameRequired", "Name must not be empty."));

		Name = name;
		return Result.Success();
	}

	public void ChangeDescription(string? description)
	{
		Description = description;
	}

	public void ChangeContactInfo(string? contactEmail, string? contactPhone, string? website)
	{
		ContactEmail = contactEmail;
		ContactPhone = contactPhone;
		Website = website;
	}

	public void Relocate(Address? address)
	{
		Address = address;
	}
}
