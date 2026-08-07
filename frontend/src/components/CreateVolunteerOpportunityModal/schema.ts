import { z } from "zod";
import type { TFunction } from "i18next";

export const TOTAL_STEPS = 4;

const OCCURRENCE_VALUES = ["OneTime", "Recurring"] as const;
const PARTICIPATION_TYPE_VALUES = [
	"ScheduledSlots",
	"IndividualContact",
] as const;
const CHECK_IN_METHOD_VALUES = ["None", "QRCode", "PINCode", "Manual"] as const;

/** Built inside the component (via useMemo) so validation messages are translated. */
export function buildOpportunityFormSchema(t: TFunction) {
	const required = t("createOpportunity.fieldRequired");
	const invalidPin = t("createOpportunity.checkInPinInvalid");
	const tooLong = (max: number) => t("createOpportunity.fieldTooLong", { max });

	return z
		.object({
			title: z.string().max(150, tooLong(150)),
			description: z.string().max(2000, tooLong(2000)),
			isRemote: z.boolean(),
			street: z.string().max(100, tooLong(100)),
			houseNumber: z.string().max(10, tooLong(10)),
			zipCode: z.string().max(5, tooLong(5)),
			city: z.string().max(100, tooLong(100)),
			occurrence: z.enum(OCCURRENCE_VALUES),
			participationType: z.enum(PARTICIPATION_TYPE_VALUES),
			checkInMethod: z.enum(CHECK_IN_METHOD_VALUES),
			checkInPin: z.string(),
			category: z.string().optional(),
			tags: z.array(z.string()),
			validUntil: z.string(),
		})
		.superRefine((data, ctx) => {
			if (!data.title.trim())
				ctx.addIssue({ code: "custom", path: ["title"], message: required });
			if (!data.description.trim())
				ctx.addIssue({
					code: "custom",
					path: ["description"],
					message: required,
				});
			if (!data.isRemote) {
				if (!data.street.trim())
					ctx.addIssue({
						code: "custom",
						path: ["street"],
						message: required,
					});
				if (!data.houseNumber.trim())
					ctx.addIssue({
						code: "custom",
						path: ["houseNumber"],
						message: required,
					});
				if (!data.zipCode.trim())
					ctx.addIssue({
						code: "custom",
						path: ["zipCode"],
						message: required,
					});
				if (!data.city.trim())
					ctx.addIssue({ code: "custom", path: ["city"], message: required });
			}
			if (
				data.checkInMethod === "PINCode" &&
				data.checkInPin &&
				!/^\d{4,6}$/.test(data.checkInPin)
			)
				ctx.addIssue({
					code: "custom",
					path: ["checkInPin"],
					message: invalidPin,
				});
		});
}

export type OpportunityFormValues = z.infer<
	ReturnType<typeof buildOpportunityFormSchema>
>;

/** Which fields belong to each wizard step, for per-step "Next" validation. */
export const STEP_FIELDS: Record<number, (keyof OpportunityFormValues)[]> = {
	1: ["title", "description"],
	2: ["street", "houseNumber", "zipCode", "city"],
	3: ["checkInPin"],
	4: [],
};

export function errorStepsFromFieldErrors(
	erroredFields: ReadonlySet<keyof OpportunityFormValues>,
): Set<number> {
	const steps = new Set<number>();
	for (const [step, fields] of Object.entries(STEP_FIELDS)) {
		if (fields.some((field) => erroredFields.has(field)))
			steps.add(Number(step));
	}
	return steps;
}
