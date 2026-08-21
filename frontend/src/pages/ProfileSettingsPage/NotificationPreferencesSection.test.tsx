import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, within } from "@testing-library/react";
import NotificationPreferencesSection from "./NotificationPreferencesSection";
import { renderWithProviders } from "../../test/render";

/**
 * `NotificationPreferencesOrganizerRowsTests`, moved down in #2148 wave 12.
 * Remaining inventory: #2159.
 *
 * The whole class is one branch: `organizerRowsVisible = orgs.length > 0 ||
 * orgsFailed`. Two of the five preferences are labelled "... opportunities you
 * organize", so for a volunteer who organizes nothing they describe email that
 * can never fire - they are hidden, and with them the group headings that only
 * make sense when both audiences are present.
 *
 * End-to-end each case signed a user in and loaded /profile/settings to read
 * which rows rendered. Here the org list is a mocked hook return.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../test/apiMock");
	return { api: createApiMock() };
});

const { useMyOrganizations } = vi.hoisted(() => ({
	useMyOrganizations: vi.fn(),
}));

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));
vi.mock("../../hooks/useMyOrganizations", () => ({ useMyOrganizations }));

const ORGANIZER_ROWS = [
	"New sign-ups for opportunities you organize",
	"Volunteer withdrawals from opportunities you organize",
];
const VOLUNTEER_ROWS = [
	"Your sign-up is confirmed",
	"Your sign-up is cancelled",
	"Reminder before your opportunity starts",
];

beforeEach(() => {
	api.__reset();
	api.getNotificationPreferences.mockResolvedValue({
		notifyOnNewSignUp: true,
		notifyOnWithdrawal: true,
		notifyOnEngagementConfirmed: true,
		notifyOnEngagementCancelled: true,
		notifyOnEngagementReminder: true,
	});
	useMyOrganizations.mockReturnValue({
		orgs: [],
		loading: false,
		failed: false,
	});
});

function renderSection() {
	return renderWithProviders(<NotificationPreferencesSection />, {
		auth: { isAuthenticated: true },
	});
}

describe("notification preferences for an organization member", () => {
	beforeEach(() => {
		useMyOrganizations.mockReturnValue({
			orgs: [{ id: "11111111-1111-1111-1111-111111111111", name: "Org" }],
			loading: false,
			failed: false,
		});
	});

	it("still shows all five preferences", async () => {
		renderSection();

		for (const label of [...ORGANIZER_ROWS, ...VOLUNTEER_ROWS]) {
			expect(await screen.findByLabelText(label)).toBeInTheDocument();
		}
	});

	it("groups them by audience", async () => {
		renderSection();

		// The groups are <h3> headings over their own block, not ARIA groups -
		// scope by each heading's own container.
		const groupOf = (heading: HTMLElement) => {
			const block = heading.parentElement;
			expect(block).not.toBeNull();
			return block as HTMLElement;
		};
		const organizerGroup = groupOf(
			await screen.findByRole("heading", { name: "As an organizer" }),
		);
		const volunteerGroup = groupOf(
			screen.getByRole("heading", { name: "As a volunteer" }),
		);

		for (const label of ORGANIZER_ROWS) {
			expect(within(organizerGroup).getByLabelText(label)).toBeInTheDocument();
		}
		for (const label of VOLUNTEER_ROWS) {
			expect(within(volunteerGroup).getByLabelText(label)).toBeInTheDocument();
		}
	});
});

describe("notification preferences for a volunteer without an organization", () => {
	it("hides the two organizer-only preferences", async () => {
		renderSection();

		// The positive half first, so the absence assertions cannot pass against
		// a section that rendered nothing at all.
		for (const label of VOLUNTEER_ROWS) {
			expect(await screen.findByLabelText(label)).toBeInTheDocument();
		}
		for (const label of ORGANIZER_ROWS) {
			expect(screen.queryByLabelText(label)).toBeNull();
		}
	});

	it("drops the group headings, since only one audience is left", async () => {
		renderSection();

		expect(await screen.findByLabelText(VOLUNTEER_ROWS[0])).toBeInTheDocument();
		expect(
			screen.queryByRole("heading", { name: "As an organizer" }),
		).toBeNull();
		expect(
			screen.queryByRole("heading", { name: "As a volunteer" }),
		).toBeNull();
	});

	it("keeps the organizer rows when the organization lookup failed", async () => {
		// `orgsFailed` deliberately falls open: hiding a real organizer's own
		// settings is worse than showing two rows to someone who organizes
		// nothing.
		useMyOrganizations.mockReturnValue({
			orgs: [],
			loading: false,
			failed: true,
		});

		renderSection();

		for (const label of ORGANIZER_ROWS) {
			expect(await screen.findByLabelText(label)).toBeInTheDocument();
		}
	});
});
