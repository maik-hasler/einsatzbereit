import { describe, expect, it } from "vitest";
import {
	buildSignUpLink,
	SIGN_UP_INTEREST,
	SIGN_UP_PARAM,
} from "./signUpDeepLink";

const OPPORTUNITY_ID = "22222222-2222-2222-2222-222222222222";
const TIME_SLOT_ID = "33333333-3333-3333-3333-333333333333";

describe("buildSignUpLink", () => {
	it("carries the slot the withdrawn sign-up was for", () => {
		expect(buildSignUpLink(OPPORTUNITY_ID, TIME_SLOT_ID)).toBe(
			`/volunteer-opportunities/${OPPORTUNITY_ID}?${SIGN_UP_PARAM}=${TIME_SLOT_ID}`,
		);
	});

	it("falls back to the interest marker when there was no slot", () => {
		expect(buildSignUpLink(OPPORTUNITY_ID)).toBe(
			`/volunteer-opportunities/${OPPORTUNITY_ID}?${SIGN_UP_PARAM}=${SIGN_UP_INTEREST}`,
		);
		expect(buildSignUpLink(OPPORTUNITY_ID, null)).toBe(
			buildSignUpLink(OPPORTUNITY_ID),
		);
	});
});
