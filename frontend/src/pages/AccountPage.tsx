import { useEffect, useState } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../hooks/useApiClient";
import type { MyProfileResponse } from "../client/api-client";
import { usePageToolbar } from "../contexts/ToolbarContext";

export default function AccountPage() {
	const auth = useAuth();
	const api = useApiClient();
	const { t } = useTranslation();

	usePageToolbar([
		{ label: t("breadcrumb.home"), href: "/" },
		{ label: t("breadcrumb.account") },
	]);
	const [profile, setProfile] = useState<MyProfileResponse | null>(null);
	const [loading, setLoading] = useState(true);
	const [saving, setSaving] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [successMessage, setSuccessMessage] = useState<string | null>(null);

	const [form, setForm] = useState({
		firstName: "",
		lastName: "",
	});

	useEffect(() => {
		let cancelled = false;
		// Retry transient failures (e.g. the backend's /users/me briefly failing
		// while Keycloak warms up) instead of leaving the page blank forever.
		const retryDelaysMs = [500, 1000, 2000];

		async function loadProfile() {
			setLoading(true);
			for (let attempt = 0; ; attempt++) {
				try {
					const data = await api.getUserProfile();
					if (cancelled) return;
					setProfile(data);
					setForm({
						firstName: data.firstName ?? "",
						lastName: data.lastName ?? "",
					});
					setError(null);
					return;
				} catch {
					if (cancelled) return;
					if (attempt >= retryDelaysMs.length) {
						setError(t("account.loadError"));
						return;
					}
					await new Promise<void>((resolve) =>
						setTimeout(resolve, retryDelaysMs[attempt]),
					);
				}
			}
		}

		loadProfile().finally(() => {
			if (!cancelled) setLoading(false);
		});

		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	async function handleSave(e: React.FormEvent) {
		e.preventDefault();
		setSaving(true);
		setError(null);
		setSuccessMessage(null);
		try {
			await api.updateUserProfile({
				firstName: form.firstName || undefined,
				lastName: form.lastName || undefined,
			});
			setSuccessMessage(t("account.savedSuccess"));
			setProfile((prev) =>
				prev
					? {
							...prev,
							firstName: form.firstName || undefined,
							lastName: form.lastName || undefined,
						}
					: prev,
			);
		} catch {
			setError(t("account.saveError"));
		} finally {
			setSaving(false);
		}
	}

	if (loading) {
		return (
			<div className="flex items-center justify-center py-16">
				<span className="text-gray-500">{t("account.loading")}</span>
			</div>
		);
	}

	const displayName = (auth.user?.profile?.name ??
		auth.user?.profile?.preferred_username ??
		"") as string;

	return (
		<div className="mx-auto max-w-2xl">
			<div className="mb-6 flex items-center gap-4">
				<div className="flex h-16 w-16 items-center justify-center rounded-full bg-brand-700 text-xl font-semibold text-white">
					{getInitials(displayName)}
				</div>
				<div>
					<h1 className="text-2xl font-bold text-gray-900">
						{t("account.title")}
					</h1>
					{profile && (
						<p className="text-sm text-gray-500">@{profile.username}</p>
					)}
				</div>
			</div>

			{error && (
				<div className="mb-4 rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
					{error}
				</div>
			)}
			{successMessage && (
				<div className="mb-4 rounded-md bg-green-50 px-4 py-3 text-sm text-green-700">
					{successMessage}
				</div>
			)}

			<form onSubmit={handleSave} className="space-y-5">
				<Field label={t("account.fieldUsername")} id="username">
					<input
						id="username"
						disabled
						value={profile?.username ?? ""}
						className={`${inputClass} cursor-not-allowed bg-gray-50 text-gray-500`}
					/>
				</Field>

				<Field label={t("account.fieldEmail")} id="email">
					<input
						id="email"
						disabled
						type="email"
						value={profile?.email ?? ""}
						className={`${inputClass} cursor-not-allowed bg-gray-50 text-gray-500`}
					/>
					<p className="mt-1 text-xs text-gray-400">{t("account.emailHint")}</p>
				</Field>

				<Field label={t("account.fieldFirstName")} id="first-name">
					<input
						id="first-name"
						value={form.firstName}
						onChange={(e) =>
							setForm((f) => ({ ...f, firstName: e.target.value }))
						}
						className={inputClass}
					/>
				</Field>

				<Field label={t("account.fieldLastName")} id="last-name">
					<input
						id="last-name"
						value={form.lastName}
						onChange={(e) =>
							setForm((f) => ({ ...f, lastName: e.target.value }))
						}
						className={inputClass}
					/>
				</Field>

				<div className="flex justify-end">
					<button
						type="submit"
						disabled={saving}
						className="rounded-md bg-gray-900 px-5 py-2 text-sm font-medium text-white hover:bg-gray-700 disabled:opacity-50"
					>
						{saving ? t("account.saving") : t("account.save")}
					</button>
				</div>
			</form>
		</div>
	);
}

function getInitials(name: string): string {
	const parts = name.trim().split(/\s+/);
	if (parts.length > 1) return (parts[0][0] + parts[1][0]).toUpperCase();
	if (name.length >= 2) return name.slice(0, 2).toUpperCase();
	return name.toUpperCase();
}

const inputClass =
	"mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-900 focus:outline-none";

function Field({
	label,
	id,
	children,
}: {
	label: string;
	id?: string;
	children: React.ReactNode;
}) {
	return (
		<div>
			<label htmlFor={id} className="block text-sm font-medium text-gray-700">
				{label}
			</label>
			{children}
		</div>
	);
}
