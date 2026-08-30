import { lazy, Suspense, useState } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	Organization,
	OrganizationSummaryDto,
} from "../../client/api-client";
import { canViewOrgTab, orgTabPath } from "../../lib/orgTabs";
import { splitForMiddleTruncation } from "../../lib/middleTruncateSplit";
import { useDismissableOverlay } from "../../hooks/useDismissableOverlay";
import OrgAvatar from "../OrgAvatar";
import ModalLoadingFallback from "../ModalLoadingFallback";
import Skeleton from "../Skeleton";
import { ChevronDownIcon, PlusIcon } from "../icons";

const CreateOrganizationModal = lazy(
	() => import("../CreateOrganizationModal"),
);

export default function OrganizationSwitcher({
	currentOrgId,
	currentTab,
	orgs,
	loading,
	error,
	transparent = false,
}: {
	currentOrgId: string;
	currentTab: string;

	orgs: OrganizationSummaryDto[];
	loading: boolean;

	error: string | null;
	transparent?: boolean;
}) {
	const navigate = useNavigate();
	const { t } = useTranslation();
	const [open, setOpen] = useState(false);
	const [showModal, setShowModal] = useState(false);
	const containerRef = useDismissableOverlay<HTMLDivElement>(open, () =>
		setOpen(false),
	);

	const currentOrg = orgs.find((o) => o.id === currentOrgId) ?? null;
	const [currentOrgNameHead, currentOrgNameTail] = currentOrg
		? splitForMiddleTruncation(currentOrg.name)
		: ["", ""];

	function orgPath(org: OrganizationSummaryDto) {
		// Switching organizations keeps you on the same section - unless that
		// section does not exist for your role over there. A plain member of the
		// target org has no sign-ups tab to land on (#2316), so they get its
		// dashboard instead.
		const tab = canViewOrgTab(currentTab, org.role === "Organizer")
			? currentTab
			: "dashboard";
		return orgTabPath(org.id, tab);
	}

	function handleSwitch(org: OrganizationSummaryDto) {
		setOpen(false);
		if (org.id === currentOrgId) return;
		navigate(orgPath(org));
	}

	function handleOrgCreated(newOrg: Organization) {
		setShowModal(false);
		navigate(`/app/${newOrg.id?.value}/dashboard`);
	}

	if (loading) {
		return <Skeleton className="h-11 w-48 rounded-lg" />;
	}

	return (
		<>
			<div className="relative" ref={containerRef}>
				<button
					type="button"
					onClick={() => setOpen(!open)}
					className={`flex min-h-11 w-full min-w-0 items-center gap-2 rounded-xl border px-3 py-1.5 text-sm font-medium transition-colors ${
						error
							? "border-red-200 bg-red-50 text-red-700 hover:bg-red-100"
							: transparent
								? "border-white/30 bg-white/10 text-white hover:bg-white/20"
								: "border-gray-200 bg-white text-gray-700 hover:bg-gray-50"
					}`}
					aria-expanded={open}
					aria-label={
						error
							? t("organization.switchLabel")
							: currentOrg
								? t("organization.switchLabelCurrent", {
										name: currentOrg.name,
									})
								: t("organization.selectPlaceholder")
					}
				>
					{!error && (
						<OrgAvatar
							name={currentOrg?.name ?? ""}
							logoUrl={currentOrg?.logoUrl}
						/>
					)}

					<span
						data-testid="org-switcher-current-name"
						title={error ? undefined : currentOrg?.name}
						className="flex max-w-85 min-w-0 flex-1 overflow-hidden sm:min-w-24"
					>
						{error ? (
							t("organization.loadError")
						) : currentOrg ? (
							<>
								<span
									data-testid="org-switcher-current-name-head"
									className="min-w-0 truncate"
								>
									{currentOrgNameHead}
								</span>
								<span
									data-testid="org-switcher-current-name-tail"
									className="shrink-0 whitespace-nowrap"
								>
									{currentOrgNameTail}
								</span>
							</>
						) : (
							t("organization.selectPlaceholder")
						)}
					</span>
					<ChevronDownIcon
						open={open}
						className={`h-3.5 w-3.5 shrink-0 ${error ? "text-red-400" : transparent ? "text-white/70" : "text-gray-400"}`}
					/>
				</button>

				{open && (
					<div
						className={`absolute top-full left-0 z-50 mt-2 w-64 rounded-lg border shadow-modal ${transparent ? "border-white/20 bg-brand-800" : "border-gray-200 bg-white"}`}
					>
						<ul
							aria-label={t("organization.switchLabel")}
							className="max-h-60 overflow-y-auto py-1"
						>
							{orgs.map((org) => (
								<li key={org.id}>
									<button
										type="button"
										data-testid="org-switch-row"
										onClick={() => handleSwitch(org)}
										aria-current={org.id === currentOrgId ? "page" : undefined}
										className={`flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors ${
											transparent
												? org.id === currentOrgId
													? "bg-white/15 font-medium text-white"
													: "text-white/80 hover:bg-white/10 hover:text-white"
												: org.id === currentOrgId
													? "bg-brand-50 font-medium text-brand-700"
													: "text-gray-700 hover:bg-gray-50"
										}`}
									>
										<OrgAvatar name={org.name} logoUrl={org.logoUrl} lazy />

										<span className="min-w-0 flex-1">{org.name}</span>
									</button>
								</li>
							))}
						</ul>

						<div
							className={`border-t ${transparent ? "border-white/20" : "border-gray-100"}`}
						>
							<button
								type="button"
								onClick={() => {
									setOpen(false);
									setShowModal(true);
								}}
								className={`flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors ${transparent ? "text-white hover:bg-white/10" : "text-brand-700 hover:bg-brand-50"}`}
							>
								<PlusIcon className="h-4 w-4" />
								{t("organization.create")}
							</button>
						</div>
					</div>
				)}
			</div>

			{showModal && (
				<Suspense
					fallback={
						<ModalLoadingFallback onClose={() => setShowModal(false)} />
					}
				>
					<CreateOrganizationModal
						onClose={() => setShowModal(false)}
						onSuccess={handleOrgCreated}
					/>
				</Suspense>
			)}
		</>
	);
}
