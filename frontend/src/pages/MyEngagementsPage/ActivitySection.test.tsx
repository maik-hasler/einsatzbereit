import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ActivitySection from "./ActivitySection";
import { renderWithProviders } from "../../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const engagement = (extra: Record<string, unknown> = {}) => ({
	id: "aaaaaaaa-0000-0000-0000-000000000001",
	opportunityId: "22222222-2222-2222-2222-222222222222",
	opportunityTitle: "Deutscher Einsatz",
	opportunityTitleDe: "Deutscher Einsatz",
	opportunityTitleEn: "German shift",
	organizationId: "11111111-1111-1111-1111-111111111111",
	organizationName: "Freiwillige Feuerwehr Kiel",
	status: "Pending",
	isCheckedIn: false,
	hasFeedback: false,
	checkInMethod: "None",
	message: undefined,
	cancellationReason: undefined,
	opportunityValidUntil: undefined,
	timeSlotStartDateTime: undefined,
	timeSlotEndDateTime: undefined,
	remainingReactivations: 2,
	createdOn: new Date(Date.UTC(2026, 7, 10)),
	...extra,
});

function mockRows(items: ReturnType<typeof engagement>[]) {
	api.getMyEngagements.mockResolvedValue({
		items,
		pageCount: 1,
		totalCount: items.length,
		currentPage: 1,
	});
}

beforeEach(() => {
	api.__reset();
	api.getMyInvitations.mockResolvedValue([]);
	mockRows([engagement()]);
});

const renderSection = (lng: "de" | "en" = "en") =>
	renderWithProviders(<ActivitySection />, {
		lng,
		route: "/my-signups",
		auth: { isAuthenticated: true },
	});

describe("my-signups engagement card date region", () => {
	it("states no fixed date and the deadline for an interest-based sign-up", async () => {
		mockRows([
			engagement({
				message: "I would like to help on weekends.",
				opportunityValidUntil: new Date(Date.UTC(2027, 0, 31)),
			}),
		]);

		renderSection();

		const date = await screen.findByTestId("engagement-date");
		expect(date).toHaveAttribute("data-date-kind", "interest");
		expect(date).toHaveTextContent("No fixed date");

		expect(screen.getByText("Your message:")).toBeInTheDocument();
		expect(
			// Substring: the card wraps the message in typographic quotes.
			screen.getByText(/I would like to help on weekends\./),
		).toBeInTheDocument();
		expect(screen.getByText(/Express interest by/)).toBeInTheDocument();
	});

	it("hides a still-future deadline once the sign-up is terminal", async () => {
		mockRows([
			engagement({
				status: "Withdrawn",
				opportunityValidUntil: new Date(Date.UTC(2027, 0, 31)),
			}),
		]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		expect(within(card).getByText("Withdrawn")).toBeInTheDocument();
		expect(within(card).getByTestId("engagement-date")).toHaveAttribute(
			"data-date-kind",
			"interest",
		);
		expect(screen.queryByText(/Express interest by/)).toBeNull();
	});

	it("states the range for a scheduled sign-up instead", async () => {
		mockRows([
			engagement({
				timeSlotStartDateTime: new Date(Date.UTC(2027, 0, 14, 9, 0)),
				timeSlotEndDateTime: new Date(Date.UTC(2027, 0, 14, 12, 0)),
			}),
		]);

		renderSection();

		expect(await screen.findByTestId("engagement-date")).toHaveAttribute(
			"data-date-kind",
			"scheduled",
		);
	});
});

describe("my-signups scope toggle", () => {
	it("announces which segment is active", async () => {
		renderSection();

		const upcoming = await screen.findByTestId("engagements-scope-upcoming");
		const past = screen.getByTestId("engagements-scope-past");

		expect(upcoming).toHaveAttribute("aria-pressed", "true");
		expect(past).toHaveAttribute("aria-pressed", "false");

		await userEvent.click(past);

		await waitFor(() => expect(past).toHaveAttribute("aria-pressed", "true"));
		expect(upcoming).toHaveAttribute("aria-pressed", "false");
		expect(api.getMyEngagements).toHaveBeenLastCalledWith(
			expect.anything(),
			expect.anything(),
			false,
		);
	});
});

describe("my-signups cancellation reason", () => {
	it("shows the organizer's reason on a cancelled sign-up", async () => {
		mockRows([
			engagement({
				status: "Cancelled",
				cancellationReason: "Shift is overstaffed.",
			}),
		]);

		renderSection();

		expect(
			await screen.findByText("Reason: Shift is overstaffed."),
		).toBeInTheDocument();
	});
});

describe("my-signups withdraw failure", () => {
	it("states the specific reason rather than the generic fallback", async () => {
		mockRows([engagement()]);
		api.withdrawEngagement.mockRejectedValue({
			status: 409,
			errorCode: "Engagement.AlreadyTerminated",
		});

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		await userEvent.click(
			within(card).getByRole("button", { name: /Withdraw/ }),
		);
		await userEvent.click(
			await screen.findByRole("button", { name: "Yes, withdraw" }),
		);

		expect(
			await screen.findByText("Sign-up is already terminated."),
		).toBeInTheDocument();
		expect(
			screen.queryByText("Could not withdraw your sign-up. Please try again."),
		).toBeNull();
	});
});

describe("my-signups check-in affordance", () => {
	const confirmed = (checkInMethod: string) =>
		engagement({ status: "Confirmed", checkInMethod });

	it("offers no check-in control when the opportunity uses none", async () => {
		mockRows([confirmed("None")]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		expect(within(card).getByText("Confirmed")).toBeInTheDocument();
		expect(screen.queryByRole("button", { name: "Check in" })).toBeNull();
		expect(
			screen.queryByText("The organizer will check you in manually."),
		).toBeNull();
	});

	it("says the organizer will do it, for manual check-in", async () => {
		mockRows([confirmed("Manual")]);

		renderSection();

		expect(
			await screen.findByText("The organizer will check you in manually."),
		).toBeInTheDocument();
		expect(screen.queryByRole("button", { name: "Check in" })).toBeNull();
	});

	it("offers the scanner for a QR-code opportunity", async () => {
		mockRows([confirmed("QRCode")]);

		renderSection();

		expect(
			await screen.findByRole("button", { name: "Check in" }),
		).toBeInTheDocument();
	});
});
