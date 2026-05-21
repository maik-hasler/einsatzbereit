using Application.Organizations.GetOrganizationDashboard.v1;

namespace Application.Organizations;

public interface IOrganizationDashboardReadRepository
{
	ValueTask<OrganizationDashboardResponse> GetKpisAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default);
}
