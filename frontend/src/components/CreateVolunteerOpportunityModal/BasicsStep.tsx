import { useEffect, useState } from "react";
import type { ChangeEvent } from "react";
import { useTranslation } from "react-i18next";
import type { UseFormRegister, UseFormWatch } from "react-hook-form";
import { FloatingField } from "./shared";
import type { OpportunityFormValues } from "./schema";
import { PhotoIcon } from "../icons";
import { IMAGE_UPLOAD_ACCEPT, getImageUploadHint } from "../../lib/imageUpload";

interface Props {
	register: UseFormRegister<OpportunityFormValues>;
	watch: UseFormWatch<OpportunityFormValues>;
	titleDeError?: string;
	titleEnError?: string;
	descriptionDeError?: string;
	descriptionEnError?: string;
	/** Bumped on every "Next"/stepper-jump/submit validation attempt for this
	 * step, even when it produces the same error message as last time - see
	 * the tab-switch effect below for why the message strings alone aren't
	 * enough (einsatzbereit#2077). */
	revalidationAttempt: number;
	bannerPreview: string | null;
	bannerError: string | null;
	onBannerChange: (e: ChangeEvent<HTMLInputElement>) => void;
	onBannerRemove: () => void;
}

const CONTENT_LANGUAGES = ["de", "en"] as const;
type ContentLanguage = (typeof CONTENT_LANGUAGES)[number];

