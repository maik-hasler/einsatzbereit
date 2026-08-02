import type { ChangeEvent } from "react";
import { useTranslation } from "react-i18next";
import type { UseFormRegister, UseFormWatch } from "react-hook-form";
import { FloatingField } from "./shared";
import type { OpportunityFormValues } from "./schema";

interface Props {
	register: UseFormRegister<OpportunityFormValues>;
	watch: UseFormWatch<OpportunityFormValues>;
	titleError?: string;
	descriptionError?: string;
	bannerPreview: string | null;
	bannerError: string | null;
	onBannerChange: (e: ChangeEvent<HTMLInputElement>) => void;
	onBannerRemove: () => void;
}

export default function BasicsStep({
	register,
	watch,
	titleError,
	descriptionError,
	bannerPreview,
	bannerError,
	onBannerChange,
	onBannerRemove,
}: Props) {
	const { t } = useTranslation();
	const title = watch("title");
	const description = watch("description");

	return (
		<div className="space-y-4" data-testid="wizard-step-1">
			<FloatingField
				id="opportunity-title"
				label={t("createOpportunity.fieldTitle")}
				registration={register("title")}
				required
				error={titleError}
				maxLength={150}
				showCount
				displayValue={title}
			/>
			<FloatingField
				id="opportunity-description"
				label={t("createOpportunity.fieldDescription")}
				registration={register("description")}
				required
				error={descriptionError}
				maxLength={2000}
				multiline
				showCount
				displayValue={description}
			/>

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
							className="absolute right-2 top-2 rounded-lg bg-black/60 px-2.5 py-1 text-xs font-semibold text-white backdrop-blur transition hover:bg-black/80"
						>
							{t("createOpportunity.bannerRemove")}
						</button>
					</div>
				) : (
					<label
						htmlFor="opportunity-banner"
						className="flex cursor-pointer flex-col items-center justify-center gap-1 rounded-xl border-2 border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center transition hover:border-brand-300 hover:bg-brand-50"
					>
						<svg
							aria-hidden="true"
							className="h-6 w-6 text-gray-400"
							fill="none"
							stroke="currentColor"
							strokeWidth={1.5}
							viewBox="0 0 24 24"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="m2.25 15.75 5.159-5.159a2.25 2.25 0 0 1 3.182 0l5.159 5.159m-1.5-1.5 1.409-1.409a2.25 2.25 0 0 1 3.182 0l2.909 2.909m-18 3.75h16.5a1.5 1.5 0 0 0 1.5-1.5V6a1.5 1.5 0 0 0-1.5-1.5H3.75A1.5 1.5 0 0 0 2.25 6v12a1.5 1.5 0 0 0 1.5 1.5Zm10.5-11.25h.008v.008h-.008V8.25Zm.375 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Z"
							/>
						</svg>
						<span className="text-sm font-medium text-gray-700">
							{t("createOpportunity.bannerUpload")}
						</span>
						<span className="text-xs text-gray-500">
							{t("createOpportunity.bannerHint")}
						</span>
						<input
							id="opportunity-banner"
							type="file"
							accept="image/jpeg,image/png,image/webp"
							className="sr-only"
							onChange={onBannerChange}
						/>
					</label>
				)}
				{bannerError && (
					<p className="mt-1 text-xs text-red-600" role="alert">
						{bannerError}
					</p>
				)}
			</div>
		</div>
	);
}
