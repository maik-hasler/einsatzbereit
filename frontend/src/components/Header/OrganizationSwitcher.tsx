import { lazy, Suspense, useState } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	Organization,
	OrganizationSummaryDto,
} from "../../client/api-client";
import { orgTabPath } from "../../lib/orgTabs";
import { splitForMiddleTruncation } from "../../lib/middleTruncateSplit";
import { useDismissableOverlay } from "../../hooks/useDismissableOverlay";
import OrgAvatar from "./OrgAvatar";
import ModalLoadingFallback from "../ModalLoadingFallback";
import Skeleton from "../Skeleton";
import { ChevronDownIcon, PlusIcon } from "../icons";

// Lazy-loaded: this component is statically imported by Header, which is
// eager on every route (including the anonymous landing page) - without
// this, react-hook-form + zod stay in the entry chunk regardless of whether
// HomePage's own CreateOrganizationModal usage is lazy-loaded, since
// bundlers keep a module in its statically-importing chunk even when a
// *different* call site imports it dynamically (#1728).
const CreateOrganizationModal = lazy(
	() => import("../CreateOrganizationModal"),
);

export default function OrganizationSwitcher({
	currentOrgId,
	currentTab,
	orgs,
	loading,
	error,
}: {
	currentOrgId: string;
	currentTab: string;
	// Fetched by the parent Header (which needs the same list for its own
	// nav-gating logic) so this component isn't firing a second, identical
	// getOrganizations() request on every org-app-shell page load.
	orgs: OrganizationSummaryDto[];
	loading: boolean;
	// Set when the shared organizations fetch failed, so this can be told
	// apart from a genuine "user has zero organizations" empty list.
	error: string | null;
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
		return orgTabPath(org.id, currentTab);
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
		return <Skeleton className="h-9 w-48 rounded-lg" />;
	}

	return (
		<>
			<div className="relative" ref={containerRef}>
				<button
					type="button"
					onClick={() => setOpen(!open)}
					className={`flex w-full min-w-0 items-center gap-2 rounded-xl border px-3 py-1.5 text-sm font-medium transition-colors ${
						error
							? "border-red-200 bg-red-50 text-red-700 hover:bg-red-100"
							: "border-gray-200 bg-white text-gray-700 hover:bg-gray-50"
					}`}
					aria-expanded={open}
					aria-label={t("organization.switchLabel")}
				>
					{!error && (
						// No name until an org resolves - OrgAvatar renders "?" for it.
						<OrgAvatar
							name={currentOrg?.name ?? ""}
							logoUrl={currentOrg?.logoUrl}
						/>
					)}
					{/* Middle-ellipsis, not end-truncation: two names sharing a
					leading word (e.g. two seeded orgs both starting "Lindenauer")
					used to become indistinguishable once cut. Split into a
					`truncate` head (grows the browser's own end-ellipsis when
					squeezed) and a `shrink-0` tail (never truncated) rather than
					doing this in JS, so the DOM text stays the exact, full org
					name - only the *rendered* width adapts - and a title
					attribute still covers hover regardless of how much is
					visible (#2080). */}
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
						className={`h-3.5 w-3.5 shrink-0 ${error ? "text-red-400" : "text-gray-400"}`}
					/>
				</button>

				{open && (
					<div className="absolute top-full left-0 z-50 mt-2 w-64 rounded-lg border border-gray-200 bg-white shadow-modal">
						<ul className="max-h-60 overflow-y-auto py-1">
							{orgs.map((org) => (
								<li key={org.id}>
									<button
										type="button"
										data-testid="org-switch-row"
										onClick={() => handleSwitch(org)}
										aria-current={org.id === currentOrgId ? "page" : undefined}
										className={`flex w-full items-center gap-2 px-3 py-2 text-left text-sm ${
											org.id === currentOrgId
												? "bg-brand-50 font-medium text-brand-700"
												: "text-gray-700 hover:bg-gray-50"
										}`}
									>
										<OrgAvatar name={org.name} logoUrl={org.logoUrl} lazy />
										{/* No truncate here (unlike the collapsed trigger above) -
										the panel already has the width to spare, so a long name
										wraps onto a second line instead of being cut off (#1907). */}
										<span className="min-w-0 flex-1">{org.name}</span>
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
								className="flex w-full items-center gap-3 px-4 py-2.5 text-sm text-brand-700 transition-colors hover:bg-brand-50"
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
