import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CreateOrganizationModal from "./CreateOrganizationModal";
import { renderWithProviders, type TestAuth } from "../test/render";

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

function open(auth: TestAuth = {}) {
	const onClose = vi.fn();
	const onSuccess = vi.fn();
	const result = renderWithProviders(
		<CreateOrganizationModal onClose={onClose} onSuccess={onSuccess} />,
		{ auth },
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
		const { container, onClose } = open();

		await userEvent.click(screen.getByTestId("modal-submit"));

		await waitFor(() =>
			expect(field(container, "create-org-name-error")).toBeVisible(),
		);
		expect(field(container, "create-org-name")).toHaveAttribute(
			"aria-invalid",
			"true",
		);

		expect(api.createOrganization).not.toHaveBeenCalled();
		expect(onClose).not.toHaveBeenCalled();
	});

	it("rejects a partial address field by field", async () => {
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

describe("CreateOrganizationModal auth refresh (#2206)", () => {
	it("refreshes the access token before reporting success, since creating an organization grants the organizer role server-side", async () => {
		const callOrder: string[] = [];
		const signinSilent = vi.fn().mockImplementation(async () => {
			callOrder.push("signinSilent");
			return null;
		});
		const { container, onSuccess } = open({ signinSilent });
		onSuccess.mockImplementation(() => callOrder.push("onSuccess"));

		await userEvent.type(field(container, "create-org-name"), "Fresh Org");
		await userEvent.click(screen.getByTestId("modal-submit"));

		await waitFor(() => expect(onSuccess).toHaveBeenCalled());
		expect(signinSilent).toHaveBeenCalledTimes(1);
		expect(callOrder).toEqual(["signinSilent", "onSuccess"]);
	});

	it("still reports success when the silent refresh itself fails", async () => {
		const signinSilent = vi.fn().mockRejectedValue(new Error("no SSO session"));
		const { container, onSuccess } = open({ signinSilent });

		await userEvent.type(field(container, "create-org-name"), "Fresh Org");
		await userEvent.click(screen.getByTestId("modal-submit"));

		await waitFor(() => expect(onSuccess).toHaveBeenCalled());
	});
});
