import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ProfileOverviewPage from "./index";
import { inputClass, labelClass } from "../../lib/formClasses";
import { useLocation } from "react-router";
import { renderWithProviders } from "../../test/render";

/**
 * Was the profile half of `SharedFormClassesTests` (#536/#1104/#1109),
 * moved down in #2148 wave 2.
 */
// Top-level await on vi.hoisted: the factory is hoisted above every import in
// this file, so it cannot reference an imported createApiMock and has to pull
// it in itself. See src/test/apiMock.ts for why pages use this instead of
// listing each endpoint by hand.
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));

beforeEach(() => {
	api.__reset();
	api.getUserProfile.mockResolvedValue({
		firstName: "Vera",
		lastName: "Volunteer",
		bio: "Ich helfe gern.",
		skills: [],
		languages: [],
		phone: undefined,
		preferredContact: undefined,
		preferredLanguage: undefined,
		avatarUrl: undefined,
	});
	api.getMyStreaks.mockResolvedValue({
		loginStreak: 0,
		activityStreak: 0,
		confirmedEngagements: 0,
	});
	api.getMyAchievements.mockResolvedValue([]);
	api.getBadgeCatalog.mockResolvedValue([]);
});

describe("ProfileOverviewPage edit form", () => {
	it("uses the shared input and label classes rather than page-local copies", async () => {
		// #536: this page and OrgSettingsPage each defined their own local
		// inputClass constant; #1109: each also had its own Field helper with a
		// hardcoded label style that diverged from labelClass on the very same
		// form. Compared against the imported constants, so a deliberate change
		// to the shared recipe does not have to be re-typed here.
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		// Fields render read-only until "Edit" - the header button once the
		// profile has data, or the empty-state CTA while it does not (#2066);
		// both carry the same testid.
		const edit = await screen.findByTestId("profile-edit");
		await userEvent.click(edit);

		await waitFor(() =>
			expect(document.querySelector("#first-name")).not.toBeNull(),
		);
		expect(document.querySelector("#first-name")).toHaveAttribute(
			"class",
			inputClass,
		);
		expect(document.querySelector("label[for='first-name']")).toHaveAttribute(
			"class",
			labelClass,
		);
	});
});

describe("ProfileOverviewPage save feedback", () => {
	it("keeps the success region mounted and empty until a save succeeds", async () => {
		// #972: a role="status" node inserted into the DOM already populated
		// does not reliably announce, so this one is mounted from the start and
		// written into on save rather than rendered on demand.
		api.updateUserProfile.mockResolvedValue(undefined);
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		const banner = await screen.findByRole("status");
		expect(banner).toHaveAttribute("aria-live", "polite");
		expect(banner).toHaveTextContent("");

		await userEvent.click(await screen.findByTestId("profile-edit"));
		await userEvent.click(
			await screen.findByRole("button", { name: /^Save$/ }),
		);

		await waitFor(() =>
			expect(screen.getByRole("status")).toHaveTextContent("Profile saved."),
		);
	});
});

/**
 * The stat-tile half of `AchievementCopyTests`, moved down from `VisualTests`
 * in #2148 wave 7. The E2E version seeded a login streak and a confirmed
 * engagement through the API purely to make these two tiles render at all -
 * both are gated on a non-zero count, which is a mocked response here.
 */
describe("ProfileOverviewPage streak stat tiles (German)", () => {
	const renderWithStreaks = (streaks: {
		loginStreak: number;
		activityStreak: number;
	}) => {
		api.getMyStreaks.mockResolvedValue({
			...streaks,
			confirmedEngagements: 0,
		});
		return renderWithProviders(<ProfileOverviewPage />, {
			lng: "de",
			auth: { isAuthenticated: true },
		});
	};

	it("gives the activity-streak tile a week unit instead of a bare number", async () => {
		// The bug: the label was the unit-less "Aktivitaetsserie", so the tile
		// read as a bare "1" with nothing saying one day, week or shift.
		// UserStreak.ActivityStreak counts consecutive ISO weeks.
		renderWithStreaks({ loginStreak: 0, activityStreak: 1 });

		const tile = await screen.findByTestId("profile-stat-streak");
		expect(tile).toHaveTextContent("Woche in Serie");
		expect(tile).not.toHaveTextContent("Aktivitätsserie");
	});

	it("inflects the activity-streak unit for a count above one", async () => {
		// The E2E version could not assert this: every confirmation in one test
		// session lands in the same ISO week, so it only ever observed the _one
		// form and had to branch on whatever rendered.
		renderWithStreaks({ loginStreak: 0, activityStreak: 3 });

		expect(await screen.findByTestId("profile-stat-streak")).toHaveTextContent(
			"Wochen in Serie",
		);
	});

	it("names each streak's own badge so the two tiles read as distinct", async () => {
		// #1935: with both stats correctly labelled, the week-streak tile
		// ("X Wochen in Serie") and the day-streak tile ("Y Tage in Folge
		// angemeldet") still sat side by side reading as near-synonyms with
		// different units. Each now names the badge it backs. Cross-check that
		// neither carries the other's name - that mismatch is the reported
		// confusion, and asserting only that each carries its own would miss it.
		renderWithStreaks({ loginStreak: 2, activityStreak: 1 });

		const streakTile = await screen.findByTestId("profile-stat-streak");
		const loginTile = await screen.findByTestId("profile-stat-login-streak");

		expect(streakTile).toHaveTextContent("Wochenheld");
		expect(streakTile).not.toHaveTextContent("Anmeldeserie");
		expect(loginTile).toHaveTextContent("Tage in Folge angemeldet");
		expect(loginTile).toHaveTextContent("Anmeldeserie");
		expect(loginTile).not.toHaveTextContent("Wochenheld");
	});

	it("inflects the login-streak unit for a single day", async () => {
		renderWithStreaks({ loginStreak: 1, activityStreak: 0 });

		const loginTile = await screen.findByTestId("profile-stat-login-streak");
		expect(loginTile).toHaveTextContent("Tag in Folge angemeldet");
		expect(loginTile).not.toHaveTextContent("Login-Serie");
	});
});

