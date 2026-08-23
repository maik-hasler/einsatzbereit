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
