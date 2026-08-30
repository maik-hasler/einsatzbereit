import { describe, it } from "vitest";
import OpportunityResultsList from "./OpportunityResultsList";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import { renderWithProviders } from "../../test/render";
import { expectNoA11yViolations } from "../../test/a11y";

const item: VolunteerOpportunitySummary = {
	id: "opp-1",
	titleDe: "Strandreinigung",
	titleEn: "Beach cleanup",
	descriptionDe: "Muell sammeln.",
	descriptionEn: "Collect litter.",
	organizationId: "org-1",
	organizationName: "Freiwillige Feuerwehr Kiel",
	street: "Strandweg",
	houseNumber: "1",
	zipCode: "24103",
	city: "Kiel",
	latitude: 54.3233,
	longitude: 10.1228,
	isRemote: false,
	occurrence: "OneTime",
	participationType: "ScheduledSlots",
	checkInMethod: "QrCode",
	category: "Environment",
	tags: [],
	createdOn: new Date(Date.UTC(2026, 7, 1)),
	validUntil: undefined,
	nextTimeSlotStart: new Date(Date.UTC(2026, 7, 27, 9, 0)),
	nextTimeSlotEnd: new Date(Date.UTC(2026, 7, 27, 17, 0)),
	totalMaxParticipants: 10,
	currentParticipantCount: 0,
	status: "Published",
	bannerImageUrl: undefined,
	organizationLogoUrl: undefined,
};

const baseProps = {
	loading: false,
	error: null as string | null,
	errorIsOffline: false,
	items: [] as VolunteerOpportunitySummary[],
	totalItems: undefined as number | undefined,
	hasFilters: false,
	onClearFilters: () => {},
	hasMore: false,
	loadingMore: false,
	onLoadMore: () => {},
	loadMoreError: null as string | null,
	loadMoreErrorIsOffline: false,
	onRetryLoadMore: () => {},
	pageSize: 9,
};

function renderList(props: Partial<typeof baseProps>) {
	return renderWithProviders(
		<OpportunityResultsList {...baseProps} {...props} />,
	);
}

describe("OpportunityResultsList a11y", () => {
	it("has no violations while the initial page is loading", async () => {
		renderList({ loading: true });
		await expectNoA11yViolations();
	});

	it("has no violations for the empty state with filters applied", async () => {
		renderList({ hasFilters: true });
		await expectNoA11yViolations();
	});

	it("has no violations for an offline error", async () => {
		renderList({ error: "network", errorIsOffline: true });
		await expectNoA11yViolations();
	});

	it("has no violations for a generic load error", async () => {
		renderList({ error: "Server error", errorIsOffline: false });
		await expectNoA11yViolations();
	});

	it("has no violations once results are loaded with the total count announced", async () => {
		renderList({ items: [item], totalItems: 1 });
		await expectNoA11yViolations();
	});

	it("has no violations with more results available, showing the loaded-of-total ratio", async () => {
		renderList({ items: [item], totalItems: 5, hasMore: true });
		await expectNoA11yViolations();
	});
});
