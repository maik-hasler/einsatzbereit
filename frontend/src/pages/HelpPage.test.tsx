import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import HelpPage from "./HelpPage";
import { renderWithProviders } from "../test/render";

const SUPPORT_EMAIL = "hallo@einsatzbereit.maik-hasler.de";

/** Was the help cases of `HelpContactImprintContentTests` (#2061). */
describe("HelpPage", () => {
	it("reaches support through a mailto: link and promises no reply SLA", () => {
		renderWithProviders(<HelpPage />);

		expect(screen.getByRole("link", { name: SUPPORT_EMAIL })).toHaveAttribute(
			"href",
			`mailto:${SUPPORT_EMAIL}`,
		);
		expect(screen.queryByText(/24 hours/)).toBeNull();
	});

	it("keeps a split German compound word inside one link label", () => {
		// #2061: "Einsatzseite" was split across two links, so the first link's
		// entire accessible name was the fragment "Einsatz-".
		renderWithProviders(<HelpPage />, { lng: "de" });

		expect(
			screen.getByRole("link", { name: "Einsatzseite" }),
		).toBeInTheDocument();
		expect(screen.queryByRole("link", { name: "Einsatz-" })).toBeNull();
	});

	it("answers the four General questions the landing page's FAQ shows", async () => {
		// #2061: the landing FAQ used to answer four questions the Help
		// Center's FAQ never covered, breaking the "More questions? See Help"
		// link's whole premise. Both surfaces now read help.generalQ1..Q4, and
		// HomePage.test.tsx asserts the landing half of the same pairing.
		const { container } = renderWithProviders(<HelpPage />);

		expect(
			screen.getByRole("heading", { name: "General" }),
		).toBeInTheDocument();
		expect(
			screen.getByText("Does using Einsatzbereit cost anything?"),
		).toBeInTheDocument();

		// The accordion is native <details>/<summary>, so the answers are in
		// the DOM but collapsed - open one to prove it carries a real answer
		// rather than an empty panel.
		const details = container.querySelectorAll("details");
		expect(details.length).toBeGreaterThanOrEqual(4);
		await userEvent.click(
			screen.getByText("Does using Einsatzbereit cost anything?"),
		);
		expect(details[0].textContent?.length ?? 0).toBeGreaterThan(
			"Does using Einsatzbereit cost anything?".length,
		);
	});
});
