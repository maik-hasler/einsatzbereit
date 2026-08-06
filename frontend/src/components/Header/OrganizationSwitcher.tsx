import { useState } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	Organization,
	OrganizationSummaryDto,
} from "../../client/api-client";
import { orgTabPath } from "../../lib/orgTabs";
import { useDismissableOverlay } from "../../hooks/useDismissableOverlay";
import CreateOrganizationModal from "../CreateOrganizationModal";
import Skeleton from "../Skeleton";
import { ChevronDownIcon, PlusIcon } from "../icons";

export default function OrganizationSwitcher({
	currentOrgId,
	currentTab,
	orgs,
	loading,
}: {
	currentOrgId: string;
	currentTab: string;
	// Fetched by the parent Header (which needs the same list for its own
	// nav-gating logic) so this component isn't firing a second, identical
	// getOrganizations() request on every org-app-shell page load.
	orgs: OrganizationSummaryDto[];
	loading: boolean;
}) {
	const navigate = useNavigate();
	const { t } = useTranslation();
	const [open, setOpen] = useState(false);
	const [showModal, setShowModal] = useState(false);
	const containerRef = useDismissableOverlay<HTMLDivElement>(open, () =>
		setOpen(false),
	);

	const currentOrg = orgs.find((o) => o.id === currentOrgId) ?? null;

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
					className="flex w-full min-w-0 items-center gap-2 rounded-xl border border-gray-200 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50"
					aria-expanded={open}
					aria-label={t("organization.switchLabel")}
				>
					{currentOrg?.logoUrl ? (
						<img
							src={currentOrg.logoUrl}
							alt=""
							width={24}
							height={24}
							className="h-6 w-6 shrink-0 rounded-md object-cover"
						/>
					) : (
						<span
							className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md bg-brand-100 text-xs font-semibold text-brand-700 before:content-[attr(data-initial)]"
							aria-hidden="true"
							data-initial={(currentOrg?.name ?? "?").charAt(0).toUpperCase()}
						/>
					)}
					<span
						data-testid="org-switcher-current-name"
						className="max-w-50 flex-1 truncate sm:min-w-24"
					>
						{currentOrg?.name ?? t("organization.selectPlaceholder")}
					</span>
					<ChevronDownIcon
						open={open}
						className="h-3.5 w-3.5 shrink-0 text-gray-400"
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
										{org.logoUrl ? (
											<img
												src={org.logoUrl}
												alt=""
												width={24}
												height={24}
												loading="lazy"
												className="h-6 w-6 shrink-0 rounded-md object-cover"
											/>
										) : (
											<span
												className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md bg-brand-100 text-xs font-semibold text-brand-700 before:content-[attr(data-initial)]"
												aria-hidden="true"
												data-initial={org.name.charAt(0).toUpperCase()}
											/>
										)}
										<span className="truncate">{org.name}</span>
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
				<CreateOrganizationModal
					onClose={() => setShowModal(false)}
					onSuccess={handleOrgCreated}
				/>
			)}
		</>
	);
}