export default function BasicsStep({
	register,
	watch,
	titleDeError,
	titleEnError,
	descriptionDeError,
	descriptionEnError,
	revalidationAttempt,
	bannerPreview,
	bannerError,
	onBannerChange,
	onBannerRemove,
}: Props) {
	const { t, i18n } = useTranslation();
	const [activeLanguage, setActiveLanguage] = useState<ContentLanguage>("de");
	const titleDe = watch("titleDe");
	const titleEn = watch("titleEn");
	const descriptionDe = watch("descriptionDe");
	const descriptionEn = watch("descriptionEn");
	const hasError = {
		de: Boolean(titleDeError || descriptionDeError),
		en: Boolean(titleEnError || descriptionEnError),
	};

	// German is required, so an error there always blocks moving on. Without
	// this, an error raised while the English tab is active (e.g. clicking
	// Next with the German title still blank) left the German fields - and
	// their inline role="alert" text - hidden behind display:none, with
	// nothing else surfacing the failure: the button just appeared to do
	// nothing, for sighted and screen-reader users alike. Switching the tab
	// back un-hides the existing FloatingField error text, the same way it
	// already surfaces for every other step in this wizard - no separate
	// live-region banner needed on top of that.
	//
	// `revalidationAttempt` is in the dependency list alongside the error
	// messages themselves, not instead of them: a user can switch to the
	// English tab *while* a German error is still outstanding (nothing stops
	// that), and a second failed attempt then produces the exact same message
	// string as the first - message-only deps would see no change and skip
	// the switch, leaving the newly-focused German field hidden behind
	// display:none (einsatzbereit#2077).
	useEffect(() => {
		if (titleDeError || descriptionDeError) setActiveLanguage("de");
	}, [titleDeError, descriptionDeError, revalidationAttempt]);

	return (
		<div className="space-y-4" data-testid="wizard-step-1">
			{/* Plain toggle buttons, not an ARIA tablist - this repo's convention
			(see LanguageSelector.tsx, Stepper in ./shared.tsx) is to only claim a
			widget role when the matching keyboard model (arrow keys) is actually
			implemented; a labelled pair of buttons with aria-current for the
			active one needs none of that. Both languages' values stay in the form
			regardless of which is showing - switching tabs never loses data,
			since only German is required to publish (einsatzbereit#1946). */}
			<div
				role="group"
				aria-label={t("createOpportunity.contentLanguageGroup")}
				className="inline-flex rounded-lg border border-gray-200 p-0.5"
			>
				{CONTENT_LANGUAGES.map((lang) => (
					<button
						key={lang}
						type="button"
						aria-current={activeLanguage === lang ? "true" : undefined}
						data-testid={`opportunity-content-language-${lang}`}
						// Sits before the title field in the DOM but must not steal the
						// modal's initial focus from it (see Modal.tsx) - a keyboard user
						// opening this dialog wants to start typing the title, not land
						// on a language toggle first.
						data-skip-initial-focus
						onClick={() => setActiveLanguage(lang)}
						className={`flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
							activeLanguage === lang
								? "bg-brand-50 text-brand-700"
								: "text-gray-600 hover:bg-gray-50"
						}`}
					>
						{t(`language.${lang}`)}
						{hasError[lang] && (
							<span
								aria-hidden="true"
								className="h-1.5 w-1.5 rounded-full bg-red-500"
							/>
						)}
					</button>
				))}
			</div>

			<div className={activeLanguage === "de" ? "space-y-4" : "hidden"}>
				{/* Keeps the original "opportunity-title"/"opportunity-description"
				ids (no "-de" suffix) - this is the direct successor of the single
				title/description field that existed before #1946, and a wide range
				of VisualTests locators already target these exact ids. */}
				<FloatingField
					id="opportunity-title"
					label={t("createOpportunity.fieldTitle")}
					registration={register("titleDe")}
					required
					error={titleDeError}
					maxLength={150}
					showCount
					displayValue={titleDe}
				/>
				<FloatingField
					id="opportunity-description"
					label={t("createOpportunity.fieldDescription")}
					registration={register("descriptionDe")}
					required
					error={descriptionDeError}
					maxLength={2000}
					multiline
					showCount
					displayValue={descriptionDe}
				/>
			</div>
			<div className={activeLanguage === "en" ? "space-y-4" : "hidden"}>
				<p className="text-xs text-gray-500">
					{t("createOpportunity.contentLanguageEnglishHint")}
				</p>
				<FloatingField
					id="opportunity-title-en"
					label={t("createOpportunity.fieldTitle")}
					registration={register("titleEn")}
					error={titleEnError}
					maxLength={150}
					showCount
					displayValue={titleEn}
				/>
				<FloatingField
					id="opportunity-description-en"
					label={t("createOpportunity.fieldDescription")}
					registration={register("descriptionEn")}
					error={descriptionEnError}
					maxLength={2000}
					multiline
					showCount
					displayValue={descriptionEn}
				/>
			</div>

			<div>
				<p className="mb-1.5 text-sm font-semibold text-gray-800">
					{t("createOpportunity.fieldBanner")}
				</p>
				{bannerPreview ? (
					<div className="relative overflow-hidden rounded-xl">
						<img
							src={bannerPreview}
							alt={t("createOpportunity.fieldBanner")}
							width={1200}
							height={480}
							loading="lazy"
							className="h-36 w-full object-cover"
						/>
						<button
							type="button"
							onClick={onBannerRemove}
							className="absolute top-2 right-2 rounded-lg bg-black/60 px-2.5 py-1 text-xs font-semibold text-white backdrop-blur transition hover:bg-black/80"
						>
							{t("createOpportunity.bannerRemove")}
						</button>
					</div>
				) : (
					<label
						htmlFor="opportunity-banner"
						className="flex cursor-pointer flex-col items-center justify-center gap-1 rounded-xl border-2 border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center transition hover:border-brand-300 hover:bg-brand-50"
					>
						<PhotoIcon className="h-6 w-6 text-gray-400" />
						<span className="text-sm font-medium text-gray-700">
							{t("createOpportunity.bannerUpload")}
						</span>
						<span className="text-xs text-gray-500">
							{getImageUploadHint(t, i18n.language)}
						</span>
						<input
							id="opportunity-banner"
							type="file"
							accept={IMAGE_UPLOAD_ACCEPT}
							className="sr-only"
							onChange={onBannerChange}
							aria-invalid={bannerError ? true : undefined}
							aria-describedby={
								bannerError ? "opportunity-banner-error" : undefined
							}
						/>
					</label>
				)}
				{bannerError && (
					<p
						id="opportunity-banner-error"
						className="mt-1 text-xs text-red-600"
						role="alert"
					>
						{bannerError}
					</p>
				)}
			</div>
		</div>
	);
}
