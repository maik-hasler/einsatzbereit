import { z } from "zod";
import type { TFunction } from "i18next";

export function buildOrganizationFormSchema(t: TFunction) {
	const required = t("orgSettings.fieldRequired");
	const invalidZip = t("orgSettings.zipInvalid");
	const invalidWebsite = t("orgSettings.websiteInvalid");
	const tooLong = (max: number) => t("orgSettings.fieldTooLong", { max });

	return z
		.object({
			name: z.string().max(100, tooLong(100)),
			description: z.string().max(1000, tooLong(1000)),
			contactEmail: z.string().max(254, tooLong(254)),
			contactPhone: z.string().max(30, tooLong(30)),
			website: z.string().max(500, tooLong(500)),
			street: z.string().max(200, tooLong(200)),
			houseNumber: z.string().max(20, tooLong(20)),
			zipCode: z.string().max(10, tooLong(10)),
			city: z.string().max(100, tooLong(100)),
		})
		.superRefine((data, ctx) => {
			if (!data.name.trim())
				ctx.addIssue({ code: "custom", path: ["name"], message: required });

			const website = data.website.trim();
			if (website) {
				let isValidWebsite: boolean;
				try {
					isValidWebsite = ["http:", "https:"].includes(
						new URL(website).protocol,
					);
				} catch {
					isValidWebsite = false;
				}
				if (!isValidWebsite)
					ctx.addIssue({
						code: "custom",
						path: ["website"],
						message: invalidWebsite,
					});
			}

			const hasAddress =
				data.street.trim() ||
				data.houseNumber.trim() ||
				data.zipCode.trim() ||
				data.city.trim();

			if (hasAddress) {
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
				else if (data.zipCode.trim().length !== 5)
					ctx.addIssue({
						code: "custom",
						path: ["zipCode"],
						message: invalidZip,
					});
				if (!data.city.trim())
					ctx.addIssue({ code: "custom", path: ["city"], message: required });
			}
		});
}

export type OrganizationFormValues = z.infer<
	ReturnType<typeof buildOrganizationFormSchema>
>;

export const ORGANIZATION_FORM_DEFAULT_VALUES: OrganizationFormValues = {
	name: "",
	description: "",
	contactEmail: "",
	contactPhone: "",
	website: "",
	street: "",
	houseNumber: "",
	zipCode: "",
	city: "",
};
