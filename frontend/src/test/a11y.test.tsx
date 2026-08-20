import { describe, it, expect } from "vitest";
import { render } from "@testing-library/react";
import { expectNoA11yViolations } from "./a11y";

/**
 * The harness's own negative control.
 *
 * Every other `*.a11y.test.tsx` in this repo asserts that axe found nothing.
 * A suite of those is indistinguishable from a suite where the scanner is
 * quietly broken - a jsdom API axe needs going missing, a rule list widened
 * one entry too far, `axe()` handed a detached node. This file is the check
 * that the gate can still fail, and it is the first thing to look at if the
 * a11y suite ever goes green through a change that should have broken it.
 */
describe("expectNoA11yViolations", () => {
	it("passes on markup with nothing wrong", async () => {
		render(
			<div>
				<label htmlFor="name">Name</label>
				<input id="name" />
			</div>,
		);
		await expectNoA11yViolations();
	});

	it("catches a critical violation (image with no alt text)", async () => {
		// eslint-disable-next-line jsx-a11y/alt-text -- the defect under test
		render(<img src="/banner.png" />);
		await expect(expectNoA11yViolations()).rejects.toThrow(/image-alt/);
	});

	it("catches a serious violation (input with no label)", async () => {
		render(<input type="text" />);
		await expect(expectNoA11yViolations()).rejects.toThrow(/label/);
	});

	it("catches an aria-hidden element that is still in the tab order", async () => {
		// The failure mode the backdrop-button modal pattern exists to avoid,
		// and the reason setup.ts stubs document.elementsFromPoint - without
		// that stub axe downgrades this rule to "incomplete" and this passes.
		render(
			<button type="button" aria-hidden="true">
				Close
			</button>,
		);
		await expect(expectNoA11yViolations()).rejects.toThrow(/aria-hidden-focus/);
	});

	it("catches a listbox option that wraps an interactive control", async () => {
		// frontend/AGENTS.md's dropdown convention, in its #1772 failure shape:
		// role="option" on an <li> that wraps a <button>.
		render(
			<ul role="listbox" aria-label="Time slots">
				<li role="option" aria-selected="false">
					<button type="button">09:00</button>
				</li>
			</ul>,
		);
		await expect(expectNoA11yViolations()).rejects.toThrow(
			/nested-interactive/,
		);
	});

	it("scans only the element it is given", async () => {
		const { container } = render(
			<div>
				<p>fine</p>
			</div>,
		);
		document.body.insertAdjacentHTML("beforeend", '<img src="/rogue.png">');
		await expectNoA11yViolations(container);
		await expect(expectNoA11yViolations(document.body)).rejects.toThrow(
			/image-alt/,
		);
	});
});

describe("expectNoA11yViolations guards", () => {
	it("refuses to pass on an empty subtree", async () => {
		const { container } = render(<></>);
		await expect(expectNoA11yViolations(container)).rejects.toThrow(
			/empty subtree/,
		);
	});
});
