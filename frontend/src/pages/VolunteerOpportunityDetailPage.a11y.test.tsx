import { describe, it, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import { Route, Routes } from "react-router";
import VolunteerOpportunityDetailPage from "./VolunteerOpportunityDetailPage";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const OPPORTUNITY_ID = "11111111-1111-1111-1111-111111111111";
const ORGANIZATION_ID = "22222222-2222-2222-2222-222222222222";
const SLOT_ID = "aaaaaaaa-0000-0000-0000-000000000001";

function renderDetail(auth?: { isAuthenticated: boolean }) {
	return renderWithProviders(
		<Routes>
			<Route
				path="/volunteer-opportunities/:opportunityId"
				element={<VolunteerOpportunityDetailPage />}
			/>
		</Routes>,
		{
			route: `/volunteer-opportunities/${OPPORTUNITY_ID}`,
			...(auth ? { auth } : {}),
		},
	);
}

beforeEach(() => {
	api.__reset();
	api.getVolunteerOpportunities.mockResolvedValue({
		items: [],
		pageCount: 1,
		totalCount: 0,
	});
});

describe("VolunteerOpportunityDetailPage a11y", () => {
	it("has no violations when coordinates are missing and the address fallback renders", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			id: OPPORTUNITY_ID,
			organizationId: "22222222-2222-2222-2222-222222222222",
			organizationName: "Bilingual Org",
			titleDe: "Deutscher Titel",
			titleEn: "English Title",
			descriptionDe: "Deutsche Beschreibung.",
			descriptionEn: "English description.",
			street: "Teststrasse",
			houseNumber: "1",
			zipCode: "24103",
			city: "Kiel",
			isRemote: false,
			latitude: undefined,
			longitude: undefined,
			occurrence: "OneTime",
			participationType: "IndividualContact",
			checkInMethod: "None",
			status: "Published",
			timeSlots: [],
			tags: [],
			currentUserEngagements: [],
			validUntil: undefined,
			createdOn: new Date(Date.UTC(2026, 7, 1, 9, 0)),
		});

		renderDetail();

		await screen.findByTestId("opportunity-address");
		await expectNoA11yViolations();
	});

	it("has no violations with a described opportunity, an action panel and past slots (#2330)", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			id: OPPORTUNITY_ID,
			organizationId: "22222222-2222-2222-2222-222222222222",
			organizationName: "Bilingual Org",
			titleDe: "Deutscher Titel",
			titleEn: "English Title",
			descriptionDe: "Absatz eins.\n\nAbsatz zwei.",
			descriptionEn: "Paragraph one.\n\nParagraph two.",
			street: "Teststrasse",
			houseNumber: "1",
			zipCode: "24103",
			city: "Kiel",
			isRemote: true,
			occurrence: "OneTime",
			participationType: "ScheduledSlots",
			checkInMethod: "None",
			status: "Published",
			bannerImageUrl: "https://storage.test/banner.jpg",
			timeSlots: [
				{
					id: "aaaaaaaa-0000-0000-0000-000000000001",
					startDateTime: new Date(Date.UTC(2027, 0, 14, 9, 0)),
					endDateTime: new Date(Date.UTC(2027, 0, 14, 12, 0)),
					maxParticipants: 5,
					bookedCount: 0,
				},
				{
					id: "aaaaaaaa-0000-0000-0000-000000000002",
					startDateTime: new Date(Date.UTC(2020, 0, 14, 9, 0)),
					endDateTime: new Date(Date.UTC(2020, 0, 14, 12, 0)),
					maxParticipants: 5,
					bookedCount: 0,
				},
			],
			tags: [],
			currentUserEngagements: [],
			validUntil: undefined,
			createdOn: new Date(Date.UTC(2026, 7, 1, 9, 0)),
		});

		renderDetail();

		await screen.findByTestId("opportunity-description");
		await expectNoA11yViolations();
	});

	// The signed-out cases above render the sign-in prompt, so nothing scanned
	// the states that share the rail with it: the sign-up card with a withdraw
	// button per engagement, and the organizer card's two-column contact grid.
	it("has no violations for a signed-in volunteer with a sign-up and an organizer card", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			id: OPPORTUNITY_ID,
			organizationId: ORGANIZATION_ID,
			organizationName: "Bilingual Org",
			titleDe: "Deutscher Titel",
			titleEn: "English Title",
			descriptionDe: "Absatz eins.\n\nAbsatz zwei.",
			descriptionEn: "Paragraph one.\n\nParagraph two.",
			street: "Teststrasse",
			houseNumber: "1",
			zipCode: "24103",
			city: "Kiel",
			isRemote: false,
			latitude: undefined,
			longitude: undefined,
			occurrence: "OneTime",
			participationType: "ScheduledSlots",
			checkInMethod: "None",
			status: "Published",
			timeSlots: [
				{
					id: SLOT_ID,
					startDateTime: new Date(Date.UTC(2027, 0, 14, 9, 0)),
					endDateTime: new Date(Date.UTC(2027, 0, 14, 12, 0)),
					maxParticipants: 5,
					bookedCount: 1,
				},
			],
			tags: ["Kiel"],
			currentUserEngagements: [
				{
					id: "cccccccc-0000-0000-0000-000000000001",
					timeSlotId: SLOT_ID,
					status: "Pending",
					isCheckedIn: false,
					remainingReactivations: 2,
				},
			],
			validUntil: undefined,
			createdOn: new Date(Date.UTC(2026, 7, 1, 9, 0)),
		});
		api.getPublicOrganizationProfile.mockResolvedValue({
			id: ORGANIZATION_ID,
			name: "Bilingual Org",
			description: "Wir helfen in Kiel.",
			contactEmail: "kontakt@example.org",
			contactPhone: "+49 431 1234567",
			website: "https://example.org",
			address: {
				street: "Teststrasse",
				houseNumber: "1",
				zipCode: "24103",
				city: "Kiel",
			},
			openOpportunities: [],
		});

		renderDetail({ isAuthenticated: true });

		await screen.findByTestId("application-status");
		await screen.findByTestId("about-organization");
		await expectNoA11yViolations();
	});
});
