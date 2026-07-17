import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import type { OrganizationSummaryDto } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import CreateOrganizationModal from "./CreateOrganizationModal";

// Lives inside OrgAppLayout only (not the global Header) - see #691/#702. Every
// row is a single navigation target: selecting an org always means "go there",
// so there's no separate name-vs-icon click target like the old header pill had.
export default function OrgSwitcher({
	activeOrganizationId,
}: {
	activeOrganizationId: string | undefined;
}) {
	const api = useApiClient();
	const navigate = useNavigate();
	const { t } = useTranslation();
	const [orgs, setOrgs] = useState<OrganizationSummaryDto[]>([]);
	const [loading, setLoading] = useState(true);
	const [open, setOpen] = useState(false);
	const [showModal, setShowModal] = useState(false);
	const containerRef = useRef<HTMLDivElement>(null);

	const dashboardPath = (org: OrganizationSummaryDto) =>
		`/app/${org.slug ?? org.id}/dashboard`;

	const fetchOrgs = () => {
		setLoading(true);
		api
			.getOrganizations()
			.then(setOrgs)
			.catch(() => setOrgs([]))
			.finally(() => setLoading(false));
	};

	useEffect(() => {
		fetchOrgs();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	useEffect(() => {
		const handleClick = (e: MouseEvent) => {
			if (
				containerRef.current &&
				!containerRef.current.contains(e.target as Node)
			) {
				setOpen(false);
			}
		};
		document.addEventListener("click", handleClick);
		return () => document.removeEventListener("click", handleClick);
	}, []);

	const activeOrg =
		orgs.find(
			(o) => o.id === activeOrganizationId || o.slug === activeOrganizationId,
		) ?? null;

	function handleSelect(org: OrganizationSummaryDto) {
		setOpen(false);
		navigate(dashboardPath(org));
	}

	function handleOrgCreated() {
		const prevIds = new Set(orgs.map((o) => o.id));
		setLoading(true);
		api
			.getOrganizations()
			.then((data) => {
				setOrgs(data);
				const created = data.find((o) => !prevIds.has(o.id));
				if (created) {
					navigate(dashboardPath(created));
				}
			})
			.catch(() => {})
			.finally(() => setLoading(false));
	}

	if (loading) {
		return <div className="h-10 w-64 animate-pulse rounded-lg bg-gray-100" />;
	}

	return (
		<div className="relative" ref={containerRef}>
			<button
				type="button"
				onClick={() => setOpen((o) => !o)}
				className="flex items-center gap-2 rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm font-medium text-gray-900 transition-colors hover:bg-gray-50"
				aria-expanded={open}
				aria-label={t("organization.switchLabel")}
			>
				<svg
					className="h-4 w-4 shrink-0 text-gray-400"
					fill="none"
					viewBox="0 0 24 24"
					strokeWidth="1.5"
					stroke="currentColor"
					aria-hidden="true"
				>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M2.25 21h19.5m-18-18v18m10.5-18v18m6-13.5V21M6.75 6.75h.75m-.75 3h.75m-.75 3h.75m3-6h.75m-.75 3h.75m-.75 3h.75M6.75 21v-3.375c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21M3 3h12m-.75 4.5H21m-3.75 3H21m-3.75 3H21"
					/>
				</svg>
				<span className="max-w-[240px] truncate">
					{activeOrg
						? t("organization.contextBanner", { name: activeOrg.name })
						: t("organization.selectPlaceholder")}
				</span>
				<svg
					className={`h-3.5 w-3.5 shrink-0 text-gray-400 transition-transform ${open ? "rotate-180" : ""}`}
					fill="none"
					viewBox="0 0 24 24"
					strokeWidth="2"
					stroke="currentColor"
					aria-hidden="true"
				>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="m19.5 8.25-7.5 7.5-7.5-7.5"
					/>
				</svg>
			</button>

			{open && (
				<div className="absolute left-0 top-full z-50 mt-2 w-72 rounded-lg border border-gray-200 bg-white shadow-lg">
					<ul className="max-h-72 overflow-y-auto py-1">
						{orgs.map((org) => {
							const isActive = org.id === activeOrg?.id;
							return (
								<li key={org.id}>
									<button
										type="button"
										data-testid="org-dashboard-link"
										onClick={() => handleSelect(org)}
										aria-current={isActive ? "true" : undefined}
										className={`flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors ${
											isActive
												? "bg-brand-50 font-medium text-brand-700"
												: "text-gray-700 hover:bg-gray-50"
										}`}
									>
										<span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md bg-brand-100 text-xs font-semibold text-brand-700">
											{org.name.charAt(0).toUpperCase()}
										</span>
										<span className="truncate">{org.name}</span>
									</button>
								</li>
							);
						})}
					</ul>

					<div className="border-t border-gray-100">
						<button
							type="button"
							onClick={() => {
								setOpen(false);
								setShowModal(true);
							}}
							className="flex w-full items-center gap-3 px-4 py-2.5 text-sm text-brand-700 transition-colors hover:bg-brand-50"
						>
							<svg
								className="h-4 w-4"
								fill="none"
								viewBox="0 0 24 24"
								strokeWidth="1.5"
								stroke="currentColor"
								aria-hidden="true"
							>
								<path
									strokeLinecap="round"
									strokeLinejoin="round"
									d="M12 4.5v15m7.5-7.5h-15"
								/>
							</svg>
							{t("organization.create")}
						</button>
					</div>
				</div>
			)}

			{showModal && (
				<CreateOrganizationModal
					onClose={() => setShowModal(false)}
					onSuccess={handleOrgCreated}
				/>
			)}
		</div>
	);
}
