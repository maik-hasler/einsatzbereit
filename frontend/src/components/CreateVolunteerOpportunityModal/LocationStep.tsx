import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { UseFormRegister, UseFormWatch } from "react-hook-form";
import type { AddressDto } from "../../client/api-client";
import { FloatingField } from "./shared";
import type { OpportunityFormValues } from "./schema";

interface Props {
	register: UseFormRegister<OpportunityFormValues>;
	watch: UseFormWatch<OpportunityFormValues>;
	onRemoteToggle: (checked: boolean) => void;
	errors: {
		street?: string;
		houseNumber?: string;
		zipCode?: string;
		city?: string;
	};
	organizationId: string;
	orgAddress: AddressDto | null;
	isEditMode: boolean;
	onApplyOrgAddress: () => void;
}

export default function LocationStep({
	register,
	watch,
	onRemoteToggle,
	errors,
	organizationId,
	orgAddress,
	isEditMode,
	onApplyOrgAddress,
}: Props) {
	const { t } = useTranslation();
	const isRemote = watch("isRemote");

	return (
		<div className="space-y-4" data-testid="wizard-step-2">
			<label
				htmlFor="opportunity-remote"
				className="flex cursor-pointer items-start gap-3 rounded-xl border-2 border-gray-200 bg-white px-4 py-3 transition hover:border-brand-200 hover:bg-brand-50 has-[:checked]:border-brand-500 has-[:checked]:bg-brand-50"
			>
				<input
					type="checkbox"
					id="opportunity-remote"
					className="mt-0.5 h-4 w-4 accent-brand-600"
					{...register("isRemote", {
						onChange: (e) => onRemoteToggle(e.target.checked),
					})}
				/>
				<span className="text-sm font-medium text-gray-800">
					{t("createOpportunity.fieldRemote")}
					<span className="mt-0.5 block text-xs font-normal text-gray-500">
						{t("createOpportunity.fieldRemoteHint")}
					</span>
				</span>
			</label>

			{!isRemote && (
				<>
					<div className="rounded-xl border border-brand-100 bg-brand-50 px-4 py-3">
						<div className="flex items-start justify-between gap-3">
							<p className="text-sm leading-relaxed text-brand-800">
								{t("createOpportunity.locationHint")}
							</p>
							{orgAddress && !isEditMode && (
								<button
									type="button"
									onClick={onApplyOrgAddress}
									className="shrink-0 rounded-lg border border-brand-200 bg-white px-3 py-1.5 text-xs font-semibold text-brand-700 transition hover:bg-brand-100"
								>
									{t("createOpportunity.useOrgAddress")}
								</button>
							)}
						</div>
						{!orgAddress && !isEditMode && (
							<p className="mt-2 text-xs leading-relaxed text-brand-700">
								{t("createOpportunity.orgAddressTip")}{" "}
								<Link
									to={`/organizations/${organizationId}/settings`}
									className="font-semibold underline hover:text-brand-900"
								>
									{t("createOpportunity.orgSettingsLink")}
								</Link>
							</p>
						)}
					</div>
					<div className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_5rem]">
						<FloatingField
							id="opportunity-street"
							label={t("createOpportunity.fieldStreet")}
							registration={register("street")}
							required
							error={errors.street}
							maxLength={100}
						/>
						<FloatingField
							id="opportunity-house"
							label={t("createOpportunity.fieldNumber")}
							registration={register("houseNumber")}
							required
							error={errors.houseNumber}
							maxLength={10}
						/>
					</div>
					<div className="grid grid-cols-1 gap-3 sm:grid-cols-[5rem_1fr]">
						<FloatingField
							id="opportunity-zip"
							label={t("createOpportunity.fieldZip")}
							registration={register("zipCode")}
							required
							error={errors.zipCode}
							maxLength={5}
							inputMode="numeric"
							pattern="\d{5}"
						/>
						<FloatingField
							id="opportunity-city"
							label={t("createOpportunity.fieldCity")}
							registration={register("city")}
							required
							error={errors.city}
							maxLength={100}
						/>
					</div>
				</>
			)}
		</div>
	);
}
