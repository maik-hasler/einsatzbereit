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
});
