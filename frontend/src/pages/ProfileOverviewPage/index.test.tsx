import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ProfileOverviewPage from "./index";
import { inputClass, labelClass } from "../../lib/formClasses";
import { useLocation } from "react-router";
import { renderWithProviders } from "../../test/render";

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
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

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
		renderWithStreaks({ loginStreak: 0, activityStreak: 1 });

		const tile = await screen.findByTestId("profile-stat-streak");
		expect(tile).toHaveTextContent("Woche in Serie");
		expect(tile).not.toHaveTextContent("Aktivitätsserie");
	});

	it("inflects the activity-streak unit for a count above one", async () => {
		renderWithStreaks({ loginStreak: 0, activityStreak: 3 });

		expect(await screen.findByTestId("profile-stat-streak")).toHaveTextContent(
			"Wochen in Serie",
		);
	});

	it("names each streak's own badge so the two tiles read as distinct", async () => {
		renderWithStreaks({ loginStreak: 2, activityStreak: 1 });

		const streakTile = await screen.findByTestId("profile-stat-streak");
		const loginTile = await screen.findByTestId("profile-stat-login-streak");

		expect(streakTile).toHaveTextContent("Wochenheld");
		expect(streakTile).not.toHaveTextContent("Anmeldeserie");
		expect(loginTile).toHaveTextContent("Tage in Folge");
		expect(loginTile).toHaveTextContent("Anmeldeserie");
		expect(loginTile).not.toHaveTextContent("Wochenheld");
	});

	it("inflects the login-streak unit for a single day", async () => {
		renderWithStreaks({ loginStreak: 1, activityStreak: 0 });

		const loginTile = await screen.findByTestId("profile-stat-login-streak");
		expect(loginTile).toHaveTextContent("Tag in Folge");
		expect(loginTile).not.toHaveTextContent("Login-Serie");
	});
});

describe("ProfileOverviewPage composition", () => {
	it("renders one page with no tab switcher", async () => {
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
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		await screen.findByRole("heading", { name: "Profile details" });
		expect(
			screen.queryByRole("heading", { name: /organi[sz]ations/i }),
		).toBeNull();
	});

	it("carries no in-page Home link, since the header nav owns that", async () => {
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
		expect(username).toBeDisabled();
		expect(email).toBeDisabled();
		expect(screen.getByRole("button", { name: /^Save$/ })).toBeInTheDocument();
	});

	it("sends the edited name on save", async () => {
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

describe("ProfileOverviewPage field limits", () => {
	it("caps every free-text field at the limit the server enforces", async () => {
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		await userEvent.click(await screen.findByTestId("profile-edit"));
		await waitFor(() =>
			expect(document.querySelector("#first-name")).not.toBeNull(),
		);

		const limits: Record<string, number> = {
			"#first-name": 100,
			"#last-name": 100,
			"#bio": 1000,
			"#phone": 30,
			"#skill-input": 100,
			"#lang-input": 50,
		};
		for (const [selector, max] of Object.entries(limits)) {
			expect(
				document.querySelector<HTMLInputElement>(selector)?.maxLength,
			).toBe(max);
		}
	});

	it("counts the bio's characters like the report dialog does", async () => {
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		await userEvent.click(await screen.findByTestId("profile-edit"));

		expect(await screen.findByText("15 / 1000")).toBeInTheDocument();
	});
});

describe("ProfileOverviewPage rejected save", () => {
	it("names the fields the server rejected instead of a bare failure", async () => {
		api.updateUserProfile.mockRejectedValue({
			status: 400,
			response: JSON.stringify({
				errors: {
					FirstName: [
						"The field FirstName must be a string or array type with a maximum length of '100'.",
					],
				},
			}),
		});
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		await userEvent.click(await screen.findByTestId("profile-edit"));
		await userEvent.click(
			await screen.findByRole("button", { name: /^Save$/ }),
		);

		expect(
			await screen.findByText(
				"Some entries are too long. Shorten the highlighted fields and save again.",
			),
		).toBeInTheDocument();
		expect(document.querySelector("#first-name")).toHaveAttribute(
			"aria-invalid",
			"true",
		);
		expect(
			screen.getByText("Must not be longer than 100 characters."),
		).toBeInTheDocument();
		expect(document.querySelector("#last-name")).not.toHaveAttribute(
			"aria-invalid",
		);
	});

	it("falls back to the generic message when the server blames no field", async () => {
		api.updateUserProfile.mockRejectedValue({ status: 500 });
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		await userEvent.click(await screen.findByTestId("profile-edit"));
		await userEvent.click(
			await screen.findByRole("button", { name: /^Save$/ }),
		);

		expect(
			await screen.findByText("Failed to save profile."),
		).toBeInTheDocument();
	});
});

describe("ProfileOverviewPage rejected field a11y", () => {
	it("ties the field's error text to the input that carries it", async () => {
		api.updateUserProfile.mockRejectedValue({
			status: 400,
			response: JSON.stringify({ errors: { Bio: ["too long"] } }),
		});
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		await userEvent.click(await screen.findByTestId("profile-edit"));
		await userEvent.click(
			await screen.findByRole("button", { name: /^Save$/ }),
		);

		// Wait on the rendered message, like the sibling rejected-save tests do,
		// then read the wiring off the settled DOM.
		const error = await screen.findByText(
			"Must not be longer than 1000 characters.",
		);

		expect(error).toHaveAttribute("id", "bio-error");
		// Field derives the id as `${id}-error`; without the pointer the message
		// is visible but never announced with the control.
		expect(document.querySelector("#bio")).toHaveAttribute(
			"aria-describedby",
			"bio-error",
		);
		expect(document.querySelector("#bio")).toHaveAttribute(
			"aria-invalid",
			"true",
		);
	});
});
