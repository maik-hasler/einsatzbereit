import { z } from "zod";
import type { TFunction } from "i18next";

/**
 * Shared by CreateOrganizationModal and OrgSettingsPage - both edit the same
 * organization fields and must reject the same invalid states before ever
 * reaching the server (mirrors backend/src/Domain/Organizations/Organization.cs
 * and backend/src/Domain/Common/Address.cs).
 */
export function buildOrganizationFormSchema(t: TFunction) {
	const required = t("orgSettings.fieldRequired");
	const invalidZip = t("orgSettings.zipInvalid");

	return z
		.object({
			name: z.string().max(100),
			description: z.string().max(1000),
			contactEmail: z.string().max(254),
			contactPhone: z.string().max(30),
			website: z.string().max(500),
			street: z.string().max(200),
			houseNumber: z.string().max(20),
			zipCode: z.string().max(10),
			city: z.string().max(100),
		})
		.superRefine((data, ctx) => {
			if (!data.name.trim())
				ctx.addIssue({ code: "custom", path: ["name"], message: required });

			// The address is optional as a whole, but once any one part of it is
			// filled in the backend requires all of street/houseNumber/zipCode/city
			// together (Address.Create) - so partial input must be caught here too,
			// not just at the server round-trip.
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
