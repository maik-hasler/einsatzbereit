import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CreateOrganizationModal from "./CreateOrganizationModal";
import { renderWithProviders } from "../test/render";

/**
 * The create-organization form cases from `OrganizationTests`, moved down in
 * #2148 wave 10.
 *
 * `organizationFormSchema.ts` already has its own unit tests for the zod
 * rules. What had no coverage below Playwright is the wiring: that the modal
 * runs the schema before it calls the API at all, renders each rejection as
 * an inline error the control points at, and collects every optional field
 * into a single create request.
 *
 * The E2E originals signed olaf in, loaded an org dashboard, opened the org
 * switcher and clicked through to the modal before they could type anything -
 * all of it setup to reach a form that mounts in one render here. Two of the
 * three never reached the server at all ("blocked client-side - the dialog is
 * still open, nothing was created"), which is exactly the assertion an
 * unmocked `createOrganization` spy makes directly.
 */
const { api } = vi.hoisted(() => ({
	api: { createOrganization: vi.fn(), uploadOrganizationLogo: vi.fn() },
}));

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

beforeEach(() => {
	vi.clearAllMocks();
	api.createOrganization.mockResolvedValue({
		id: { value: "33333333-3333-3333-3333-333333333333" },
		name: "Created Org",
	});
});

function open() {
	const onClose = vi.fn();
	const onSuccess = vi.fn();
	const result = renderWithProviders(
		<CreateOrganizationModal onClose={onClose} onSuccess={onSuccess} />,
	);
	return { ...result, onClose, onSuccess };
}

const field = (container: HTMLElement, id: string) => {
	const el = container.ownerDocument.getElementById(id);
	expect(el, `#${id} must exist`).not.toBeNull();
	return el as HTMLElement;
};

describe("CreateOrganizationModal validation", () => {
	it("blocks a blank submit with an inline error instead of a native tooltip", async () => {
		// The form uses the same react-hook-form + zod approach as the
		// volunteer-opportunity wizard, so an empty required field has to be
		// rejected client-side and described by its own message.
		const { container, onClose } = open();

		await userEvent.click(screen.getByTestId("modal-submit"));

		await waitFor(() =>
			expect(field(container, "create-org-name-error")).toBeVisible(),
		);
		expect(field(container, "create-org-name")).toHaveAttribute(
			"aria-invalid",
			"true",
		);

		// Blocked client-side: nothing was created and the dialog stayed open.
		expect(api.createOrganization).not.toHaveBeenCalled();
		expect(onClose).not.toHaveBeenCalled();
	});

	it("rejects a partial address field by field", async () => {
		// Address.Create requires street/houseNumber/zipCode/city together, and
		// the shared schema mirrors that conditional-required rule client-side
		// so a half-filled address never round-trips to fail.
		const { container } = open();

		await userEvent.type(
			field(container, "create-org-name"),
			"Partial Address Org",
		);
		await userEvent.type(field(container, "create-org-street"), "Main Street");

		await userEvent.click(screen.getByTestId("modal-submit"));

		await waitFor(() =>
			expect(field(container, "create-org-house-number-error")).toBeVisible(),
		);
		expect(field(container, "create-org-zip-error")).toBeVisible();
		expect(field(container, "create-org-city-error")).toBeVisible();
		expect(api.createOrganization).not.toHaveBeenCalled();
	});

	it("confirms before discarding a partly filled form", async () => {
		// Cancel must not silently drop typed input.
		const { container, onClose } = open();

		await userEvent.type(field(container, "create-org-name"), "Half Typed Org");
		await userEvent.click(screen.getByTestId("modal-cancel"));

		const discard = await screen.findByRole("button", {
			name: "Discard changes",
		});
		expect(onClose).not.toHaveBeenCalled();

		await userEvent.click(discard);
		expect(onClose).toHaveBeenCalled();
	});
});

describe("CreateOrganizationModal submission", () => {
	it("sends every collected field in one create request", async () => {
		// The modal collects description, contact details and address alongside
		// the name, and all of them have to persist in the single create call -
		// the E2E original proved this by reading them back off the settings
		// page afterwards.
		const { container, onSuccess } = open();

		await userEvent.type(
			field(container, "create-org-name"),
			"Full Details Org",
		);
		await userEvent.type(
			field(container, "create-org-description"),
			"A helpful description for volunteers.",
		);
		await userEvent.type(
			field(container, "create-org-contact-email"),
			"contact@example.com",
		);
		await userEvent.type(
			field(container, "create-org-phone"),
			"+49 30 1234567",
		);
		await userEvent.type(
			field(container, "create-org-website"),
			"https://example.com",
		);
		await userEvent.type(field(container, "create-org-street"), "Main Street");
		await userEvent.type(field(container, "create-org-house-number"), "1");
		await userEvent.type(field(container, "create-org-zip"), "12345");
		await userEvent.type(field(container, "create-org-city"), "Berlin");

		await userEvent.click(screen.getByTestId("modal-submit"));

		await waitFor(() =>
			expect(api.createOrganization).toHaveBeenCalledTimes(1),
		);
		expect(api.createOrganization).toHaveBeenCalledWith({
			name: "Full Details Org",
			description: "A helpful description for volunteers.",
			contactEmail: "contact@example.com",
			contactPhone: "+49 30 1234567",
			website: "https://example.com",
			address: {
				street: "Main Street",
				houseNumber: "1",
				zipCode: "12345",
				city: "Berlin",
			},
		});
		await waitFor(() => expect(onSuccess).toHaveBeenCalled());
	});

	it("omits the address entirely when no address field was filled", async () => {
		// `hasAddress` gates the whole nested object: sending an empty one would
		// fail Address.Create server-side for a name-only organization, which is
		// a perfectly legal thing to create.
		const { container } = open();

		await userEvent.type(field(container, "create-org-name"), "Name Only Org");
		await userEvent.click(screen.getByTestId("modal-submit"));

		await waitFor(() =>
			expect(api.createOrganization).toHaveBeenCalledTimes(1),
		);
		expect(api.createOrganization).toHaveBeenCalledWith(
			expect.objectContaining({ name: "Name Only Org", address: undefined }),
		);
	});
});
