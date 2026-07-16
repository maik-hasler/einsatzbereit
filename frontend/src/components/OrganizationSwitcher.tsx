import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import type { OrganizationSummaryDto } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { getActiveOrgId, setActiveOrgCookie } from "../lib/activeOrg";
import CreateOrganizationModal from "./CreateOrganizationModal";

export default function OrganizationSwitcher({
	transparent = false,
}: {
	transparent?: boolean;
}) {
	const api = useApiClient();
	const navigate = useNavigate();
	const { t } = useTranslation();
	const [orgs, setOrgs] = useState<OrganizationSummaryDto[]>([]);
	const [activeOrgId, setActiveOrgId] = useState<string | null>(getActiveOrgId);
	const [loading, setLoading] = useState(true);
	const [open, setOpen] = useState(false);
	const [showModal, setShowModal] = useState(false);
	const containerRef = useRef<HTMLDivElement>(null);

	const activeOrg = orgs.find((o) => o.id === activeOrgId) ?? null;

	const dashboardPath = (org: OrganizationSummaryDto) =>
		`/organizations/${org.slug ?? org.id}/dashboard`;

	const fetchOrgs = () => {
		setLoading(true);
		api
			.getOrganizations()
			.then((data: OrganizationSummaryDto[]) => {
				setOrgs(data);
				if (!getActiveOrgId() && data.length > 0) {
					setActiveOrgCookie(data[0].id);
					setActiveOrgId(data[0].id);
				}
			})
			.catch(() => setOrgs([]))
			.finally(() => setLoading(false));
	};

	useEffect(() => {
		fetchOrgs();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	// Close dropdown on outside click
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

	const handleSwitch = (org: OrganizationSummaryDto) => {
		setActiveOrgCookie(org.id);
		setActiveOrgId(org.id);
		setOpen(false);
		navigate(dashboardPath(org));
	};

	const handleOrgCreated = () => {
		const prevIds = new Set(orgs.map((o) => o.id));
		setLoading(true);
		api
			.getOrganizations()
			.then((data: OrganizationSummaryDto[]) => {
				setOrgs(data);
				const newOrg = data.find((o) => !prevIds.has(o.id));
				if (newOrg) {
					setActiveOrgCookie(newOrg.id);
					setActiveOrgId(newOrg.id);
				} else if (!getActiveOrgId() && data.length > 0) {
					setActiveOrgCookie(data[0].id);
					setActiveOrgId(data[0].id);
				}
			})
			.catch(() => setOrgs([]))
			.finally(() => setLoading(false));
	};

	if (loading) {
		return (
			<div
				className={`h-9 w-32 animate-pulse rounded-lg ${transparent ? "bg-white/20" : "bg-gray-100"}`}
			/>
		);
	}

	// No orgs - hide the switcher from the header entirely.
	// Users can create an org from their profile page instead.
	if (orgs.length === 0) {
		return null;
	}

	return (
		<>
			<div className="relative" ref={containerRef}>
				<button
					type="button"
					onClick={() => setOpen(!open)}
					className={`flex items-center gap-2 rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors ${transparent ? "border-white/30 bg-white/10 text-white hover:bg-white/20" : "border-gray-200 bg-white text-gray-700 hover:bg-gray-50"}`}
					aria-expanded={open}
					aria-label={t("organization.switchLabel")}
				>
					{/* Building icon */}
					<svg
						className={`w-4 h-4 ${transparent ? "text-white/70" : "text-gray-400"}`}
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

					<span className="max-w-[150px] truncate">
						{activeOrg ? activeOrg.name : t("organization.selectPlaceholder")}
					</span>

					{/* Chevron */}
					<svg
						className={`w-3.5 h-3.5 transition-transform ${open ? "rotate-180" : ""} ${transparent ? "text-white/70" : "text-gray-400"}`}
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

				{/* Dropdown */}
				{open && (
					<div className="absolute left-0 top-full mt-2 w-56 rounded-lg border shadow-lg z-50 bg-white border-gray-200">
						<ul className="py-1 max-h-60 overflow-y-auto">
							{orgs.map((org) => (
								<li
									key={org.id}
									className={`flex items-center gap-2 px-3 py-2 ${org.id === activeOrgId ? "bg-brand-50" : ""}`}
								>
									<button
										type="button"
										onClick={() => handleSwitch(org)}
										className={`flex flex-1 items-center gap-2 min-w-0 text-sm ${org.id === activeOrgId ? "text-brand-700 font-medium" : "text-gray-700 hover:text-gray-900"}`}
									>
										<span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md text-xs font-semibold bg-brand-100 text-brand-700">
											{org.name.charAt(0).toUpperCase()}
										</span>
										<span className="truncate">{org.name}</span>
									</button>
									<button
										type="button"
										data-testid="org-dashboard-link"
										aria-label={t("organization.dashboard")}
										onClick={() => {
											setActiveOrgCookie(org.id);
											setActiveOrgId(org.id);
											setOpen(false);
											navigate(dashboardPath(org));
										}}
										className="shrink-0 rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-brand-700"
									>
										<svg
											className="w-4 h-4"
											fill="none"
											viewBox="0 0 24 24"
											strokeWidth="1.5"
											stroke="currentColor"
											aria-hidden="true"
										>
											<path
												strokeLinecap="round"
												strokeLinejoin="round"
												d="m2.25 12 8.954-8.955c.44-.439 1.152-.439 1.591 0L21.75 12M4.5 9.75v10.125c0 .621.504 1.125 1.125 1.125H9.75v-4.875c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21h4.125c.621 0 1.125-.504 1.125-1.125V9.75M8.25 21h8.25"
											/>
										</svg>
									</button>
								</li>
							))}
						</ul>

						<div className="border-t border-gray-100">
							<button
								type="button"
								onClick={() => {
									setOpen(false);
									setShowModal(true);
								}}
								className="flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors text-brand-700 hover:bg-brand-50"
							>
								<svg
									className="w-4 h-4"
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
			</div>

			{showModal && (
				<CreateOrganizationModal
					onClose={() => setShowModal(false)}
					onSuccess={handleOrgCreated}
				/>
			)}
		</>
	);
}
