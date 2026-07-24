import { useState } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import type { AdminUserListItem } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { useLoadMore } from "../hooks/useLoadMore";
import { getApiErrorMessage } from "../lib/apiError";
import { dispatchToast } from "../lib/toastBus";
import { inputClass } from "../lib/formClasses";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import Spinner from "../components/Spinner";
import EmptyState from "../components/EmptyState";

const PAGE_SIZE = 10;

interface OrgRow {
	id: string;
	name: string;
	isVerified: boolean;
}

export default function AdministrationPage() {
	const { t } = useTranslation();
	usePageTitle(t("administration.title"));
	usePageToolbar([{ label: t("administration.title") }]);

	return (
		<>
			<h1 className="mb-6 text-2xl font-bold text-gray-900">
				{t("administration.title")}
			</h1>
			<section className="mb-10">
				<h2 className="mb-4 text-lg font-semibold text-gray-900">
					{t("administration.organizationsHeading")}
				</h2>
				<OrganizationsSection />
			</section>
			<section>
				<h2 className="mb-4 text-lg font-semibold text-gray-900">
					{t("administration.usersHeading")}
				</h2>
				<UsersSection />
			</section>
		</>
	);
}

function LoadMoreButton({
	loading,
	label,
	onClick,
}: {
	loading: boolean;
	label: string;
	onClick: () => void;
}) {
	const { t } = useTranslation();
	return (
		<div className="mt-4 flex justify-center">
			<button
				type="button"
				onClick={onClick}
				disabled={loading}
				className="rounded-xl border border-brand-200 bg-brand-50 px-8 py-3 text-sm font-semibold text-brand-700 transition-colors hover:bg-brand-100 disabled:opacity-40"
			>
				{loading ? t("administration.loadingMore") : label}
			</button>
		</div>
	);
}

function OrganizationsSection() {
	const { t } = useTranslation();
	const api = useApiClient();

	const [toggling, setToggling] = useState<string | null>(null);

	const {
		items: rows,
		setItems: setRows,
		loading,
		loadingMore,
		error,
		hasMore,
		loadMore,
	} = useLoadMore<OrgRow>(
		(pageNumber) =>
			api.listOrganizations(pageNumber, PAGE_SIZE).then((result) => ({
				items: result.items.map((o) => ({
					id: o.id,
					name: o.name,
					isVerified: o.isVerified,
				})),
				pageCount: result.pageCount,
			})),
		{ getErrorMessage: () => t("administration.organizations.error") },
	);

	async function toggleVerified(orgId: string, next: boolean) {
		setToggling(orgId);
		try {
			await api.verifyOrganization(orgId, { isVerified: next });
			setRows((prev) =>
				prev.map((r) => (r.id === orgId ? { ...r, isVerified: next } : r)),
			);
			dispatchToast("success", t("organizations.verifySuccess"));
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(err, t("organizations.verifyError")),
			);
		} finally {
			setToggling(null);
		}
	}

	if (loading) {
		return (
			<div className="flex items-center justify-center py-16">
				<Spinner label={t("administration.organizations.loading")} />
			</div>
		);
	}
	if (error) return <p className="text-red-600">{error}</p>;
	if (rows.length === 0)
		return (
			<EmptyState title={t("administration.organizations.noOrganizations")} />
		);

	return (
		<>
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
											{t("administration.organizations.unverify")}
										</button>
									) : (
										<button
											type="button"
											disabled={toggling === row.id}
											onClick={() => void toggleVerified(row.id, true)}
											className="rounded-lg bg-brand-700 px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-brand-800 disabled:opacity-50"
										>
											{t("administration.organizations.verify")}
										</button>
									)}
								</td>
							</tr>
						))}
					</tbody>
				</table>
			</div>
			{hasMore && (
				<LoadMoreButton
					loading={loadingMore}
					label={t("administration.organizations.loadMore")}
					onClick={loadMore}
				/>
			)}
		</>
	);
}

function UsersSection() {
	const { t } = useTranslation();
	const auth = useAuth();
	const api = useApiClient();
	const currentUserId = auth.user?.profile?.sub;

	const [search, setSearch] = useState("");
	const [appliedSearch, setAppliedSearch] = useState("");
	const [pendingUserId, setPendingUserId] = useState<string | null>(null);

	const {
		items: rows,
		setItems: setRows,
		loading,
		loadingMore,
		error,
		hasMore,
		loadMore,
		reset,
	} = useLoadMore<AdminUserListItem>(
		(pageNumber) =>
			api.listUsers(appliedSearch.trim() || undefined, pageNumber, PAGE_SIZE),
		{ getErrorMessage: () => t("administration.users.error") },
	);

	function handleSearchSubmit(e: React.FormEvent) {
		e.preventDefault();
		setAppliedSearch(search);
		reset();
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
				next
					? t("administration.users.unblockSuccess")
					: t("administration.users.blockSuccess"),
			);
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(
					err,
					next
						? t("administration.users.unblockError")
						: t("administration.users.blockError"),
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
				next
					? t("administration.users.promoteSuccess")
					: t("administration.users.demoteSuccess"),
			);
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(
					err,
					next
						? t("administration.users.promoteError")
						: t("administration.users.demoteError"),
				),
			);
		} finally {
			setPendingUserId(null);
		}
	}

	return (
		<>
			<form onSubmit={handleSearchSubmit} className="mb-6 flex items-end gap-3">
				<div className="flex-1">
					<label
						htmlFor="admin-user-search"
						className="block text-xs text-gray-600"
					>
						{t("administration.users.searchLabel")}
					</label>
					<input
						id="admin-user-search"
						type="search"
						value={search}
						onChange={(e) => setSearch(e.target.value)}
						placeholder={t("administration.users.searchPlaceholder")}
						className={inputClass}
					/>
				</div>
				<button
					type="submit"
					className="rounded-lg bg-brand-700 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-brand-800"
				>
					{t("administration.users.searchButton")}
				</button>
			</form>

			<p className="mb-4 text-xs text-gray-500">
				{t("administration.users.staleness")}
			</p>

			{loading ? (
				<div className="flex items-center justify-center py-16">
					<Spinner label={t("administration.users.loading")} />
				</div>
			) : error ? (
				<p className="text-red-600">{error}</p>
			) : rows.length === 0 ? (
				<EmptyState title={t("administration.users.noUsers")} />
			) : (
				<>
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
															{t("administration.users.adminBadge")}
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
														? t("administration.users.statusActive")
														: t("administration.users.statusBlocked")}
												</span>
											</td>
											<td className="flex shrink-0 items-center gap-2">
												{isSelf ? (
													<span
														className="text-xs text-gray-500"
														title={t(
															"administration.users.selfActionDisabledHint",
														)}
													>
														{t("administration.users.selfActionDisabledHint")}
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
																? t("administration.users.block")
																: t("administration.users.unblock")}
														</button>
														<button
															type="button"
															disabled={isPending}
															onClick={() => void toggleAdmin(row.id, !isAdmin)}
															className="rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:opacity-50"
														>
															{isAdmin
																? t("administration.users.demote")
																: t("administration.users.promote")}
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
					{hasMore && (
						<LoadMoreButton
							loading={loadingMore}
							label={t("administration.users.loadMore")}
							onClick={loadMore}
						/>
					)}
				</>
			)}
		</>
	);
}
