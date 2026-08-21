import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ActivitySection from "./ActivitySection";
import { renderWithProviders } from "../../test/render";

/**
 * The `/my-signups` engagement-card cases from `MyEngagementsScopeTabsTests`,
 * `OpportunityCardContractTests`, `MyEngagementsWithdrawErrorMessageTests`,
 * `MyEngagementsScopeToggleTests`, `EngagementCancellationReasonTests` and
 * `CheckInAndSlotTests`, moved down in #2148 wave 13. Remaining inventory:
 * #2159.
 *
 * Every one is a conditional over a single engagement row's fields. The E2E
 * originals each seeded an opportunity and an engagement over three or four
 * HTTP calls and then paged the list until the right card was on screen; the
 * row is a mock literal here, and there is only ever one of it.
 *
 * Which rows the API returns for a given scope is a server-side decision
 * (`EngagementReadRepository.GetByVolunteerAsync`) - covered in
 * `IntegrationTests/EngagementReadRepositoryTests.cs`, also as part of #2148.
 * What is left for this file is what the card does with a row once it has one.
 */
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
		// #1777: the volunteer's own application message used to occupy this
		// region for any sign-up without a time slot, so an interest-based card
		// showed a quoted fragment of its own message where the next card showed
		// a date.
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

		// The message is still on the card - labelled, and below the date.
		expect(screen.getByText("Your message:")).toBeInTheDocument();
		// The card wraps the message in typographic quotes, so this matches the
		// substring rather than the whole text node.
		expect(
			screen.getByText(/I would like to help on weekends\./),
		).toBeInTheDocument();
		expect(screen.getByText(/Express interest by/)).toBeInTheDocument();
	});

	it("hides a still-future deadline once the sign-up is terminal", async () => {
		// #2070: an IndividualContact opportunity stays open for other
		// volunteers long after this one withdrew, so its deadline kept reading
		// as a future-looking date on a card that is otherwise done.
		mockRows([
			engagement({
				status: "Withdrawn",
				opportunityValidUntil: new Date(Date.UTC(2027, 0, 31)),
			}),
		]);

		renderSection();

		// The positive half first: the card is there and states its status, so
		// the absence below cannot pass against an empty list.
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

		// `aria-pressed` is the only thing that carries this to a screen reader -
		// the active segment is otherwise distinguished by background alone.
		expect(upcoming).toHaveAttribute("aria-pressed", "true");
		expect(past).toHaveAttribute("aria-pressed", "false");

		await userEvent.click(past);

		await waitFor(() => expect(past).toHaveAttribute("aria-pressed", "true"));
		expect(upcoming).toHaveAttribute("aria-pressed", "false");
		// And the scope is a server-side query parameter, not a client filter.
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
		// The same `err instanceof Error` defect as the detail page's own
		// withdraw handler - a ProblemDetails object is not an Error, so every
		// failure collapsed to "Could not withdraw your sign-up".
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
		// Text, not a control: there is nothing for the volunteer to press.
		expect(screen.queryByRole("button", { name: "Check in" })).toBeNull();
	});

	it("offers the scanner for a QR-code opportunity", async () => {
		// The branch that makes the two above more than "no button rendered".
		mockRows([confirmed("QRCode")]);

		renderSection();

		expect(
			await screen.findByRole("button", { name: "Check in" }),
		).toBeInTheDocument();
	});
});
