import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { MyProfileResponse } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { usePageToolbar } from "../contexts/ToolbarContext";

type ContactPref = "Email" | "Phone" | "";

export default function ProfilePage() {
	const api = useApiClient();
	const { t } = useTranslation();

	usePageToolbar([
		{ label: t("breadcrumb.home"), href: "/" },
		{ label: t("breadcrumb.profile") },
	]);

	const [profile, setProfile] = useState<MyProfileResponse | null>(null);
	const [loading, setLoading] = useState(true);
	const [saving, setSaving] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [successMessage, setSuccessMessage] = useState<string | null>(null);

	const [bio, setBio] = useState("");
	const [skills, setSkills] = useState<string[]>([]);
	const [languages, setLanguages] = useState<string[]>([]);
	const [preferredContact, setPreferredContact] = useState<ContactPref>("");

	const [skillInput, setSkillInput] = useState("");
	const [langInput, setLangInput] = useState("");

	const skillInputRef = useRef<HTMLInputElement>(null);
	const langInputRef = useRef<HTMLInputElement>(null);

	useEffect(() => {
		setLoading(true);
		api
			.getUserProfile()
			.then((data) => {
				setProfile(data);
				setBio(data.bio ?? "");
				setSkills(data.skills ?? []);
				setLanguages(data.languages ?? []);
				const pref = data.preferredContact;
				setPreferredContact(pref === "Email" || pref === "Phone" ? pref : "");
			})
			.catch(() => setError(t("profile.loadError")))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

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

	if (loading) {
		return (
			<div className="flex items-center justify-center py-16">
				<span className="text-gray-500">{t("profile.loading")}</span>
			</div>
		);
	}

	return (
		<div className="mx-auto max-w-2xl">
			<div className="mb-6">
				<h1 className="text-2xl font-bold text-gray-900">
					{t("profile.title")}
				</h1>
				{profile && (
					<p className="text-sm text-gray-500">@{profile.username}</p>
				)}
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
						onChange={(e) => setPreferredContact(e.target.value as ContactPref)}
						className={inputClass}
					>
						<option value="">{t("profile.preferredContactNone")}</option>
						<option value="Email">{t("profile.preferredContactEmail")}</option>
						<option value="Phone">{t("profile.preferredContactPhone")}</option>
					</select>
				</Field>

				<div className="flex justify-end">
					<button
						type="submit"
						disabled={saving}
						className="rounded-md bg-gray-900 px-5 py-2 text-sm font-medium text-white hover:bg-gray-700 disabled:opacity-50"
					>
						{saving ? t("profile.saving") : t("profile.save")}
					</button>
				</div>
			</form>
		</div>
	);
}

const inputClass =
	"mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-900 focus:outline-none";

const textareaClass =
	"mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-900 focus:outline-none resize-y";

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
