import { describe, it } from "vitest";
import BadgeGrid from "./BadgeGrid";
import type {
	AchievementSummary,
	BadgeCatalogEntry,
} from "../client/api-client";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

/**
 * Covers the achievements block scanned end-to-end by
 * `ProfileOverviewPage_*` and `UserProfilePage_*`. Both scans needed a login
 * and a seeded achievement history to reach it; the interesting states here
 * are earned vs. not-earned vs. loading, which are props.
 */
const catalog: BadgeCatalogEntry[] = [
	{
		key: "first-engagement",
		type: 0,
		name: "First engagement",
		description: "Signed up for your first opportunity.",
		isHidden: false,
	},
	{
		key: "five-engagements",
		type: 1,
		name: "Five engagements",
		description: "Completed five opportunities.",
		isHidden: false,
	},
	{
		key: "login-streak-7",
		type: 2,
		name: "One week streak",
		description: "Signed in seven days in a row.",
		isHidden: false,
	},
];

const earned: AchievementSummary[] = [
	{
		id: "ach-1",
		type: "FirstEngagement",
		key: "first-engagement",
		name: "First engagement",
		description: "Signed up for your first opportunity.",
		unlockedAt: new Date(Date.UTC(2026, 6, 1, 10, 0)),
	},
];

describe("BadgeGrid a11y", () => {
	it("has no violations while loading", async () => {
		renderWithProviders(<BadgeGrid earned={[]} catalog={[]} loading />);
		await expectNoA11yViolations();
	});

	it("has no violations with a mix of earned and unearned badges", async () => {
		renderWithProviders(<BadgeGrid earned={earned} catalog={catalog} />);
		await expectNoA11yViolations();
	});

	it("has no violations with progress indicators (the viewer's own profile)", async () => {
		renderWithProviders(
			<BadgeGrid
				earned={earned}
				catalog={catalog}
				progress={{ engagements: 3, loginStreak: 4, activityStreak: 2 }}
			/>,
		);
		await expectNoA11yViolations();
	});

	it("has no violations with nothing earned yet (someone else's public profile)", async () => {
		renderWithProviders(<BadgeGrid earned={[]} catalog={catalog} />);
		await expectNoA11yViolations();
	});
});
