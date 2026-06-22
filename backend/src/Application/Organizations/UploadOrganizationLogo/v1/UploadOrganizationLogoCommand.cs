using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.UploadOrganizationLogo.v1;

public sealed record UploadOrganizationLogoCommand(
	Guid OrganizationId,
	byte[] Content,
	string ContentType,
	UserId RequestingUserId)
	: ICommand<bool>;
