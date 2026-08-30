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

	it("reads the initial segment from the URL, so it survives a reload (#2240)", async () => {
		mockRows([engagement({ status: "Withdrawn" })]);

		renderWithProviders(<ActivitySection />, {
			route: "/my-signups?scope=past",
			auth: { isAuthenticated: true },
		});

		const past = await screen.findByTestId("engagements-scope-past");
		expect(past).toHaveAttribute("aria-pressed", "true");
		expect(screen.getByTestId("engagements-scope-upcoming")).toHaveAttribute(
			"aria-pressed",
			"false",
		);
		await waitFor(() =>
			expect(api.getMyEngagements).toHaveBeenLastCalledWith(
				expect.anything(),
				expect.anything(),
				false,
			),
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

describe("my-signups engagement card organization link", () => {
	it("links the organization name to its public profile", async () => {
		mockRows([
			engagement({
				organizationId: "55555555-5555-5555-5555-555555555555",
				organizationName: "Malteser Kiel",
			}),
		]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		const link = within(card).getByRole("link", { name: "Malteser Kiel" });
		expect(link).toHaveAttribute(
			"href",
			"/organizations/55555555-5555-5555-5555-555555555555",
		);
	});
});

describe("my-signups scheduled time slot", () => {
	it("states the scheduled time and the original sign-up date on the same card", async () => {
		mockRows([
			engagement({
				status: "Confirmed",
				timeSlotId: "66666666-6666-6666-6666-666666666666",
				timeSlotStartDateTime: new Date(Date.UTC(2027, 0, 14, 9, 0)),
				timeSlotEndDateTime: new Date(Date.UTC(2027, 0, 14, 12, 0)),
			}),
		]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		expect(within(card).getByText(/^Scheduled:/)).toBeInTheDocument();
		expect(within(card).getByText(/^Signed up:/)).toBeInTheDocument();
	});
});

describe("my-signups withdraw success (#2240)", () => {
	it("keeps the card under upcoming, now withdrawn with a way back in", async () => {
		const target = engagement({ status: "Confirmed" });
		mockRows([target]);
		api.withdrawEngagement.mockResolvedValue({
			...target,
			status: "Withdrawn",
		});

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		await userEvent.click(
			within(card).getByRole("button", { name: /Withdraw/ }),
		);
		await userEvent.click(
			await screen.findByRole("button", { name: "Yes, withdraw" }),
		);

		await waitFor(() =>
			expect(within(card).getByText("Withdrawn")).toBeInTheDocument(),
		);
		expect(screen.getByTestId("engagements-scope-upcoming")).toHaveAttribute(
			"aria-pressed",
			"true",
		);
		expect(
			within(card).getByRole("link", { name: "Sign up again" }),
		).toHaveAttribute(
			"href",
			`/volunteer-opportunities/${target.opportunityId}?signUp=interest`,
		);
	});
});

describe("my-signups reactivate action (#2240)", () => {
	it("offers a way to sign up again for a withdrawn, still-open engagement", async () => {
		mockRows([engagement({ status: "Withdrawn", remainingReactivations: 2 })]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		expect(
			within(card).getByRole("link", { name: "Sign up again" }),
		).toHaveAttribute(
			"href",
			"/volunteer-opportunities/22222222-2222-2222-2222-222222222222?signUp=interest",
		);
	});

	it("shows the reactivation limit message instead, once it's reached", async () => {
		mockRows([engagement({ status: "Withdrawn", remainingReactivations: 0 })]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		expect(
			within(card).queryByRole("link", { name: "Sign up again" }),
		).toBeNull();
		expect(within(card).getByText(/reached the limit/)).toBeInTheDocument();
	});

	it("hides the reactivate action once the sign-up has moved to past", async () => {
		mockRows([engagement({ status: "Withdrawn", remainingReactivations: 2 })]);

		renderSection();
		await userEvent.click(await screen.findByTestId("engagements-scope-past"));

		const card = await screen.findByTestId("engagement-card");
		expect(
			within(card).queryByRole("link", { name: "Sign up again" }),
		).toBeNull();
	});
});

describe("my-signups calendar button (#2240)", () => {
	const timeSlotId = "66666666-6666-6666-6666-666666666666";

	it("hides Add to calendar once the time slot has ended", async () => {
		mockRows([
			engagement({
				status: "Confirmed",
				timeSlotId,
				timeSlotStartDateTime: new Date(Date.UTC(2020, 0, 14, 9, 0)),
				timeSlotEndDateTime: new Date(Date.UTC(2020, 0, 14, 12, 0)),
			}),
		]);

		renderSection();

		await screen.findByTestId("engagement-card");
		expect(screen.queryByText("Add to calendar")).toBeNull();
	});

	it("offers Add to calendar while the time slot has not ended", async () => {
		const start = new Date(Date.now() + 24 * 60 * 60 * 1000);
		const end = new Date(start.getTime() + 2 * 60 * 60 * 1000);
		mockRows([
			engagement({
				status: "Confirmed",
				timeSlotId,
				timeSlotStartDateTime: start,
				timeSlotEndDateTime: end,
			}),
		]);

		renderSection();

		expect(await screen.findByText("Add to calendar")).toBeInTheDocument();
	});
});

describe("my-signups opportunity title language (#2328)", () => {
	it("shows the English title in the English interface", async () => {
		mockRows([engagement()]);

		renderSection("en");

		const card = await screen.findByTestId("engagement-card");
		const link = within(card).getByRole("link", { name: "German shift" });
		expect(link).toHaveAttribute("lang", "en");
		expect(
			within(card).queryByText("Deutscher Einsatz"),
		).not.toBeInTheDocument();
	});

	it("shows the German title in the German interface", async () => {
		mockRows([engagement()]);

		renderSection("de");

		const card = await screen.findByTestId("engagement-card");
		const link = within(card).getByRole("link", { name: "Deutscher Einsatz" });
		expect(link).toHaveAttribute("lang", "de");
	});

	// A German-only opportunity has no titleEn to pick, so the English
	// interface keeps the German title - marked up as German either way.
	it("falls back to the German title when no translation exists", async () => {
		mockRows([engagement({ opportunityTitleEn: undefined })]);

		renderSection("en");

		const card = await screen.findByTestId("engagement-card");
		const link = within(card).getByRole("link", { name: "Deutscher Einsatz" });
		expect(link).toHaveAttribute("lang", "de");
	});
});

describe("my-signups withdraw copy (#2228)", () => {
	it("speaks of withdrawing interest, not releasing a seat, for an interest-based sign-up", async () => {
		mockRows([engagement({ status: "Confirmed" })]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		await userEvent.click(
			within(card).getByRole("button", { name: /Withdraw/ }),
		);

		expect(
			await screen.findByRole("heading", {
				name: "Withdraw expression of interest?",
			}),
		).toBeInTheDocument();
		expect(
			screen.getByText(
				'Your expression of interest for "German shift" will be withdrawn, and you\'ll be able to express interest again later.',
			),
		).toBeInTheDocument();
	});

	it("keeps the seat-release copy for a scheduled sign-up", async () => {
		mockRows([
			engagement({
				status: "Confirmed",
				timeSlotId: "66666666-6666-6666-6666-666666666666",
				timeSlotStartDateTime: new Date(Date.UTC(2027, 0, 14, 9, 0)),
				timeSlotEndDateTime: new Date(Date.UTC(2027, 0, 14, 12, 0)),
			}),
		]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		await userEvent.click(
			within(card).getByRole("button", { name: /Withdraw/ }),
		);

		expect(
			await screen.findByRole("heading", { name: "Withdraw sign-up?" }),
		).toBeInTheDocument();
		expect(
			screen.getByText(
				'Your spot for "German shift" will be released, and you\'ll be able to sign up again later.',
			),
		).toBeInTheDocument();
	});
});

describe("my-signups engagement grid columns", () => {
	it("caps at two columns rather than leaving a dangling cell for one card", async () => {
		mockRows([engagement()]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		const list = card.closest("ul");
		expect(list?.className).toContain("@sm:grid-cols-2");
		expect(list?.className).not.toContain("@4xl:grid-cols-3");
	});

	it("allows three columns once there are enough cards to fill a row", async () => {
		mockRows([
			engagement({ id: "aaaaaaaa-0000-0000-0000-000000000001" }),
			engagement({ id: "aaaaaaaa-0000-0000-0000-000000000002" }),
			engagement({ id: "aaaaaaaa-0000-0000-0000-000000000003" }),
		]);

		renderSection();

		const cards = await screen.findAllByTestId("engagement-card");
		expect(cards).toHaveLength(3);
		expect(cards[0].closest("ul")?.className).toContain("@4xl:grid-cols-3");
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

describe("my-signups invitations (#2206)", () => {
	const invitation = (extra: Record<string, unknown> = {}) => ({
		id: "77777777-7777-7777-7777-777777777777",
		organizationId: "11111111-1111-1111-1111-111111111111",
		organizationName: "Malteser Kiel",
		createdOn: new Date(Date.UTC(2026, 7, 1)),
		...extra,
	});

	it("refreshes the access token after accepting, since the invitation may have granted the organizer role", async () => {
		api.getMyInvitations.mockResolvedValue([invitation()]);
		api.acceptInvitation.mockResolvedValue(undefined);
		const signinSilent = vi.fn().mockResolvedValue(null);

		renderWithProviders(<ActivitySection />, {
			route: "/my-signups",
			auth: { isAuthenticated: true, signinSilent },
		});

		await userEvent.click(
			await screen.findByRole("button", { name: "Accept" }),
		);

		await waitFor(() =>
			expect(api.acceptInvitation).toHaveBeenCalledWith(invitation().id),
		);
		expect(signinSilent).toHaveBeenCalledTimes(1);
	});

	it("still removes the invitation from the list when the silent refresh itself fails", async () => {
		api.getMyInvitations.mockResolvedValue([invitation()]);
		api.acceptInvitation.mockResolvedValue(undefined);
		const signinSilent = vi.fn().mockRejectedValue(new Error("no SSO session"));

		renderWithProviders(<ActivitySection />, {
			route: "/my-signups",
			auth: { isAuthenticated: true, signinSilent },
		});

		await userEvent.click(
			await screen.findByRole("button", { name: "Accept" }),
		);

		await waitFor(() =>
			expect(screen.queryByRole("button", { name: "Accept" })).toBeNull(),
		);
	});
});

describe("my-signups check-in time window (#2323)", () => {
	const hoursFromNow = (hours: number) =>
		new Date(Date.now() + hours * 60 * 60 * 1000);

	const slotSignUp = (startHours: number, endHours: number, extra = {}) =>
		engagement({
			status: "Confirmed",
			checkInMethod: "PINCode",
			timeSlotId: "33333333-3333-3333-3333-333333333333",
			timeSlotStartDateTime: hoursFromNow(startHours),
			timeSlotEndDateTime: hoursFromNow(endHours),
			...extra,
		});

	it("offers check-in while the slot is running", async () => {
		mockRows([slotSignUp(-1, 2)]);

		renderSection();

		expect(
			await screen.findByRole("button", { name: "Check in" }),
		).toBeInTheDocument();
		expect(screen.queryByTestId("check-in-opens-at")).toBeNull();
	});

	it("names the opening time instead of a live button for a slot weeks away", async () => {
		mockRows([slotSignUp(24 * 22, 24 * 22 + 4)]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		expect(within(card).getByTestId("check-in-opens-at")).toHaveTextContent(
			/Check-in possible from/,
		);
		expect(screen.queryByRole("button", { name: "Check in" })).toBeNull();
	});

	it("explains an ended slot nobody checked the volunteer in for, and drops the two dead controls", async () => {
		mockRows([slotSignUp(-8, -6)]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		expect(
			within(card).getByTestId("check-in-window-closed"),
		).toHaveTextContent("The organization did not check you in for this date");
		expect(
			within(card).getByRole("link", { name: "Contact the organization" }),
		).toBeInTheDocument();
		expect(screen.queryByRole("button", { name: "Check in" })).toBeNull();
		expect(screen.queryByRole("button", { name: "Withdraw" })).toBeNull();
	});

	it("still offers withdraw while the slot is in the future", async () => {
		mockRows([slotSignUp(48, 52)]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		expect(
			within(card).getByRole("button", { name: "Withdraw" }),
		).toBeInTheDocument();
	});
});

describe("my-signups rating entry point (#2323)", () => {
	const hoursFromNow = (hours: number) =>
		new Date(Date.now() + hours * 60 * 60 * 1000);

	const checkedIn = (startHours: number, endHours: number, extra = {}) =>
		engagement({
			status: "Confirmed",
			isCheckedIn: true,
			checkInMethod: "PINCode",
			timeSlotId: "33333333-3333-3333-3333-333333333333",
			timeSlotStartDateTime: hoursFromNow(startHours),
			timeSlotEndDateTime: hoursFromNow(endHours),
			...extra,
		});

	it("withholds the rating from someone who checked in early, before the slot starts", async () => {
		mockRows([checkedIn(0.5, 3)]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		expect(within(card).getByTestId("feedback-after-event")).toHaveTextContent(
			"You can rate this once the opportunity has ended.",
		);
		expect(screen.queryByRole("button", { name: "Leave feedback" })).toBeNull();
	});

	it("offers the rating once the slot is over", async () => {
		mockRows([checkedIn(-4, -2)]);

		renderSection();

		expect(
			await screen.findByRole("button", { name: "Leave feedback" }),
		).toBeInTheDocument();
		expect(screen.queryByTestId("feedback-after-event")).toBeNull();
	});

	it("shows the score that was given and when it stops being editable", async () => {
		mockRows([
			checkedIn(-4, -2, {
				hasFeedback: true,
				feedbackRating: 4,
				feedbackSubmittedAt: new Date(),
			}),
		]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		expect(
			within(card).getByRole("img", { name: "4 out of 5 stars" }),
		).toBeInTheDocument();
		expect(
			within(card).getByTestId("feedback-editable-until"),
		).toHaveTextContent(/You can edit or delete this until/);
	});

	it("drops the edit affordances and the deadline line once the edit window has passed", async () => {
		mockRows([
			checkedIn(-24 * 30, -24 * 30 + 2, {
				hasFeedback: true,
				feedbackRating: 5,
				feedbackSubmittedAt: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000),
			}),
		]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		expect(within(card).getByText("Feedback given")).toBeInTheDocument();
		expect(within(card).queryByTestId("feedback-editable-until")).toBeNull();
		expect(screen.queryByRole("button", { name: "Edit" })).toBeNull();
	});
});

describe("my-signups past empty state (#2323)", () => {
	it("says what would show up there and offers a way to get there", async () => {
		mockRows([]);

		renderWithProviders(<ActivitySection />, {
			route: "/my-signups?scope=past",
			auth: { isAuthenticated: true },
		});

		expect(
			await screen.findByText("No past sign-ups yet."),
		).toBeInTheDocument();
		expect(screen.getByText(/that's where you rate it/)).toBeInTheDocument();
		expect(
			screen.getByRole("link", { name: "Explore opportunities" }),
		).toBeInTheDocument();
	});
});

describe("my-signups withdraw dialog reactivation budget (#2323)", () => {
	it("states how many sign-ups are left before the limit", async () => {
		mockRows([engagement({ status: "Confirmed", remainingReactivations: 3 })]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		await userEvent.click(
			within(card).getByRole("button", { name: "Withdraw" }),
		);

		expect(
			await screen.findByTestId("withdraw-remaining-reactivations"),
		).toHaveTextContent(
			"After this you can express interest again 3 more times.",
		);
	});

	it("leaves the last one to the stronger warning instead of double-counting it", async () => {
		mockRows([engagement({ status: "Confirmed", remainingReactivations: 1 })]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		await userEvent.click(
			within(card).getByRole("button", { name: "Withdraw" }),
		);

		await screen.findByRole("button", { name: "Yes, withdraw" });
		expect(screen.queryByTestId("withdraw-remaining-reactivations")).toBeNull();
		expect(screen.getByText(/only one more chance/)).toBeInTheDocument();
	});
});

describe("my-signups reactivate deep link (#2323)", () => {
	it("carries the slot the withdrawn sign-up was for", async () => {
		mockRows([
			engagement({
				status: "Withdrawn",
				remainingReactivations: 2,
				timeSlotId: "33333333-3333-3333-3333-333333333333",
				timeSlotStartDateTime: new Date(Date.now() + 48 * 60 * 60 * 1000),
				timeSlotEndDateTime: new Date(Date.now() + 52 * 60 * 60 * 1000),
			}),
		]);

		renderSection();

		const card = await screen.findByTestId("engagement-card");
		expect(
			within(card).getByRole("link", { name: "Sign up again" }),
		).toHaveAttribute(
			"href",
			"/volunteer-opportunities/22222222-2222-2222-2222-222222222222?signUp=33333333-3333-3333-3333-333333333333",
		);
	});
});
