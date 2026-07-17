import { useEffect, useState } from "react";
import { Navigate, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import type { OrganizationSummaryDto } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { usePageTitle } from "../../hooks/usePageTitle";
import { usePageToolbar } from "../../contexts/ToolbarContext";
import CreateOrganizationModal from "../../components/CreateOrganizationModal";
import EmptyState from "../../components/EmptyState";

export default function OrgAppEntryPage() {
	const api = useApiClient();
	const navigate = useNavigate();
	const { t } = useTranslation();
	usePageTitle(t("orgAppEntry.title"));
	usePageToolbar([
		{ label: t("breadcrumb.home"), href: "/" },
		{ label: t("breadcrumb.orgApp") },
	]);

	const [orgs, setOrgs] = useState<OrganizationSummaryDto[] | null>(null);
	const [showCreateModal, setShowCreateModal] = useState(false);

	useEffect(() => {
		let cancelled = false;
		api
			.getOrganizations()
			.then((data) => {
				if (!cancelled) setOrgs(data);
			})
			.catch(() => {
				if (!cancelled) setOrgs([]);
			});
		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	if (orgs === null) {
		return (
			<div className="flex justify-center py-16">
				<span className="text-gray-500">{t("orgAppEntry.loading")}</span>
			</div>
		);
	}

	if (orgs.length === 1) {
		return <Navigate to={`/app/${orgs[0].id}/dashboard`} replace />;
	}

	if (orgs.length === 0) {
		return (
			<>
				<EmptyState
					title={t("orgAppEntry.emptyTitle")}
					message={t("orgAppEntry.emptyDesc")}
					action={{
						label: t("organization.create"),
						onClick: () => setShowCreateModal(true),
					}}
				/>

				{showCreateModal && (
					<CreateOrganizationModal
						onClose={() => setShowCreateModal(false)}
						onSuccess={(org) => navigate(`/app/${org.id?.value}/dashboard`)}
					/>
				)}
			</>
		);
	}

	return (
		<div className="mx-auto max-w-xl">
			<h1 className="text-xl font-bold text-gray-900">
				{t("orgAppEntry.pickerTitle")}
			</h1>
			<p className="mt-1 text-sm text-gray-600">
				{t("orgAppEntry.pickerDesc")}
			</p>
			<ul className="mt-6 space-y-3">
				{orgs.map((org) => (
					<li key={org.id}>
						<button
							type="button"
							data-testid="org-entry-picker-row"
							onClick={() => navigate(`/app/${org.id}/dashboard`)}
							className="flex w-full items-center gap-3 rounded-xl border border-gray-200 bg-white px-5 py-4 text-left shadow-sm transition-colors hover:border-brand-200 hover:shadow-md"
						>
							{org.logoUrl ? (
								<img
									src={org.logoUrl}
									alt=""
									className="h-10 w-10 shrink-0 rounded-lg object-cover"
								/>
							) : (
								<span
									className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-brand-100 text-base font-semibold text-brand-700"
									aria-hidden="true"
								>
									{org.name.charAt(0).toUpperCase()}
								</span>
							)}
							<span className="min-w-0 flex-1 truncate font-medium text-gray-900">
								{org.name}
							</span>
							<svg
								className="h-4 w-4 shrink-0 text-gray-400"
								fill="none"
								viewBox="0 0 24 24"
								strokeWidth="2"
								stroke="currentColor"
								aria-hidden="true"
							>
								<path
									strokeLinecap="round"
									strokeLinejoin="round"
									d="m8.25 4.5 7.5 7.5-7.5 7.5"
								/>
							</svg>
						</button>
					</li>
				))}
			</ul>
		</div>
	);
}
