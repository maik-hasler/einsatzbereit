import { useEffect, useState } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../hooks/useApiClient";
import { runtimeConfig } from "../lib/runtimeConfig";
import { dispatchToast } from "../lib/toastBus";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";

interface OrgRow {
	id: string;
	name: string;
	isVerified: boolean;
}

export default function AdminOrganizationsPage() {
	const { t } = useTranslation();
	const auth = useAuth();
	const api = useApiClient();
	const [rows, setRows] = useState<OrgRow[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [toggling, setToggling] = useState<string | null>(null);

	usePageTitle(t("adminOrgs.title"));
	usePageToolbar([
		{ label: t("breadcrumb.home"), href: "/" },
		{ label: t("adminOrgs.title") },
	]);

	useEffect(() => {
		async function load() {
			try {
				const orgs = await api.getOrganizations();
				const profiles = await Promise.allSettled(
					orgs.map((o) => api.getPublicOrganizationProfile(o.id)),
				);
				setRows(
					orgs.map((o, i) => {
						const result = profiles[i];
						const isVerified =
							result.status === "fulfilled"
								? ((result.value?.isVerified as boolean | undefined) ?? false)
								: false;
						return { id: o.id, name: o.name, isVerified };
					}),
				);
			} catch {
				setError(t("adminOrgs.error"));
			} finally {
				setLoading(false);
			}
		}
		void load();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	async function toggleVerified(orgId: string, next: boolean) {
		setToggling(orgId);
		try {
			const res = await fetch(
				`${runtimeConfig.apiUrl}/v1/organizations/${orgId}/verify`,
				{
					method: "PUT",
					headers: {
						"Content-Type": "application/json",
						Authorization: `Bearer ${auth.user?.access_token ?? ""}`,
					},
					body: JSON.stringify({ isVerified: next }),
				},
			);
			if (!res.ok) throw new Error();
			setRows((prev) =>
				prev.map((r) => (r.id === orgId ? { ...r, isVerified: next } : r)),
			);
			dispatchToast("success", t("organizations.verifySuccess"));
		} catch {
			dispatchToast("error", t("organizations.verifyError"));
		} finally {
			setToggling(null);
		}
	}

	if (loading) return <p className="text-gray-500">{t("adminOrgs.loading")}</p>;
	if (error) return <p className="text-red-600">{error}</p>;
	if (rows.length === 0)
		return <p className="text-gray-500">{t("adminOrgs.noOrgs")}</p>;

	return (
		<>
			<h1 className="mb-6 text-2xl font-bold text-gray-900">
				{t("adminOrgs.title")}
			</h1>
			<div className="overflow-hidden rounded-2xl border border-gray-200">
				<table className="w-full text-sm">
					<tbody className="divide-y divide-gray-100">
						{rows.map((row) => (
							<tr key={row.id} className="flex items-center gap-4 px-4 py-3">
								<td className="flex flex-1 items-center gap-2 font-medium text-gray-900">
									{row.name}
									{row.isVerified && (
										<svg
											className="h-4 w-4 shrink-0 text-brand-600"
											viewBox="0 0 20 20"
											fill="currentColor"
											aria-label={t("organizations.verified")}
											role="img"
										>
											<path
												fillRule="evenodd"
												d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm3.857-9.809a.75.75 0 0 0-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 1 0-1.06 1.061l2.5 2.5a.75.75 0 0 0 1.137-.089l4-5.5Z"
												clipRule="evenodd"
											/>
										</svg>
									)}
								</td>
								<td>
									{row.isVerified ? (
										<button
											type="button"
											disabled={toggling === row.id}
											onClick={() => void toggleVerified(row.id, false)}
											className="rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:opacity-50"
										>
											{t("adminOrgs.unverify")}
										</button>
									) : (
										<button
											type="button"
											disabled={toggling === row.id}
											onClick={() => void toggleVerified(row.id, true)}
											className="rounded-lg bg-brand-600 px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-brand-700 disabled:opacity-50"
										>
											{t("adminOrgs.verify")}
										</button>
									)}
								</td>
							</tr>
						))}
					</tbody>
				</table>
			</div>
		</>
	);
}
