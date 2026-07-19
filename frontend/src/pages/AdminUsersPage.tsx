import { useEffect, useState } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import type { AdminUserListItem } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { getApiErrorMessage } from "../lib/apiError";
import { dispatchToast } from "../lib/toastBus";
import { inputClass } from "../lib/formClasses";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import AdminNav from "../components/AdminNav";
import EmptyState from "../components/EmptyState";

export default function AdminUsersPage() {
	const { t } = useTranslation();
	const auth = useAuth();
	const api = useApiClient();
	const currentUserId = auth.user?.profile?.sub;

	const [rows, setRows] = useState<AdminUserListItem[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [search, setSearch] = useState("");
	const [pendingUserId, setPendingUserId] = useState<string | null>(null);

	usePageTitle(t("adminUsers.title"));
	usePageToolbar([
		{ label: t("breadcrumb.home"), href: "/" },
		{ label: t("adminUsers.title") },
	]);

	function load(searchTerm: string) {
		setLoading(true);
		setError(null);
		api
			.listUsers(searchTerm.trim() || undefined)
			.then(setRows)
			.catch(() => setError(t("adminUsers.error")))
			.finally(() => setLoading(false));
	}

	useEffect(() => {
		load("");
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	function handleSearchSubmit(e: React.FormEvent) {
		e.preventDefault();
		load(search);
	}

	async function toggleEnabled(userId: string, next: boolean) {
		setPendingUserId(userId);
		try {
			await api.setUserEnabled(userId, { enabled: next });
			setRows((prev) =>
				prev.map((r) => (r.id === userId ? { ...r, enabled: next } : r)),
			);
			dispatchToast(
				"success",
				next ? t("adminUsers.unblockSuccess") : t("adminUsers.blockSuccess"),
			);
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(
					err,
					next ? t("adminUsers.unblockError") : t("adminUsers.blockError"),
				),
			);
		} finally {
			setPendingUserId(null);
		}
	}

	async function toggleAdmin(userId: string, next: boolean) {
		setPendingUserId(userId);
		try {
			await api.setUserAdminStatus(userId, { isAdmin: next });
			setRows((prev) =>
				prev.map((r) =>
					r.id === userId
						? {
								...r,
								realmRoles: next
									? [...r.realmRoles, "admin"]
									: r.realmRoles.filter((role) => role !== "admin"),
							}
						: r,
				),
			);
			dispatchToast(
				"success",
				next ? t("adminUsers.promoteSuccess") : t("adminUsers.demoteSuccess"),
			);
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(
					err,
					next ? t("adminUsers.promoteError") : t("adminUsers.demoteError"),
				),
			);
		} finally {
			setPendingUserId(null);
		}
	}

	return (
		<>
			<AdminNav />
			<h1 className="mb-6 text-2xl font-bold text-gray-900">
				{t("adminUsers.title")}
			</h1>

			<form onSubmit={handleSearchSubmit} className="mb-6 flex items-end gap-3">
				<div className="flex-1">
					<label
						htmlFor="admin-user-search"
						className="block text-xs text-gray-600"
					>
						{t("adminUsers.searchLabel")}
					</label>
					<input
						id="admin-user-search"
						type="search"
						value={search}
						onChange={(e) => setSearch(e.target.value)}
						placeholder={t("adminUsers.searchPlaceholder")}
						className={inputClass}
					/>
				</div>
				<button
					type="submit"
					className="rounded-lg bg-brand-700 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-brand-800"
				>
					{t("adminUsers.searchButton")}
				</button>
			</form>

			<p className="mb-4 text-xs text-gray-500">{t("adminUsers.staleness")}</p>

			{loading ? (
				<p className="text-gray-500">{t("adminUsers.loading")}</p>
			) : error ? (
				<p className="text-red-600">{error}</p>
			) : rows.length === 0 ? (
				<EmptyState title={t("adminUsers.noUsers")} />
			) : (
				<div className="overflow-hidden rounded-2xl border border-gray-200">
					<table className="w-full text-sm">
						<tbody className="divide-y divide-gray-100">
							{rows.map((row) => {
								const isSelf = row.id === currentUserId;
								const isAdmin = row.realmRoles.includes("admin");
								const isPending = pendingUserId === row.id;
								const displayName =
									row.firstName && row.lastName
										? `${row.firstName} ${row.lastName}`
										: row.username;

								return (
									<tr
										key={row.id}
										className="flex flex-wrap items-center gap-3 px-4 py-3"
									>
										<td className="min-w-0 flex-1">
											<p className="truncate font-medium text-gray-900">
												{displayName}
												{isAdmin && (
													<span className="ml-2 inline-block rounded-full bg-amber-50 px-2 py-0.5 text-xs font-normal text-amber-700">
														{t("adminUsers.adminBadge")}
													</span>
												)}
											</p>
											<p className="truncate text-xs text-gray-500">
												{row.username} &middot; {row.email}
											</p>
										</td>
										<td className="shrink-0">
											<span
												className={`rounded-full px-2 py-0.5 text-xs font-medium ${
													row.enabled
														? "bg-green-50 text-green-700"
														: "bg-red-50 text-red-700"
												}`}
											>
												{row.enabled
													? t("adminUsers.statusActive")
													: t("adminUsers.statusBlocked")}
											</span>
										</td>
										<td className="flex shrink-0 items-center gap-2">
											{isSelf ? (
												<span
													className="text-xs text-gray-500"
													title={t("adminUsers.selfActionDisabledHint")}
												>
													{t("adminUsers.selfActionDisabledHint")}
												</span>
											) : (
												<>
													<button
														type="button"
														disabled={isPending}
														onClick={() =>
															void toggleEnabled(row.id, !row.enabled)
														}
														className="rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:opacity-50"
													>
														{row.enabled
															? t("adminUsers.block")
															: t("adminUsers.unblock")}
													</button>
													<button
														type="button"
														disabled={isPending}
														onClick={() => void toggleAdmin(row.id, !isAdmin)}
														className="rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:opacity-50"
													>
														{isAdmin
															? t("adminUsers.demote")
															: t("adminUsers.promote")}
													</button>
												</>
											)}
										</td>
									</tr>
								);
							})}
						</tbody>
					</table>
				</div>
			)}
		</>
	);
}
