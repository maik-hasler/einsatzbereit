using Domain.Common;
using Domain.Primitives;

namespace Domain.Organizations;

public sealed class Organization
	: AggregateRoot<OrganizationId>,
		IAuditableEntity,
		ISoftDeletableEntity
{
	public string Name { get; private set; }

	public string? Description { get; private set; }

	public string? ContactEmail { get; private set; }

	public string? ContactPhone { get; private set; }

	public string? Website { get; private set; }

	public Address? Address { get; private set; }

	public string? LogoUrl { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

	public bool IsDeleted { get; private set; }

	public DateTimeOffset? DeletedOn { get; private set; }

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

	public const int MaxNameLength = 100;

	public static Result<Organization> Create(
		OrganizationId id,
		string name)
	{
		var validName = EnsureValidName(name);
		if (validName.IsFailure)
			return Result.Failure<Organization>(validName.Error);

		return new Organization(id, name);
	}

	public void SetLogoUrl(string? url)
	{
		LogoUrl = url;
	}

	public Result Rename(string name)
	{
		var validName = EnsureValidName(name);
		if (validName.IsFailure)
			return validName;

		Name = name;
		return Result.Success();
	}

	// #1158: Create enforced this length cap only in CreateOrganizationCommandHandler
	// (deliberately, so the check runs before that handler's Keycloak call), but
	// Rename had no equivalent cap at all - shared here so both go through the
	// same rule regardless of caller.
	private static Result EnsureValidName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return Result.Failure(Error.Validation("Organization.NameRequired", "Name must not be empty."));

		if (name.Length > MaxNameLength)
			return Result.Failure(Error.Validation("Organization.NameTooLong", $"Organization name must not exceed {MaxNameLength} characters."));

		return Result.Success();
	}

	public void ChangeDescription(string? description)
	{
		Description = description;
	}

	public Result ChangeContactInfo(string? contactEmail, string? contactPhone, string? website)
	{
		if (!string.IsNullOrWhiteSpace(website))
		{
			if (website.Length > 500)
				return Result.Failure(Error.Validation("Organization.WebsiteTooLong", "Website must not exceed 500 characters."));

			if (!Uri.TryCreate(website, UriKind.Absolute, out var uri)
				|| (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
				return Result.Failure(Error.Validation("Organization.WebsiteInvalid", "Website must be a valid http or https URL."));
		}

		ContactEmail = contactEmail;
		ContactPhone = contactPhone;
		Website = website;
		return Result.Success();
	}

	public void Relocate(Address? address)
	{
		Address = address;
	}

	public Result MarkDeleted(DateTimeOffset deletedOn)
	{
		if (IsDeleted)
			return Result.Failure(Error.Conflict("Organization.AlreadyDeleted", "Organization is already shadow-deleted."));

		IsDeleted = true;
		DeletedOn = deletedOn;
		return Result.Success();
	}

	public Result Restore()
	{
		if (!IsDeleted)
			return Result.Failure(Error.Conflict("Organization.NotDeleted", "Organization is not shadow-deleted."));

		IsDeleted = false;
		DeletedOn = null;
		return Result.Success();
	}
}
