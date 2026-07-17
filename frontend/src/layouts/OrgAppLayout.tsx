import { NavLink, Outlet, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import OrgSwitcher from "../components/OrgSwitcher";

const TABS = [
	{ segment: "dashboard", labelKey: "orgOverview.tabCalendar" },
	{ segment: "engagements", labelKey: "orgOverview.tabEngagements" },
	{ segment: "members", labelKey: "orgOverview.tabMembers" },
	{ segment: "settings", labelKey: "orgOverview.tabSettings" },
] as const;

// The organizer application context - #691/#702. Separate from the public
// Main Page shell: this is where "which organization am I acting in" lives,
// not a pill in the global Header that most of the site has no use for.
export default function OrgAppLayout() {
	const { organizationId } = useParams<{ organizationId: string }>();
	const { t } = useTranslation();

	return (
		<div>
			<div className="mb-6 flex flex-col gap-4 border-b border-gray-200 pb-4 sm:flex-row sm:items-center sm:justify-between">
				<OrgSwitcher activeOrganizationId={organizationId} />

				<nav
					className="flex gap-4 overflow-x-auto"
					aria-label={t("orgDashboard.title")}
				>
					{TABS.map(({ segment, labelKey }) => (
						<NavLink
							key={segment}
							to={`/app/${organizationId}/${segment}`}
							className={({ isActive }) =>
								`shrink-0 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
									isActive
										? "bg-brand-50 text-brand-700"
										: "text-gray-500 hover:bg-gray-50 hover:text-gray-700"
								}`
							}
						>
							{t(labelKey)}
						</NavLink>
					))}
				</nav>
			</div>

			<Outlet />
		</div>
	);
}
