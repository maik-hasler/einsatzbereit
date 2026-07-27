using Application.Common.Messaging;

namespace Application.Organizations.AdminRestoreOrganization.v1;

public sealed record AdminRestoreOrganizationCommand(
	Guid OrganizationId)
	: ICommand<bool>;
