import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import LanguageSelector from "./LanguageSelector";
import { renderWithProviders } from "../../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const renderSelector = (lng: "de" | "en" = "en") =>
	renderWithProviders(<LanguageSelector />, { lng });

describe("LanguageSelector disclosure semantics", () => {
	it("announces itself as a disclosure, never as a listbox or menu", async () => {
		renderSelector();

		const trigger = screen.getByTestId("language-selector-trigger");
		expect(trigger).toHaveAttribute("aria-expanded", "false");
		expect(trigger).not.toHaveAttribute("aria-haspopup");

		await userEvent.click(trigger);

		expect(trigger).toHaveAttribute("aria-expanded", "true");

		const menu = screen.getByTestId("language-selector-menu");
		expect(menu.tagName).toBe("UL");
		expect(menu).toHaveAccessibleName("Switch language");
		expect(menu).not.toHaveAttribute("role");
		expect(menu.querySelectorAll('[role="option"]')).toHaveLength(0);
		expect(menu.querySelectorAll("[aria-selected]")).toHaveLength(0);
	});

	it("marks the active language with aria-current, not aria-selected", async () => {
		renderSelector("de");

		await userEvent.click(screen.getByTestId("language-selector-trigger"));

		const menu = screen.getByTestId("language-selector-menu");
		const current = within(menu).getByRole("button", { name: "Deutsch" });
		expect(current).toHaveAttribute("aria-current", "true");
		expect(
			within(menu).getByRole("button", { name: "English" }),
		).not.toHaveAttribute("aria-current");
	});

	it("leads its accessible name with the code it visibly shows", () => {
		renderSelector("de");

		const trigger = screen.getByTestId("language-selector-trigger");
		expect(trigger).toHaveTextContent("DE");
		const name = trigger.getAttribute("aria-label") ?? "";
		expect(name.startsWith("DE")).toBe(true);
		expect(name).toContain("Deutsch");
	});
});

describe("LanguageSelector email-language divergence (#2328)", () => {
	beforeEach(() => {
		api.getUserProfile.mockReset();
	});

	async function switchToEnglish() {
		await userEvent.click(screen.getByTestId("language-selector-trigger"));
		const menu = screen.getByTestId("language-selector-menu");
		await userEvent.click(
			within(menu).getByRole("button", { name: "English" }),
		);
	}

	it("says emails keep arriving in the other language after a switch", async () => {
		api.getUserProfile.mockResolvedValue({ preferredLanguage: "de" });
		renderWithProviders(<LanguageSelector />, {
			lng: "de",
			auth: { isAuthenticated: true },
		});

		await switchToEnglish();

		const toast = await screen.findByRole("alert");
		expect(toast).toHaveTextContent(
			"Your notification emails still arrive in German.",
		);
	});

	it("stays quiet when the two already agree", async () => {
		api.getUserProfile.mockResolvedValue({ preferredLanguage: "en" });
		renderWithProviders(<LanguageSelector />, {
			lng: "de",
			auth: { isAuthenticated: true },
		});

		await switchToEnglish();

		await waitFor(() => expect(api.getUserProfile).toHaveBeenCalled());
		expect(screen.queryByRole("alert")).not.toBeInTheDocument();
	});

	it("does not ask the API about a signed-out visitor", async () => {
		renderSelector("de");

		await switchToEnglish();

		expect(api.getUserProfile).not.toHaveBeenCalled();
		expect(screen.queryByRole("alert")).not.toBeInTheDocument();
	});

	// The lookup is a courtesy, not part of switching the language - a failing
	// one must not surface as an error the user did not cause.
	it("switches the language anyway when the profile lookup fails", async () => {
		api.getUserProfile.mockRejectedValue(new Error("offline"));
		renderWithProviders(<LanguageSelector />, {
			lng: "de",
			auth: { isAuthenticated: true },
		});

		await switchToEnglish();

		await waitFor(() =>
			expect(screen.getByTestId("language-selector-trigger")).toHaveTextContent(
				"EN",
			),
		);
		expect(screen.queryByRole("alert")).not.toBeInTheDocument();
	});
});
