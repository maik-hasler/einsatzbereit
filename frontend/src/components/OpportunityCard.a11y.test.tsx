import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import OpportunityCard, { type OpportunityCardItem } from "./OpportunityCard";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

/**
 * The one card every surface that lists an opportunity renders (#2054), and
 * therefore the shared subject of `OpportunitiesPage_*`,
 * `LandingOpportunityPreview*`, `OrganizationProfilePage_*` and the "more
 * from this organization" rail on the detail page.
 *
 * The stretched-link pattern (frontend/AGENTS.md, "Clickable cards") is the
 * a11y-relevant part, and it is markup.
 */
const base: OpportunityCardItem = {
	id: "opp-1",
	titleDe: "Strandreinigung",
	titleEn: "Beach cleanup",
	descriptionDe: "Wir sammeln Muell am Strand.",
	descriptionEn: "We collect litter on the beach.",
	street: "Strandweg",
	houseNumber: "1",
	zipCode: "24103",
	city: "Kiel",
	isRemote: false,
	occurrence: "OneTime",
	participationType: "ScheduledSlots",
	category: "Environment",
	totalMaxParticipants: 10,
	currentParticipantCount: 3,
	validUntil: new Date(Date.UTC(2026, 8, 30)),
	nextTimeSlotStart: new Date(Date.UTC(2026, 7, 27, 9, 0)),
};

function renderCard(item: OpportunityCardItem, headingLevel: 2 | 3 = 2) {
	return renderWithProviders(
		<ul>
			<OpportunityCard item={item} headingLevel={headingLevel} />
		</ul>,
	);
}

describe("OpportunityCard a11y", () => {
	it("has no violations for the lean summary shape", async () => {
		renderCard(base);
		await expectNoA11yViolations();
	});

	it("has no violations with organization identity, tags and a banner", async () => {
		renderCard({
			...base,
			organizationId: "org-1",
			organizationName: "Freiwillige Feuerwehr Kiel",
			organizationLogoUrl: "https://storage.example.test/logo.png",
			tags: ["cleanup", "outdoors"],
			bannerImageUrl: "https://storage.example.test/banner.jpg",
		});
		await expectNoA11yViolations();
	});

	it("has no violations when it is full, remote and open-ended", async () => {
		renderCard({
			...base,
			isRemote: true,
			street: undefined,
			houseNumber: undefined,
			zipCode: undefined,
			city: undefined,
			occurrence: "Recurring",
			totalMaxParticipants: 3,
			currentParticipantCount: 3,
			validUntil: undefined,
			nextTimeSlotStart: undefined,
		});
		await expectNoA11yViolations();
	});

	it("has no violations at heading level 3, under a section heading", async () => {
		renderWithProviders(
			<section aria-labelledby="latest">
				<h2 id="latest">Latest opportunities</h2>
				<ul>
					<OpportunityCard item={base} headingLevel={3} />
				</ul>
			</section>,
		);
		await expectNoA11yViolations();
	});

	it("exposes exactly one stretched link naming the opportunity", async () => {
		renderCard(base);
		const link = screen.getByRole("link", { name: /Beach cleanup/ });
		expect(link).toHaveAttribute(
			"href",
			expect.stringContaining("/volunteer-opportunities/opp-1"),
		);
	});
});
