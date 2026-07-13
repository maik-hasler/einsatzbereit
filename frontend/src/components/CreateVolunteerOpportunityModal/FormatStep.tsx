import { useTranslation } from "react-i18next";
import type { UseFormRegister, UseFormWatch } from "react-hook-form";
import { FloatingField } from "./shared";
import type { OpportunityFormValues } from "./schema";

function generateRandomPin(): string {
	return String(Math.floor(1000 + Math.random() * 9000));
}

function RadioCardGroup<T extends string>({
	name,
	options,
	current,
	register,
}: {
	name: "occurrence" | "participationType" | "checkInMethod";
	options: readonly [T, string][];
	current: string;
	register: UseFormRegister<OpportunityFormValues>;
}) {
	return (
		<div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
			{options.map(([value, label]) => (
				<label
					key={value}
					className={`flex cursor-pointer items-center gap-3 rounded-xl border-2 px-4 py-3 transition ${
						current === value
							? "border-brand-500 bg-brand-50 text-brand-800"
							: "border-gray-200 bg-white text-gray-700 hover:border-brand-200 hover:bg-gray-50"
					}`}
				>
					<input
						type="radio"
						value={value}
						className="sr-only"
						{...register(name)}
					/>
					<span className="text-sm font-medium">{label}</span>
				</label>
			))}
		</div>
	);
}

interface Props {
	register: UseFormRegister<OpportunityFormValues>;
	watch: UseFormWatch<OpportunityFormValues>;
	setCheckInPin: (pin: string) => void;
	checkInPinError?: string;
}

export default function FormatStep({
	register,
	watch,
	setCheckInPin,
	checkInPinError,
}: Props) {
	const { t } = useTranslation();
	const occurrence = watch("occurrence");
	const participationType = watch("participationType");
	const checkInMethod = watch("checkInMethod");
	const checkInPin = watch("checkInPin");

	return (
		<div className="space-y-6" data-testid="wizard-step-3">
			<div>
				<p className="mb-3 text-sm font-semibold text-gray-800">
					{t("createOpportunity.fieldFrequency")}
				</p>
				<RadioCardGroup
					name="occurrence"
					current={occurrence}
					register={register}
					options={[
						["OneTime", t("opportunities.oneTime")],
						["Recurring", t("opportunities.recurring")],
					]}
				/>
			</div>

			<div>
				<p className="mb-3 text-sm font-semibold text-gray-800">
					{t("createOpportunity.fieldParticipationType")}
				</p>
				<RadioCardGroup
					name="participationType"
					current={participationType}
					register={register}
					options={[
						["Waitlist", t("opportunities.waitlist")],
						["IndividualContact", t("opportunities.individualContact")],
					]}
				/>
			</div>

			<div>
				<p className="mb-3 text-sm font-semibold text-gray-800">
					{t("createOpportunity.fieldCheckInMethod")}
				</p>
				<RadioCardGroup
					name="checkInMethod"
					current={checkInMethod}
					register={register}
					options={[
						["None", t("checkInMethod.none")],
						["QRCode", t("checkInMethod.qrCode")],
						["PINCode", t("checkInMethod.pinCode")],
						["Manual", t("checkInMethod.manual")],
					]}
				/>
			</div>

			{checkInMethod === "PINCode" && (
				<div>
					<div className="flex items-start gap-2">
						<FloatingField
							id="create-check-in-pin"
							label={t("createOpportunity.fieldCheckInPin")}
							registration={register("checkInPin", {
								onChange: (e) => {
									const sanitized = e.target.value
										.replace(/\D/g, "")
										.slice(0, 6);
									if (sanitized !== e.target.value) setCheckInPin(sanitized);
								},
							})}
							error={checkInPinError}
							maxLength={6}
							inputMode="numeric"
							pattern="[0-9]*"
							displayValue={checkInPin}
							wrapperClassName="flex-1"
						/>
						<button
							type="button"
							onClick={() => setCheckInPin(generateRandomPin())}
							className="mt-0.5 shrink-0 rounded-xl border-2 border-gray-200 px-4 py-3.5 text-sm font-medium text-gray-700 transition hover:border-brand-200 hover:bg-gray-50"
						>
							{t("createOpportunity.generateRandomPin")}
						</button>
					</div>
					{!checkInPinError && (
						<p className="mt-1 text-xs text-gray-500">
							{t("createOpportunity.checkInPinHint")}
						</p>
					)}
				</div>
			)}
		</div>
	);
}
