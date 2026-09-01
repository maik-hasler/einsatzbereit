import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import OpportunityActionPanel, {
	type PanelFact,
} from "./OpportunityActionPanel";
import { renderWithProviders } from "../test/render";
import { CalendarIcon, MapPinIcon } from "./icons";

const facts: PanelFact[] = [
	{
		key: "when",
		icon: <CalendarIcon className="h-4 w-4" />,
		label: "When",
		value: "27.08.2026, 09:00",
		"data-testid": "fact-when",
	},
	{
		key: "where",
		icon: <MapPinIcon className="h-4 w-4" />,
		label: "Where",
		value: "Kiel",
	},
];

describe("OpportunityActionPanel", () => {
	it("pairs every fact as a dt/dd one level below the dl", () => {
		const { container } = renderWithProviders(
			<OpportunityActionPanel
				status={{ label: "3 spots left", tone: "open" }}
				facts={facts}
			/>,
		);

		expect(screen.getByTestId("fact-when")).toHaveTextContent(
			"27.08.2026, 09:00",
		);
		// axe's `dlitem` rule fails the moment a wrapper column pushes these
		// deeper, and jsdom has no layout to make that visible otherwise.
		const list = container.querySelector("dl");
		for (const term of container.querySelectorAll("dt, dd")) {
			expect(term.parentElement?.parentElement).toBe(list);
		}
	});

	it("carries the status label in the strip, with the note under it", () => {
		renderWithProviders(
			<OpportunityActionPanel
				status={{
					label: "By expression of interest",
					tone: "open",
					note: "4 people have already joined",
				}}
				facts={facts}
			/>,
		);

		expect(screen.getByTestId("opportunity-capacity")).toHaveTextContent(
			"By expression of interest",
		);
		expect(
			screen.getByTestId("opportunity-capacity-secondary"),
		).toHaveTextContent("4 people have already joined");
	});

	it("leaves out the note when there is none to make", () => {
		renderWithProviders(
			<OpportunityActionPanel
				status={{ label: "Full", tone: "closed" }}
				facts={facts}
			/>,
		);

		expect(screen.queryByTestId("opportunity-capacity-secondary")).toBeNull();
	});

	it.each([
		["open", "bg-brand-700"],
		["urgent", "bg-amber-100"],
		["closed", "bg-gray-100"],
	] as const)("paints the %s strip with %s", (tone, expected) => {
		renderWithProviders(
			<OpportunityActionPanel
				status={{ label: "Status", tone }}
				facts={facts}
			/>,
		);

		expect(screen.getByTestId("opportunity-capacity")).toHaveClass(expected);
	});

	it("renders no action block for a visitor with nothing to do", () => {
		const { container } = renderWithProviders(
			<OpportunityActionPanel
				status={{ label: "Full", tone: "closed" }}
				facts={facts}
			/>,
		);

		// An owner viewing their own draft gets neither a CTA nor a calendar
		// entry - the card must not end in an empty padded strip.
		expect(container.querySelectorAll("dl ~ div")).toHaveLength(0);
	});

	it("renders no action block when every conditional slot is false", () => {
		const showCta = false;
		const showSignIn = false;

		const { container } = renderWithProviders(
			<OpportunityActionPanel
				status={{ label: "No open spots", tone: "closed" }}
				facts={facts}
			>
				{showCta && <button type="button">Sign up</button>}
				{showSignIn && <button type="button">Sign in</button>}
			</OpportunityActionPanel>,
		);

		// This is the page's real call shape: React hands two `false` slots over
		// as an array, which a plain `children &&` check reads as truthy.
		expect(container.querySelectorAll("dl ~ div")).toHaveLength(0);
	});

	it("keeps the call to action and the secondary footer apart", () => {
		renderWithProviders(
			<OpportunityActionPanel
				status={{ label: "3 spots left", tone: "open" }}
				facts={facts}
				footer={<button type="button">Add to calendar</button>}
			>
				<button type="button">Sign up</button>
			</OpportunityActionPanel>,
		);

		const cta = screen.getByRole("button", { name: "Sign up" });
		const footer = screen.getByRole("button", { name: "Add to calendar" });
		expect(cta.parentElement).not.toBe(footer.parentElement);
	});
});
