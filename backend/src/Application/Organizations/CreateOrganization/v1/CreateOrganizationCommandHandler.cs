using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Organizations.CreateOrganization.v1;

internal sealed class CreateOrganizationCommandHandler(
	IKeycloakOrganizationService keycloakOrganizationService,
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	ILogger<CreateOrganizationCommandHandler> logger)
	: ICommandHandler<CreateOrganizationCommand, Organization>
{
	public async ValueTask<Organization> Handle(
		CreateOrganizationCommand request,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new ResultFailureException(Error.Validation("Organization.NameRequired", "Name must not be empty."));

		if (request.Name.Length > 100)
			throw new ResultFailureException(Error.Validation("Organization.NameTooLong", "Organization name must not exceed 100 characters."));

		var keycloakId = await keycloakOrganizationService.CreateOrganizationAsync(
			request.Name, cancellationToken);

		try
		{
			await keycloakOrganizationService.AddMemberAsync(
				keycloakId, request.UserId, cancellationToken);

			await keycloakOrganizationService.AssignOrganizerRoleAsync(
				request.UserId, cancellationToken);

			var organization = Organization.Create(OrganizationId.Create(keycloakId).GetValueOrThrow(), request.Name)
				.GetValueOrThrow();

			var address = request.Address is null
				? null
				: Address.Create(
					request.Address.Street,
					request.Address.HouseNumber,
					request.Address.ZipCode,
					request.Address.City).GetValueOrThrow();

			organization.ChangeDescription(request.Description);
			organization.ChangeContactInfo(request.ContactEmail, request.ContactPhone, request.Website).ThrowIfFailure();
			organization.Relocate(address);

			await dbContext.Organizations.AddAsync(organization, cancellationToken);

			var membership = OrganizationMembership.Create(
				organization.Id, UserId.Create(request.UserId).GetValueOrThrow(), OrganizationMemberRole.Organizer);

			await dbContext.OrganizationMemberships.AddAsync(membership, cancellationToken);

			// Flushed here, rather than left to TransactionPipelineBehavior's own
			// SaveChangesAsync after Handle returns, so a DB-level failure (e.g. a unique
			// constraint) is caught by the compensation below instead of leaving the
			// Keycloak organization orphaned.
			await unitOfWork.SaveChangesAsync(cancellationToken);

			return organization;
		}
		catch (Exception ex)
		{
			logger.LogWarning(
				ex,
				"Organization creation failed after Keycloak organization {KeycloakOrganizationId} was created; compensating by deleting it",
				keycloakId);

			try
			{
				// CancellationToken.None: this cleanup must run even if the original
				// operation failed because the caller's token was cancelled.
				await keycloakOrganizationService.DeleteOrganizationAsync(keycloakId, CancellationToken.None);
			}
			catch (Exception cleanupException)
			{
				logger.LogError(
					cleanupException,
					"Failed to compensate orphaned Keycloak organization {KeycloakOrganizationId} - manual cleanup required",
					keycloakId);
			}

			throw;
		}
	}
}
