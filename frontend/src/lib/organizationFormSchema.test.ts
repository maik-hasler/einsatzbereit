import { describe, it, expect } from "vitest";
import type { TFunction } from "i18next";
import {
	buildOrganizationFormSchema,
	ORGANIZATION_FORM_DEFAULT_VALUES,
	type OrganizationFormValues,
} from "./organizationFormSchema";

const fakeT = ((key: string) => key) as TFunction;

function issuePaths(result: {
	success: boolean;
	error?: { issues: { path: PropertyKey[] }[] };
}): string[] {
	return (result.error?.issues ?? []).map((issue) => String(issue.path[0]));
}

function values(
	overrides: Partial<OrganizationFormValues>,
): OrganizationFormValues {
	return { ...ORGANIZATION_FORM_DEFAULT_VALUES, ...overrides };
}

describe("buildOrganizationFormSchema", () => {
	const schema = buildOrganizationFormSchema(fakeT);

	it("rejects the untouched default values because name is required", () => {
		const result = schema.safeParse(ORGANIZATION_FORM_DEFAULT_VALUES);
		expect(result.success).toBe(false);
		expect(issuePaths(result)).toEqual(["name"]);
	});

	it("accepts a name with no address at all", () => {
		const result = schema.safeParse(values({ name: "Rotes Kreuz" }));
		expect(result.success).toBe(true);
	});

	it("rejects a name that is only whitespace", () => {
		const result = schema.safeParse(values({ name: "   " }));
		expect(result.success).toBe(false);
		expect(issuePaths(result)).toContain("name");
	});

	it("requires every address field once any one of them is filled in", () => {
		const result = schema.safeParse(
			values({ name: "Org", street: "Hauptstrasse" }),
		);
		expect(result.success).toBe(false);
		expect(issuePaths(result).sort()).toEqual(
			["city", "houseNumber", "zipCode"].sort(),
		);
	});

	it("accepts a fully specified address", () => {
		const result = schema.safeParse(
			values({
				name: "Org",
				street: "Hauptstrasse",
				houseNumber: "12",
				zipCode: "12345",
				city: "Berlin",
			}),
		);
		expect(result.success).toBe(true);
	});

	it("rejects a zip code that is not exactly 5 digits long", () => {
		const result = schema.safeParse(
			values({
				name: "Org",
				street: "Hauptstrasse",
				houseNumber: "12",
				zipCode: "123",
				city: "Berlin",
			}),
		);
		expect(result.success).toBe(false);
		const zipIssue = result.error?.issues.find(
			(issue) => issue.path[0] === "zipCode",
		);
		expect(zipIssue?.message).toBe("orgSettings.zipInvalid");
	});

	it("reports zip code as missing (not invalid) when left blank alongside the rest of the address", () => {
		const result = schema.safeParse(
			values({
				name: "Org",
				street: "Hauptstrasse",
				houseNumber: "12",
				zipCode: "",
				city: "Berlin",
			}),
		);
		expect(result.success).toBe(false);
		const zipIssue = result.error?.issues.find(
			(issue) => issue.path[0] === "zipCode",
		);
		expect(zipIssue?.message).toBe("orgSettings.fieldRequired");
	});

	it("rejects a name longer than 100 characters with a translated message", () => {
		const result = schema.safeParse(values({ name: "a".repeat(101) }));
		expect(result.success).toBe(false);
		expect(issuePaths(result)).toContain("name");
		// Regression guard for #1731: zod's built-in message must not leak
		// through untranslated for any bare .max() field.
		const nameIssue = result.error?.issues.find(
			(issue) => issue.path[0] === "name",
		);
		expect(nameIssue?.message).toBe("orgSettings.fieldTooLong");
	});

	it("trims whitespace-only address fields the same as empty ones", () => {
		const result = schema.safeParse(
			values({
				name: "Org",
				street: "   ",
				houseNumber: "",
				zipCode: "",
				city: "",
			}),
		);
		expect(result.success).toBe(true);
	});

	it("accepts an empty website", () => {
		const result = schema.safeParse(values({ name: "Org", website: "" }));
		expect(result.success).toBe(true);
	});

	it("accepts an absolute https website", () => {
		const result = schema.safeParse(
			values({ name: "Org", website: "https://example.org" }),
		);
		expect(result.success).toBe(true);
	});

	it.each(["not-a-url", "javascript:alert(1)", "ftp://example.org"])(
		"rejects a website that is not an absolute http(s) URL: %s",
		(website) => {
			const result = schema.safeParse(values({ name: "Org", website }));
			expect(result.success).toBe(false);
			const websiteIssue = result.error?.issues.find(
				(issue) => issue.path[0] === "website",
			);
			expect(websiteIssue?.message).toBe("orgSettings.websiteInvalid");
		},
	);
});

describe("ORGANIZATION_FORM_DEFAULT_VALUES", () => {
	it("defaults every field to an empty string", () => {
		expect(
			Object.values(ORGANIZATION_FORM_DEFAULT_VALUES).every((v) => v === ""),
		).toBe(true);
	});
});
