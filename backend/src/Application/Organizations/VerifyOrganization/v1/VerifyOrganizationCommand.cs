using Application.Common.Messaging;

namespace Application.Organizations.VerifyOrganization.v1;

public sealed record VerifyOrganizationCommand(
	Guid OrganizationId,
	bool IsVerified)
	: ICommand<bool>;
