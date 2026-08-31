import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import QuickCheckInWidget from "./QuickCheckInWidget";
import { renderWithProviders } from "../../../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";

const opportunity = (
	id: string,
	titleDe: string,
	checkInMethod: "QRCode" | "PINCode" | "Manual" | "None",
) => ({
	id,
	titleDe,
	titleEn: undefined,
	checkInMethod,
	organizationId: ORG_ID,
	occurrence: "OneTime",
	participationType: "ScheduledSlots",
	status: "Published",
	isRemote: true,
	createdOn: new Date(Date.UTC(2026, 7, 1)),
	totalMaxParticipants: 5,
	currentParticipantCount: 0,
});

beforeEach(() => {
	api.__reset();
});

function renderWidget() {
	return renderWithProviders(
		<QuickCheckInWidget
			organizationId={ORG_ID}
			refreshKey={0}
			onOpportunityCreated={() => {}}
		/>,
		{ auth: { isAuthenticated: true } },
	);
}

describe("QuickCheckInWidget", () => {
	it("offers every check-in method the organizer can act on", async () => {
		api.getOrganizationOpportunities.mockResolvedValue({
			items: [
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000001",
					"Scan me",
					"QRCode",
				),
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000002",
					"Type a PIN",
					"PINCode",
				),
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000003",
					"Tick them off",
					"Manual",
				),
			],
			pageCount: 1,
			totalCount: 3,
		});

		renderWidget();

		await userEvent.click(await screen.findByRole("combobox"));

		expect(screen.getAllByRole("option").map((o) => o.textContent)).toEqual([
			"Scan me",
			"Type a PIN",
			"Tick them off",
		]);
		expect(screen.getByTestId("quick-checkin-scan-btn")).toBeEnabled();
	});

	// The scanner can only finish a QR check-in. PIN and manual both need the
	// opportunity's own sign-up list, so the action becomes a link there
	// rather than a scanner that would do nothing useful (#2322 F6).
	it("swaps the scanner for a link to the sign-ups on a PIN opportunity", async () => {
		api.getOrganizationOpportunities.mockResolvedValue({
			items: [
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000002",
					"Type a PIN",
					"PINCode",
				),
			],
			pageCount: 1,
			totalCount: 1,
		});

		renderWidget();

		const open = await screen.findByTestId("quick-checkin-open-btn");
		expect(open).toHaveAttribute(
			"href",
			`/app/${ORG_ID}/dashboard/opportunities/aaaaaaaa-0000-0000-0000-000000000002/engagements`,
		);
		expect(screen.queryByTestId("quick-checkin-scan-btn")).toBeNull();
	});

	it("keeps the scanner for the QR opportunity the organizer picks", async () => {
		api.getOrganizationOpportunities.mockResolvedValue({
			items: [
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000002",
					"Type a PIN",
					"PINCode",
				),
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000001",
					"Scan me",
					"QRCode",
				),
			],
			pageCount: 1,
			totalCount: 2,
		});

		renderWidget();

		await userEvent.click(await screen.findByRole("combobox"));
		await userEvent.click(screen.getByRole("option", { name: "Scan me" }));

		expect(screen.getByTestId("quick-checkin-scan-btn")).toBeEnabled();
		expect(screen.queryByTestId("quick-checkin-open-btn")).toBeNull();
	});

	it("shows the empty state only when no published opportunity has check-in at all", async () => {
		api.getOrganizationOpportunities.mockResolvedValue({
			items: [
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000004",
					"Nothing to check",
					"None",
				),
			],
			pageCount: 1,
			totalCount: 1,
		});

		renderWidget();

		expect(
			await screen.findByText(
				"No published opportunities with check-in enabled yet.",
			),
		).toBeInTheDocument();
		expect(screen.queryByRole("combobox")).toBeNull();
		expect(screen.queryByTestId("quick-checkin-scan-btn")).toBeNull();
		expect(screen.queryByTestId("quick-checkin-open-btn")).toBeNull();
	});
});
