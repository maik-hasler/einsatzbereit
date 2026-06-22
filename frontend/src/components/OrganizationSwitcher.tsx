import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import type { KeycloakOrganization } from "../client/api-client";
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
	const [orgs, setOrgs] = useState<KeycloakOrganization[]>([]);
	const [activeOrgId, setActiveOrgId] = useState<string | null>(getActiveOrgId);
	const [loading, setLoading] = useState(true);
	const [open, setOpen] = useState(false);
	const [showModal, setShowModal] = useState(false);
	const containerRef = useRef<HTMLDivElement>(null);

	const activeOrg = orgs.find((o) => o.id === activeOrgId) ?? null;

	const fetchOrgs = () => {
		setLoading(true);
		api
			.getOrganizations()
			.then((data: KeycloakOrganization[]) => {
				setOrgs(data);
				// Auto-select first org if none is active
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

	const handleSwitch = (org: KeycloakOrganization) => {
		setActiveOrgCookie(org.id);
		setActiveOrgId(org.id);
		setOpen(false);
	};

	const handleOrgCreated = () => {
		const prevIds = new Set(orgs.map((o) => o.id));
		setLoading(true);
		api
			.getOrganizations()
			.then((data: KeycloakOrganization[]) => {
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
					<div className="absolute left-0 top-full mt-2 w-64 rounded-lg border shadow-lg z-50 bg-white border-gray-200">
						<div className="py-1 max-h-60 overflow-y-auto">
							{orgs.map((org) => (
								<button
									key={org.id}
									type="button"
									onClick={() => handleSwitch(org)}
									className={`flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors ${
										org.id === activeOrgId
											? "bg-brand-50 text-brand-700 font-medium"
											: "text-gray-700 hover:bg-gray-50"
									}`}
								>
									<span className="flex h-7 w-7 items-center justify-center rounded-md text-xs font-semibold bg-brand-100 text-brand-700">
										{org.name.charAt(0).toUpperCase()}
									</span>
									<span className="truncate">{org.name}</span>
									{org.id === activeOrgId && (
										<svg
											className="ml-auto w-4 h-4 text-brand-500"
											fill="none"
											viewBox="0 0 24 24"
											strokeWidth="2"
											stroke="currentColor"
										>
											<path
												strokeLinecap="round"
												strokeLinejoin="round"
												d="m4.5 12.75 6 6 9-13.5"
											/>
										</svg>
									)}
								</button>
							))}
						</div>

						<div className="border-t border-gray-100">
							{activeOrgId && (
								<>
									<button
										type="button"
										data-testid="org-dashboard-link"
										onClick={() => {
											setOpen(false);
											navigate(`/organizations/${activeOrgId}/dashboard`);
										}}
										className="flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors text-gray-700 hover:bg-gray-50"
									>
										<svg
											className="w-4 h-4 text-gray-400"
											fill="none"
											viewBox="0 0 24 24"
											strokeWidth="1.5"
											stroke="currentColor"
										>
											<path
												strokeLinecap="round"
												strokeLinejoin="round"
												d="M3.75 6A2.25 2.25 0 0 1 6 3.75h2.25A2.25 2.25 0 0 1 10.5 6v2.25a2.25 2.25 0 0 1-2.25 2.25H6a2.25 2.25 0 0 1-2.25-2.25V6ZM3.75 15.75A2.25 2.25 0 0 1 6 13.5h2.25a2.25 2.25 0 0 1 2.25 2.25V18a2.25 2.25 0 0 1-2.25 2.25H6A2.25 2.25 0 0 1 3.75 18v-2.25ZM13.5 6a2.25 2.25 0 0 1 2.25-2.25H18A2.25 2.25 0 0 1 20.25 6v2.25A2.25 2.25 0 0 1 18 10.5h-2.25a2.25 2.25 0 0 1-2.25-2.25V6ZM13.5 15.75a2.25 2.25 0 0 1 2.25-2.25H18a2.25 2.25 0 0 1 2.25 2.25V18A2.25 2.25 0 0 1 18 20.25h-2.25A2.25 2.25 0 0 1 13.5 18v-2.25Z"
											/>
										</svg>
										{t("organization.dashboard")}
									</button>
									<button
										type="button"
										data-testid="org-engagements-link"
										onClick={() => {
											setOpen(false);
											navigate(
												`/organizations/${activeOrgId}/dashboard?tab=engagements`,
											);
										}}
										className="flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors text-gray-700 hover:bg-gray-50"
									>
										<svg
											className="w-4 h-4 text-gray-400"
											fill="none"
											viewBox="0 0 24 24"
											strokeWidth="1.5"
											stroke="currentColor"
											aria-hidden="true"
										>
											<path
												strokeLinecap="round"
												strokeLinejoin="round"
												d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
											/>
										</svg>
										{t("organization.engagements")}
									</button>
									<button
										type="button"
										data-testid="org-settings-link"
										onClick={() => {
											setOpen(false);
											navigate(
												`/organizations/${activeOrgId}/dashboard?tab=settings`,
											);
										}}
										className="flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors text-gray-700 hover:bg-gray-50"
									>
										<svg
											className="w-4 h-4 text-gray-400"
											fill="none"
											viewBox="0 0 24 24"
											strokeWidth="1.5"
											stroke="currentColor"
										>
											<path
												strokeLinecap="round"
												strokeLinejoin="round"
												d="M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.325.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 0 1 1.37.49l1.296 2.247a1.125 1.125 0 0 1-.26 1.431l-1.003.827c-.293.241-.438.613-.43.992a7.723 7.723 0 0 1 0 .255c-.008.378.137.75.43.991l1.004.827c.424.35.534.955.26 1.43l-1.298 2.247a1.125 1.125 0 0 1-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.47 6.47 0 0 1-.22.128c-.331.183-.581.495-.644.869l-.213 1.281c-.09.543-.56.94-1.11.94h-2.594c-.55 0-1.019-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 0 1-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 0 1-1.369-.49l-1.297-2.247a1.125 1.125 0 0 1 .26-1.431l1.004-.827c.292-.24.437-.613.43-.991a6.932 6.932 0 0 1 0-.255c.007-.38-.138-.751-.43-.992l-1.004-.827a1.125 1.125 0 0 1-.26-1.43l1.297-2.247a1.125 1.125 0 0 1 1.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.086.22-.128.332-.183.582-.495.644-.869l.214-1.28Z"
											/>
											<path
												strokeLinecap="round"
												strokeLinejoin="round"
												d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
											/>
										</svg>
										{t("organization.settings")}
									</button>
								</>
							)}
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
