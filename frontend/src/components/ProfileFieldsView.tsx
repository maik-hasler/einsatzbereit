import { useTranslation } from "react-i18next";

interface ProfileFieldsViewProps {
	bio?: string | null;
	skills: string[];
	languages: string[];
	preferredContact?: string | null;
}

export default function ProfileFieldsView({
	bio,
	skills,
	languages,
	preferredContact,
}: ProfileFieldsViewProps) {
	const { t } = useTranslation();

	return (
		<>
			{bio && (
				<div>
					<p className="mb-1 text-sm font-medium text-gray-700">
						{t("profile.fieldBio")}
					</p>
					<p className="whitespace-pre-wrap text-sm text-gray-600">{bio}</p>
				</div>
			)}

			{skills.length > 0 && (
				<div>
					<p className="mb-2 text-sm font-medium text-gray-700">
						{t("profile.fieldSkills")}
					</p>
					<div className="flex flex-wrap gap-2">
						{skills.map((s) => (
							<span
								key={s}
								className="rounded-full bg-brand-50 px-3 py-1 text-sm text-brand-700"
							>
								{s}
							</span>
						))}
					</div>
				</div>
			)}

			{languages.length > 0 && (
				<div>
					<p className="mb-2 text-sm font-medium text-gray-700">
						{t("profile.fieldLanguages")}
					</p>
					<div className="flex flex-wrap gap-2">
						{languages.map((l) => (
							<span
								key={l}
								className="rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-600"
							>
								{l}
							</span>
						))}
					</div>
				</div>
			)}

			{preferredContact && (
				<div>
					<p className="mb-1 text-sm font-medium text-gray-700">
						{t("profile.fieldPreferredContact")}
					</p>
					<p className="text-sm text-gray-600">
						{preferredContact === "Email"
							? t("profile.preferredContactEmail")
							: t("profile.preferredContactPhone")}
					</p>
				</div>
			)}
		</>
	);
}