/**
 * `ProfileOverviewTests` and `OrgAppRestructureTests`' profile case, moved
 * down in #2148 wave 12. Remaining inventory: #2159.
 *
 * These are page composition and form state: which sections render, what the
 * read-only identity fields carry, and what the save call sends. The E2E
 * originals signed vera in and loaded /profile for each.
 */
describe("ProfileOverviewPage composition", () => {
	it("renders one page with no tab switcher", async () => {
		// #794 consolidated a Profile/Activity tab switcher into a single page,
		// and #1684 moved sign-ups out to /my-signups. The positive headings
		// come first deliberately: the three absence assertions below are the
		// kind that pass forever on their own.
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		expect(
			await screen.findByRole("heading", { name: "Profile details" }),
		).toBeInTheDocument();
		expect(screen.getByRole("heading", { name: "Badges" })).toBeInTheDocument();

		for (const name of ["Profile", "Activity", "Share achievements"]) {
			expect(screen.queryByRole("button", { name })).toBeNull();
		}
	});

	it("no longer carries an organizations section", async () => {
		// The org app owns organizations now; this page used to list them too.
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		await screen.findByRole("heading", { name: "Profile details" });
		expect(
			screen.queryByRole("heading", { name: /organi[sz]ations/i }),
		).toBeNull();
	});

	it("carries no in-page Home link, since the header nav owns that", async () => {
		// Every subpage used to restate "back to the home page" in its own title
		// band. The header's `nav-home` is asserted in Header.test.tsx; what this
		// page owes is *not* repeating it.
		const { container } = renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		await screen.findByRole("heading", { name: "Profile details" });
		expect(container.querySelector("nav[aria-label='Breadcrumb']")).toBeNull();
		expect(screen.queryByRole("link", { name: "Home" })).toBeNull();
	});
});

describe("ProfileOverviewPage identity fields", () => {
	it("shows username and email as read-only, beside a Save action", async () => {
		api.getUserProfile.mockResolvedValue({
			firstName: "Vera",
			lastName: "Volunteer",
			username: "vera",
			email: "vera@example.com",
			bio: "",
			skills: [],
			languages: [],
		});

		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		await userEvent.click(await screen.findByTestId("profile-edit"));

		const username = screen.getByLabelText("Username");
		const email = screen.getByLabelText("Email address");
		expect(username).toHaveValue("vera");
		expect(email).toHaveValue("vera@example.com");
		// Read-only by design - identity lives in Keycloak, not here.
		expect(username).toBeDisabled();
		expect(email).toBeDisabled();
		expect(screen.getByRole("button", { name: /^Save$/ })).toBeInTheDocument();
	});

	it("sends the edited name on save", async () => {
		// The sibling case above asserts the success region; this one asserts
		// what actually left for the server, which nothing else covered.
		api.updateUserProfile.mockResolvedValue(undefined);

		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		await userEvent.click(await screen.findByTestId("profile-edit"));
		const first = screen.getByLabelText("First name");
		await userEvent.clear(first);
		await userEvent.type(first, "Vera");
		const last = screen.getByLabelText("Last name");
		await userEvent.clear(last);
		await userEvent.type(last, "Sample");

		await userEvent.click(screen.getByRole("button", { name: /^Save$/ }));

		await waitFor(() =>
			expect(api.updateUserProfile).toHaveBeenCalledWith(
				expect.objectContaining({ firstName: "Vera", lastName: "Sample" }),
			),
		);
	});
});

describe("ProfileOverviewPage legacy tab deep links", () => {
	function LocationProbe() {
		const location = useLocation();
		return <output data-testid="location">{location.pathname}</output>;
	}

	it("redirects the old engagements tab to /my-signups", async () => {
		// Invitations and sign-ups moved to their own page (#1684); the old
		// ?tab= deep links have to land there rather than on a tab that no
		// longer exists.
		renderWithProviders(
			<>
				<ProfileOverviewPage />
				<LocationProbe />
			</>,
			{ auth: { isAuthenticated: true }, route: "/profile?tab=engagements" },
		);

		await waitFor(() =>
			expect(screen.getByTestId("location")).toHaveTextContent("/my-signups"),
		);
	});

	it("stays on /profile for a tab that is not a legacy alias", async () => {
		renderWithProviders(
			<>
				<ProfileOverviewPage />
				<LocationProbe />
			</>,
			{ auth: { isAuthenticated: true }, route: "/profile" },
		);

		await screen.findByRole("heading", { name: "Profile details" });
		expect(screen.getByTestId("location")).toHaveTextContent("/profile");
	});
});
