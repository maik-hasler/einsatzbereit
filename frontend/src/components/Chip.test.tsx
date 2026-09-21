import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Chip from "./Chip";
import { renderWithProviders } from "../test/render";

describe("Chip as a label", () => {
	it("renders its children inside a span", () => {
		const { container } = renderWithProviders(<Chip>Gardening</Chip>);

		const chip = container.querySelector("span");
		expect(chip).toHaveTextContent("Gardening");
	});

	it("carries no interactive affordance by default", () => {
		renderWithProviders(<Chip>Gardening</Chip>);

		expect(screen.queryByRole("link")).not.toBeInTheDocument();
		expect(screen.queryByRole("button")).not.toBeInTheDocument();
	});

	it("forwards arbitrary span attributes", () => {
		renderWithProviders(<Chip data-testid="tag">Gardening</Chip>);

		expect(screen.getByTestId("tag")).toHaveTextContent("Gardening");
	});
});

describe("Chip as a link", () => {
	it("renders an anchor pointing at `to`", () => {
		renderWithProviders(
			<Chip to="/opportunities?tag=gardening">Gardening</Chip>,
		);

		const link = screen.getByRole("link", { name: "Gardening" });
		expect(link).toHaveAttribute("href", "/opportunities?tag=gardening");
	});

	// A navigating chip is a pointer target, and `sm` alone renders 20px tall -
	// WCAG 2.5.8 puts the floor at 24x24 CSS px (#2327).
	it("meets the minimum target size when it navigates", () => {
		renderWithProviders(
			<Chip to="/opportunities" size="sm">
				Gardening
			</Chip>,
		);

		const classes = screen.getByRole("link", { name: "Gardening" }).className;
		expect(classes).toContain("min-h-6");
		expect(classes).toContain("min-w-6");
	});

	it("does not pay for the target-size floor when it is only a label", () => {
		const { container } = renderWithProviders(<Chip size="sm">Gardening</Chip>);

		expect(container.querySelector("span")?.className).not.toContain("min-h-6");
	});
});

describe("Chip as a removable filter", () => {
	it("renders a remove button labelled for screen readers", () => {
		renderWithProviders(
			<Chip onRemove={() => {}} removeLabel="Remove gardening filter">
				Gardening
			</Chip>,
		);

		expect(
			screen.getByRole("button", { name: "Remove gardening filter" }),
		).toBeInTheDocument();
	});

	it("calls onRemove when the remove button is pressed", async () => {
		const onRemove = vi.fn();
		renderWithProviders(
			<Chip onRemove={onRemove} removeLabel="Remove gardening filter">
				Gardening
			</Chip>,
		);

		await userEvent.click(
			screen.getByRole("button", { name: "Remove gardening filter" }),
		);

		expect(onRemove).toHaveBeenCalledTimes(1);
	});

	// A bare `<button>` inside a `<form>` submits it. Every removable chip in
	// this app sits in a filter bar that is itself a form.
	it("never submits a surrounding form", () => {
		renderWithProviders(
			<Chip onRemove={() => {}} removeLabel="Remove gardening filter">
				Gardening
			</Chip>,
		);

		expect(
			screen.getByRole("button", { name: "Remove gardening filter" }),
		).toHaveAttribute("type", "button");
	});
});

describe("Chip styling", () => {
	function classesOf(text: string, container: HTMLElement): string {
		const chip = [...container.querySelectorAll("span")].find(
			(node) => node.textContent === text,
		);
		return chip?.className ?? "";
	}

	it("uses the neutral tone and medium size by default", () => {
		const { container } = renderWithProviders(<Chip>Gardening</Chip>);

		const classes = classesOf("Gardening", container);
		expect(classes).toContain("bg-gray-100");
		expect(classes).toContain("px-3");
	});

	it.each([
		["brand", "bg-brand-50"],
		["success", "bg-green-50"],
		["warning", "bg-amber-50"],
		["danger", "bg-red-50"],
	] as const)("applies the %s tone", (tone, expectedClass) => {
		const { container } = renderWithProviders(
			<Chip tone={tone}>Gardening</Chip>,
		);

		expect(classesOf("Gardening", container)).toContain(expectedClass);
	});

	it("applies the small size", () => {
		const { container } = renderWithProviders(<Chip size="sm">Gardening</Chip>);

		expect(classesOf("Gardening", container)).toContain("px-2");
	});

	it("appends a caller's className rather than replacing the tone", () => {
		const { container } = renderWithProviders(
			<Chip className="ml-2">Gardening</Chip>,
		);

		const classes = classesOf("Gardening", container);
		expect(classes).toContain("ml-2");
		expect(classes).toContain("bg-gray-100");
	});

	it("leaves no double spaces when the optional class slots are empty", () => {
		const { container } = renderWithProviders(<Chip>Gardening</Chip>);

		expect(classesOf("Gardening", container)).not.toMatch(/\s{2,}/);
	});
});
