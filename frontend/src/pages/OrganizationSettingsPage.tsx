import { useEffect, useRef, useState } from "react";
import { useParams, Link } from "react-router";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../hooks/useApiClient";
import type { OrganizationDetailsResponse } from "../client/api-client";
import EmptyState from "../components/EmptyState";
import { usePageTitle } from "../hooks/usePageTitle";

const MAX_LOGO_BYTES = 2 * 1024 * 1024;
const LOGO_TYPES = ["image/jpeg", "image/png", "image/webp"];

type Tab = "general" | "members";

export default function OrganizationSettingsPage() {
	const { organizationId } = useParams<{ organizationId: string }>();
	const api = useApiClient();
	const { t, i18n } = useTranslation();
	const [activeTab, setActiveTab] = useState<Tab>("general");
	const [org, setOrg] = useState<OrganizationDetailsResponse | null>(null);
	const [loading, setLoading] = useState(true);
	const [saving, setSaving] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [successMessage, setSuccessMessage] = useState<string | null>(null);

	const [form, setForm] = useState({
		name: "",
		description: "",
		contactEmail: "",
		contactPhone: "",
		website: "",
		street: "",
		houseNumber: "",
		zipCode: "",
		city: "",
	});

	const [logoUrl, setLogoUrl] = useState<string | null>(null);
	const [uploadingLogo, setUploadingLogo] = useState(false);
	const [logoError, setLogoError] = useState<string | null>(null);
	const logoInputRef = useRef<HTMLInputElement>(null);

	const locale = i18n.language === "de" ? "de-DE" : "en-GB";

	usePageTitle(t("orgSettings.title"));

	useEffect(() => {
		if (!organizationId) return;
		setLoading(true);
		api
			.getOrganizationDetails(organizationId)
			.then((data) => {
				setOrg(data);
				setLogoUrl(data.logoUrl ?? null);
				setForm({
					name: data.name,
					description: data.description ?? "",
					contactEmail: data.contactEmail ?? "",
					contactPhone: data.contactPhone ?? "",
					website: data.website ?? "",
					street: data.address?.street ?? "",
					houseNumber: data.address?.houseNumber ?? "",
					zipCode: data.address?.zipCode ?? "",
					city: data.address?.city ?? "",
				});
			})
			.catch(() => setError(t("orgSettings.loadError")))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	const hasAddress =
		form.street || form.houseNumber || form.zipCode || form.city;

	async function handleLogoChange(e: React.ChangeEvent<HTMLInputElement>) {
		const file = e.target.files?.[0];
		if (!file || !organizationId) return;
		if (!LOGO_TYPES.includes(file.type)) {
			setLogoError(t("orgSettings.logoHint"));
			return;
		}
		if (file.size > MAX_LOGO_BYTES) {
			setLogoError(t("orgSettings.logoHint"));
			return;
		}
		setUploadingLogo(true);
		setLogoError(null);
		try {
			await api.uploadOrganizationLogo(organizationId, {
				data: file,
				fileName: file.name,
			});
			setLogoUrl(URL.createObjectURL(file));
		} catch {
			setLogoError(t("orgSettings.logoUploadError"));
		} finally {
			setUploadingLogo(false);
			if (logoInputRef.current) logoInputRef.current.value = "";
		}
	}

	async function handleSave(e: React.FormEvent) {
		e.preventDefault();
		if (!organizationId) return;
		setSaving(true);
		setError(null);
		setSuccessMessage(null);
		try {
			await api.updateOrganization(organizationId, {
				name: form.name,
				description: form.description || undefined,
				contactEmail: form.contactEmail || undefined,
				contactPhone: form.contactPhone || undefined,
				website: form.website || undefined,
				address: hasAddress
					? {
							street: form.street,
							houseNumber: form.houseNumber,
							zipCode: form.zipCode,
							city: form.city,
						}
					: undefined,
			});
			setSuccessMessage(t("orgSettings.savedSuccess"));
			setOrg((prev) =>
				prev
					? {
							...prev,
							name: form.name,
							description: form.description || undefined,
							contactEmail: form.contactEmail || undefined,
							contactPhone: form.contactPhone || undefined,
							website: form.website || undefined,
							address: hasAddress
								? {
										street: form.street,
										houseNumber: form.houseNumber,
										zipCode: form.zipCode,
										city: form.city,
									}
								: undefined,
						}
					: prev,
			);
		} catch {
			setError(t("orgSettings.saveError"));
		} finally {
			setSaving(false);
		}
	}

	async function handleRemoveMember(userId: string) {
		if (!organizationId) return;
		try {
			await api.removeMember(organizationId, userId);
			setOrg((prev) =>
				prev
					? {
							...prev,
							members: prev.members.filter((m) => m.userId !== userId),
						}
					: prev,
			);
		} catch {
			setError(t("orgSettings.removeMemberError"));
		}
	}

	if (loading) {
		return (
			<div className="flex items-center justify-center py-16">
				<span className="text-gray-500">{t("orgSettings.loading")}</span>
			</div>
		);
	}

	if (!org) {
		return (
			<div className="py-8 text-center text-red-600">
				{t("orgSettings.notFound")}
			</div>
		);
	}

	return (
		<>
			<div className="mb-6">
				<Link
					to={organizationId ? `/organizations/${organizationId}` : "/"}
					className="mb-1 inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700"
				>
					<svg
						className="h-4 w-4"
						fill="none"
						viewBox="0 0 24 24"
						strokeWidth="1.5"
						stroke="currentColor"
						aria-hidden="true"
					>
						<path
							strokeLinecap="round"
							strokeLinejoin="round"
							d="M15.75 19.5 8.25 12l7.5-7.5"
						/>
					</svg>
					{org.name}
				</Link>
				<h1 className="text-2xl font-bold text-gray-900">
					{t("orgSettings.title")}
				</h1>
			</div>

			<div className="mx-auto max-w-2xl">
				<p className="mb-6 text-sm text-gray-500">
					{t("orgSettings.createdOn", {
						date: new Date(org.createdOn).toLocaleDateString(locale, {
							day: "2-digit",
							month: "long",
							year: "numeric",
						}),
					})}
				</p>

				<div className="mb-6 flex gap-4 border-b border-gray-200">
					{(["general", "members"] as Tab[]).map((tab) => (
						<button
							key={tab}
							onClick={() => setActiveTab(tab)}
							className={`pb-2 text-sm font-medium transition-colors ${
								activeTab === tab
									? "border-b-2 border-brand-700 text-brand-700"
									: "text-gray-500 hover:text-gray-700"
							}`}
						>
							{tab === "general"
								? t("orgSettings.tabGeneral")
								: t("orgSettings.tabMembers", { count: org.members.length })}
						</button>
					))}
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

				{activeTab === "general" && (
					<form onSubmit={handleSave} className="space-y-5">
						<div>
							<p className="mb-1 block text-sm font-medium text-gray-700">
								{t("orgSettings.fieldLogo")}
							</p>
							<div className="flex items-center gap-4">
								{logoUrl ? (
									<img
										src={logoUrl}
										alt=""
										className="h-16 w-16 rounded-lg object-contain ring-1 ring-gray-200"
									/>
								) : (
									<span className="flex h-16 w-16 items-center justify-center rounded-lg bg-brand-100 text-2xl font-semibold text-brand-700">
										{org.name.charAt(0).toUpperCase()}
									</span>
								)}
								<div>
									<label
										htmlFor="logo-upload"
										className={`cursor-pointer rounded-md border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 ${uploadingLogo ? "opacity-50 pointer-events-none" : ""}`}
									>
										{uploadingLogo
											? t("orgSettings.logoUploading")
											: t("orgSettings.logoUpload")}
									</label>
									<input
										ref={logoInputRef}
										id="logo-upload"
										type="file"
										accept="image/jpeg,image/png,image/webp"
										className="sr-only"
										onChange={handleLogoChange}
										disabled={uploadingLogo}
									/>
									<p className="mt-1 text-xs text-gray-500">
										{t("orgSettings.logoHint")}
									</p>
									{logoError && (
										<p className="mt-1 text-xs text-red-600">{logoError}</p>
									)}
								</div>
							</div>
						</div>

						<Field label={t("orgSettings.fieldName")} id="org-name">
							<input
								id="org-name"
								required
								value={form.name}
								onChange={(e) =>
									setForm((f) => ({ ...f, name: e.target.value }))
								}
								className={inputClass}
							/>
						</Field>

						<Field
							label={t("orgSettings.fieldDescription")}
							id="org-description"
						>
							<textarea
								id="org-description"
								rows={3}
								value={form.description}
								onChange={(e) =>
									setForm((f) => ({
										...f,
										description: e.target.value,
									}))
								}
								className={inputClass}
							/>
						</Field>

						<Field
							label={t("orgSettings.fieldContactEmail")}
							id="org-contact-email"
						>
							<input
								id="org-contact-email"
								type="email"
								value={form.contactEmail}
								onChange={(e) =>
									setForm((f) => ({
										...f,
										contactEmail: e.target.value,
									}))
								}
								className={inputClass}
							/>
						</Field>

						<Field label={t("orgSettings.fieldPhone")} id="org-phone">
							<input
								id="org-phone"
								type="tel"
								value={form.contactPhone}
								onChange={(e) =>
									setForm((f) => ({
										...f,
										contactPhone: e.target.value,
									}))
								}
								className={inputClass}
							/>
						</Field>

						<Field label={t("orgSettings.fieldWebsite")} id="org-website">
							<input
								id="org-website"
								type="url"
								value={form.website}
								onChange={(e) =>
									setForm((f) => ({
										...f,
										website: e.target.value,
									}))
								}
								placeholder="https://"
								className={inputClass}
							/>
						</Field>

						<fieldset className="rounded-md border border-gray-200 p-4">
							<legend className="px-1 text-sm font-medium text-gray-700">
								{t("orgSettings.fieldAddress")}
							</legend>
							<div className="mt-3 grid grid-cols-3 gap-3">
								<div className="col-span-2">
									<label htmlFor="org-street" className={labelClass}>
										{t("orgSettings.fieldStreet")}
									</label>
									<input
										id="org-street"
										value={form.street}
										onChange={(e) =>
											setForm((f) => ({
												...f,
												street: e.target.value,
											}))
										}
										className={inputClass}
									/>
								</div>
								<div>
									<label htmlFor="org-house-number" className={labelClass}>
										{t("orgSettings.fieldHouseNumber")}
									</label>
									<input
										id="org-house-number"
										value={form.houseNumber}
										onChange={(e) =>
											setForm((f) => ({
												...f,
												houseNumber: e.target.value,
											}))
										}
										className={inputClass}
									/>
								</div>
								<div>
									<label htmlFor="org-zip" className={labelClass}>
										{t("orgSettings.fieldZip")}
									</label>
									<input
										id="org-zip"
										maxLength={5}
										value={form.zipCode}
										onChange={(e) =>
											setForm((f) => ({
												...f,
												zipCode: e.target.value,
											}))
										}
										className={inputClass}
									/>
								</div>
								<div className="col-span-2">
									<label htmlFor="org-city" className={labelClass}>
										{t("orgSettings.fieldCity")}
									</label>
									<input
										id="org-city"
										value={form.city}
										onChange={(e) =>
											setForm((f) => ({
												...f,
												city: e.target.value,
											}))
										}
										className={inputClass}
									/>
								</div>
							</div>
						</fieldset>

						<div className="flex justify-end">
							<button
								type="submit"
								disabled={saving}
								className="rounded-md bg-brand-700 px-5 py-2 text-sm font-medium text-white hover:bg-brand-800 disabled:opacity-50"
							>
								{saving ? t("orgSettings.saving") : t("orgSettings.save")}
							</button>
						</div>
					</form>
				)}

				{activeTab === "members" && (
					<>
						{org.members.length === 0 ? (
							<EmptyState
								title={t("orgSettings.noMembers")}
								message={t("orgSettings.noMembersHint")}
							/>
						) : (
							<ul className="divide-y divide-gray-100">
								{org.members.map((member) => (
									<li
										key={member.userId}
										className="flex items-center justify-between py-3"
									>
										<div>
											<p className="text-sm font-medium text-gray-900">
												{member.firstName && member.lastName
													? `${member.firstName} ${member.lastName}`
													: member.username}
											</p>
											<p className="text-xs text-gray-500">{member.email}</p>
											{member.isOrganisator && (
												<span className="mt-0.5 inline-block rounded-full bg-brand-50 px-2 py-0.5 text-xs text-brand-700">
													{t("orgSettings.organisator")}
												</span>
											)}
										</div>
										<button
											onClick={() => handleRemoveMember(member.userId)}
											className="text-xs text-red-700 hover:text-red-800"
										>
											{t("orgSettings.removeMember")}
										</button>
									</li>
								))}
							</ul>
						)}
					</>
				)}
			</div>
		</>
	);
}

const inputClass =
	"mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-700 focus:outline-none";

const labelClass = "block text-xs text-gray-600";

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
