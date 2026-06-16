import { useEffect, useRef, useState } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import type { MyProfileResponse } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import ConfirmDialog from "../components/ConfirmDialog";

type ContactPref = "Email" | "Phone" | "";

export default function ProfilePage() {
	const auth = useAuth();
	const api = useApiClient();
	const { t } = useTranslation();
	const navigate = useNavigate();
	usePageTitle(t("profile.title"));

	const [profile, setProfile] = useState<MyProfileResponse | null>(null);
	const [loading, setLoading] = useState(true);
	const [saving, setSaving] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [successMessage, setSuccessMessage] = useState<string | null>(null);

	const [firstName, setFirstName] = useState("");
	const [lastName, setLastName] = useState("");
	const [bio, setBio] = useState("");
	const [skills, setSkills] = useState<string[]>([]);
	const [languages, setLanguages] = useState<string[]>([]);
	const [preferredContact, setPreferredContact] = useState<ContactPref>("");

	const [skillInput, setSkillInput] = useState("");
	const [langInput, setLangInput] = useState("");

	const skillInputRef = useRef<HTMLInputElement>(null);
	const langInputRef = useRef<HTMLInputElement>(null);

	const [showDeleteDialog, setShowDeleteDialog] = useState(false);
	const [deleting, setDeleting] = useState(false);
	const [deleteError, setDeleteError] = useState<string | null>(null);

	const accessToken = auth.user?.access_token;

	useEffect(() => {
		let cancelled = false;
		const retryDelaysMs = [500, 1000, 2000];

		async function loadProfile() {
			setLoading(true);
			for (let attempt = 0; ; attempt++) {
				try {
					const data = await api.getUserProfile();
					if (cancelled) return;
					setProfile(data);
					setFirstName(data.firstName ?? "");
					setLastName(data.lastName ?? "");
					setBio(data.bio ?? "");
					setSkills(data.skills ?? []);
					setLanguages(data.languages ?? []);
					const pref = data.preferredContact;
					setPreferredContact(pref === "Email" || pref === "Phone" ? pref : "");
					setError(null);
					return;
				} catch {
					if (cancelled) return;
					if (attempt >= retryDelaysMs.length) {
						setError(t("profile.loadError"));
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
	}, [accessToken]);

	function addChip(
		value: string,
		list: string[],
		setList: (l: string[]) => void,
		setInput: (s: string) => void,
	) {
		const trimmed = value.trim();
		if (trimmed && !list.includes(trimmed)) {
			setList([...list, trimmed]);
		}
		setInput("");
	}

	function removeChip(
		item: string,
		list: string[],
		setList: (l: string[]) => void,
	) {
		setList(list.filter((s) => s !== item));
	}

	async function handleSave(e: React.FormEvent) {
		e.preventDefault();
		setSaving(true);
		setError(null);
		setSuccessMessage(null);
		try {
			await api.updateUserProfile({
				firstName: firstName || undefined,
				lastName: lastName || undefined,
				bio: bio || undefined,
				skills,
				languages,
				preferredContact: preferredContact || undefined,
			});
			setSuccessMessage(t("profile.savedSuccess"));
		} catch {
			setError(t("profile.saveError"));
		} finally {
			setSaving(false);
		}
	}

	async function handleDeleteAccount() {
		setDeleting(true);
		setDeleteError(null);
		try {
			await api.deleteMyAccount();
			await auth.removeUser();
			navigate("/");
		} catch {
			setDeleteError(t("account.deleteError"));
			setDeleting(false);
		}
	}

	if (loading) {
		return (
			<div className="flex items-center justify-center py-16">
				<span className="text-gray-500">{t("profile.loading")}</span>
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
						{t("profile.title")}
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

			<form onSubmit={handleSave} className="space-y-6">
				<section>
					<h2 className="mb-4 text-base font-semibold text-gray-900">
						{t("account.title")}
					</h2>
					<div className="space-y-5">
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
							<p className="mt-1 text-xs text-gray-500">
								{t("account.emailHint")}
							</p>
						</Field>

						<Field label={t("account.fieldFirstName")} id="first-name">
							<input
								id="first-name"
								value={firstName}
								onChange={(e) => setFirstName(e.target.value)}
								className={inputClass}
							/>
						</Field>

						<Field label={t("account.fieldLastName")} id="last-name">
							<input
								id="last-name"
								value={lastName}
								onChange={(e) => setLastName(e.target.value)}
								className={inputClass}
							/>
						</Field>
					</div>
				</section>

				<hr className="border-gray-200" />

				<section>
					<h2 className="mb-4 text-base font-semibold text-gray-900">
						{t("profile.sectionDetails")}
					</h2>
					<div className="space-y-5">
						<Field label={t("profile.fieldBio")} id="bio">
							<textarea
								id="bio"
								rows={4}
								value={bio}
								placeholder={t("profile.bioPlaceholder")}
								onChange={(e) => setBio(e.target.value)}
								className={textareaClass}
							/>
						</Field>

						<Field label={t("profile.fieldSkills")} id="skill-input">
							<ChipInput
								inputRef={skillInputRef}
								inputId="skill-input"
								chips={skills}
								inputValue={skillInput}
								placeholder={t("profile.skillsPlaceholder")}
								onInputChange={setSkillInput}
								onAdd={(v) => addChip(v, skills, setSkills, setSkillInput)}
								onRemove={(v) => removeChip(v, skills, setSkills)}
								removeLabel={t("profile.removeChip")}
							/>
						</Field>

						<Field label={t("profile.fieldLanguages")} id="lang-input">
							<ChipInput
								inputRef={langInputRef}
								inputId="lang-input"
								chips={languages}
								inputValue={langInput}
								placeholder={t("profile.languagesPlaceholder")}
								onInputChange={setLangInput}
								onAdd={(v) => addChip(v, languages, setLanguages, setLangInput)}
								onRemove={(v) => removeChip(v, languages, setLanguages)}
								removeLabel={t("profile.removeChip")}
							/>
						</Field>

						<Field
							label={t("profile.fieldPreferredContact")}
							id="preferred-contact"
						>
							<select
								id="preferred-contact"
								value={preferredContact}
								onChange={(e) =>
									setPreferredContact(e.target.value as ContactPref)
								}
								className={inputClass}
							>
								<option value="">{t("profile.preferredContactNone")}</option>
								<option value="Email">
									{t("profile.preferredContactEmail")}
								</option>
								<option value="Phone">
									{t("profile.preferredContactPhone")}
								</option>
							</select>
						</Field>
					</div>
				</section>

				<div className="flex justify-end">
					<button
						type="submit"
						disabled={saving}
						className="rounded-md bg-brand-700 px-5 py-2 text-sm font-medium text-white hover:bg-brand-800 disabled:opacity-50"
					>
						{saving ? t("profile.saving") : t("profile.save")}
					</button>
				</div>
			</form>

			<div className="mt-12 rounded-lg border border-red-200 bg-red-50 p-6">
				<h2 className="mb-1 text-base font-semibold text-red-800">
					{t("account.dangerZoneTitle")}
				</h2>
				<p className="mb-4 text-sm text-red-700">
					{t("account.dangerZoneDescription")}
				</p>
				<button
					type="button"
					onClick={() => setShowDeleteDialog(true)}
					className="rounded-md border border-red-700 px-4 py-2 text-sm font-medium text-red-700 hover:bg-red-50"
				>
					{t("account.deleteAccountButton")}
				</button>
			</div>

			{showDeleteDialog && (
				<ConfirmDialog
					title={t("account.deleteConfirmTitle")}
					message={t("account.deleteConfirmMessage")}
					confirmLabel={t("account.deleteConfirmButton")}
					onConfirm={handleDeleteAccount}
					onClose={() => {
						setShowDeleteDialog(false);
						setDeleteError(null);
					}}
					loading={deleting}
					error={deleteError}
				/>
			)}
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
	"mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-700 focus:outline-none";

const textareaClass =
	"mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-700 focus:outline-none resize-y";

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

function ChipInput({
	inputRef,
	inputId,
	chips,
	inputValue,
	placeholder,
	onInputChange,
	onAdd,
	onRemove,
	removeLabel,
}: {
	inputRef: React.RefObject<HTMLInputElement | null>;
	inputId: string;
	chips: string[];
	inputValue: string;
	placeholder: string;
	onInputChange: (v: string) => void;
	onAdd: (v: string) => void;
	onRemove: (v: string) => void;
	removeLabel: string;
}) {
	function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
		if (e.key === "Enter") {
			e.preventDefault();
			onAdd(inputValue);
		}
	}

	return (
		<div className="mt-1">
			{chips.length > 0 && (
				<div className="mb-2 flex flex-wrap gap-2">
					{chips.map((chip) => (
						<span
							key={chip}
							className="inline-flex items-center gap-1 rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-700"
						>
							{chip}
							<button
								type="button"
								aria-label={`${removeLabel} ${chip}`}
								onClick={() => onRemove(chip)}
								className="ml-1 text-gray-400 hover:text-gray-700"
							>
								&times;
							</button>
						</span>
					))}
				</div>
			)}
			<input
				ref={inputRef}
				id={inputId}
				type="text"
				value={inputValue}
				placeholder={placeholder}
				onChange={(e) => onInputChange(e.target.value)}
				onKeyDown={handleKeyDown}
				onBlur={() => {
					if (inputValue.trim()) onAdd(inputValue);
				}}
				className={inputClass}
			/>
		</div>
	);
}
