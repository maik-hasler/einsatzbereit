import { useEffect, useState } from "react";
import { useApiClient } from "../../../hooks/useApiClient";

export interface DashboardKpis {
	pendingEngagements: number;
	confirmedEngagementsTotal: number;
}

export interface DashboardKpisState {
	kpis: DashboardKpis | null;
	loading: boolean;
	failed: boolean;
}

// Shared by ToDoWidget (the pending-sign-up action queue) and
// VolunteerStatsWidget (the neutral signed-up total) - #1780 split those two
// numbers into separate tiles, and both read them off the same
// GET .../dashboard response.
//
// Each widget still calls this hook for itself rather than the dashboard page
// fetching once and passing counts down: every widget in this folder owns its
// own data (see CalendarWidget/UpcomingOpportunitiesWidget), which is what
// makes removing a widget from the layout actually stop its request. Two
// tiles placed at once therefore issue two GETs of an endpoint that is two
// COUNTs - cheap enough that keeping the removal semantics is the better
// trade.
//
// `failed` is a flag, not a message: the two widgets label the same failure
// differently ("summary"/"volunteer count"), so the copy stays at the call
// site.
export function useOrganizationDashboardKpis(
	organizationId: string,
	refreshKey: number,
): DashboardKpisState {
	const api = useApiClient();

	const [kpis, setKpis] = useState<DashboardKpis | null>(null);
	const [loading, setLoading] = useState(true);
	const [failed, setFailed] = useState(false);

	useEffect(() => {
		// Switching organizations from the header switcher issues a second
		// request while the first is still open; without this guard the slower
		// response wins whichever order they land in, and one org's dashboard
		// can end up showing another's counts. Same pattern as OrgAppLayout's
		// own latestRequestRef.
		let alive = true;
		setLoading(true);
		setFailed(false);
		api
			.getOrganizationDashboard(organizationId)
			.then((data) => {
				if (!alive) return;
				setKpis({
					pendingEngagements: data.pendingEngagements,
					confirmedEngagementsTotal: data.confirmedEngagementsTotal,
				});
			})
			.catch(() => {
				if (alive) setFailed(true);
			})
			.finally(() => {
				if (alive) setLoading(false);
			});
		return () => {
			alive = false;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId, refreshKey]);

	return { kpis, loading, failed };
}
