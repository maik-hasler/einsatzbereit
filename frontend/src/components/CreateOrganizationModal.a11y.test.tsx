import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CreateOrganizationModal from "./CreateOrganizationModal";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

const { api } = vi.hoisted(() => ({
	api: { createOrganization: vi.fn(), uploadOrganizationLogo: vi.fn() },
}));

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

beforeEach(() => {
	vi.clearAllMocks();
});

describe("CreateOrganizationModal a11y", () => {
	function open() {
		return renderWithProviders(
			<CreateOrganizationModal onClose={() => {}} onSuccess={() => {}} />,
			{ auth: { isAuthenticated: true } },
		);
	}

	it("has no violations on the empty form", async () => {
		open();
		await expectNoA11yViolations();
	});

	it("has no violations once validation has rejected the form", async () => {
		open();
		await userEvent.click(screen.getByRole("button", { name: "Create" }));

		await waitFor(() =>
			expect(screen.getByRole("textbox", { name: /name/i })).toHaveAttribute(
				"aria-invalid",
				"true",
			),
		);
		await expectNoA11yViolations();
	});

	it("describes each rejected control by its own error message", async () => {
		open();
		await userEvent.click(screen.getByRole("button", { name: "Create" }));

		const name = await screen.findByRole("textbox", { name: /name/i });
		expect(name).toHaveAttribute("aria-invalid", "true");
		expect(name).toHaveAccessibleDescription(expect.stringMatching(/\S/));
	});
});
