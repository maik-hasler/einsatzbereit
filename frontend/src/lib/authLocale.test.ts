import { describe, it, expect, vi } from "vitest";

const i18nMock = vi.hoisted(() => ({ language: "en" }));

vi.mock("../i18n", () => ({
	default: i18nMock,
}));

import { signinLocaleArgs } from "./authLocale";

describe("signinLocaleArgs", () => {
	it("reflects the current i18next language", () => {
		i18nMock.language = "en";
		expect(signinLocaleArgs()).toEqual({ ui_locales: "en" });
	});

	it("picks up a language change", () => {
		i18nMock.language = "de";
		expect(signinLocaleArgs()).toEqual({ ui_locales: "de" });
	});

	it("omits state when no return path is given", () => {
		expect(signinLocaleArgs()).not.toHaveProperty("state.returnTo");
	});

	it("carries the given return path in state.returnTo", () => {
		i18nMock.language = "en";
		expect(signinLocaleArgs("/opportunities/123")).toEqual({
			ui_locales: "en",
			state: { returnTo: "/opportunities/123" },
		});
	});
});
