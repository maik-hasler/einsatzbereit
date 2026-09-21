import { describe, it, expect } from "vitest";
import { reportIntentSigninArgs } from "./reportIntent";

const TARGET_ID = "33333333-3333-3333-3333-333333333333";

describe("reportIntentSigninArgs", () => {
	it("carries the target id back to the page the click happened on", () => {
		expect(
			reportIntentSigninArgs(`/organizations/${TARGET_ID}`, "", TARGET_ID),
		).toMatchObject({
			state: {
				returnTo: `/organizations/${TARGET_ID}?report=${TARGET_ID}`,
			},
		});
	});

	it("keeps the query string the visitor was already on", () => {
		expect(
			reportIntentSigninArgs("/organizations", "?q=leipzig", TARGET_ID),
		).toMatchObject({
			state: { returnTo: `/organizations?q=leipzig&report=${TARGET_ID}` },
		});
	});

	it("passes the UI locale through, like every other sign-in redirect", () => {
		expect(
			reportIntentSigninArgs("/organizations", "", TARGET_ID),
		).toHaveProperty("ui_locales");
	});
});
