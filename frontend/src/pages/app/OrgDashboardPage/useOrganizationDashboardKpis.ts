import { useApiClient } from "../../../hooks/useApiClient";
import { useSharedOrgFetch } from "../../../hooks/useSharedOrgFetch";

export interface DashboardKpis {
	pendingEngagements: number;
	confirmedEngagementsTotal: number;
}

export interface DashboardKpisState {
	kpis: DashboardKpis | null;
	loading: boolean;
	failed: boolean;
}

export function useOrganizationDashboardKpis(
	organizationId: string,
	refreshKey: number,
): DashboardKpisState {
	const api = useApiClient();

	// Shared rather than per-widget: the ToDo and VolunteerStats tiles read
	// two numbers out of the same response, and each used to issue its own
	// identical request on every dashboard load (#2322 F7).
	//
	// Keying on the organization also settles the race the per-widget effect
	// used to guard by hand - switching organizations from the header
	// switcher issues a second request while the first is still open, and
	// without a guard the slower response wins whichever order they land in,
	// so one org's dashboard can end up showing another's counts.
	const [kpis, , error] = useSharedOrgFetch<DashboardKpis>(
		`organizationDashboard:${organizationId}:${refreshKey}`,
		() =>
			api.getOrganizationDashboard(organizationId).then((data) => ({
				pendingEngagements: data.pendingEngagements,
				confirmedEngagementsTotal: data.confirmedEngagementsTotal,
			})),
	);

	return {
		kpis,
		loading: kpis === null && error === null,
		failed: error !== null,
	};
}
