import { describe, it, expect, vi, afterEach } from "vitest";
import { screen, fireEvent, cleanup } from "@testing-library/react";
import ConfigGate from "./ConfigGate";
import { renderWithProviders } from "../test/render";

const { mockRuntimeConfig } = vi.hoisted(() => ({
	mockRuntimeConfig: { isConfigured: true },
}));

vi.mock("../lib/runtimeConfig", () => ({ runtimeConfig: mockRuntimeConfig }));

function setNavigatorOnLine(value: boolean) {
	Object.defineProperty(navigator, "onLine", {
		configurable: true,
		get: () => value,
	});
}

function renderGate() {
	return renderWithProviders(
		<ConfigGate>
			<p>protected content</p>
		</ConfigGate>,
	);
}

describe("ConfigGate", () => {
	afterEach(() => {
		mockRuntimeConfig.isConfigured = true;
		setNavigatorOnLine(true);
		vi.unstubAllGlobals();
		cleanup();
	});

	it("renders its children when the runtime config is valid", () => {
		mockRuntimeConfig.isConfigured = true;

		renderGate();

		expect(screen.getByText("protected content")).toBeInTheDocument();
	});

	it("shows a configuration-missing screen instead of the app when unconfigured", () => {
		mockRuntimeConfig.isConfigured = false;

		renderGate();

		expect(screen.queryByText("protected content")).not.toBeInTheDocument();
		expect(
			screen.getByRole("heading", { name: "Configuration missing" }),
		).toBeInTheDocument();
	});

	it("reloads once the browser comes back online while unconfigured", () => {
		mockRuntimeConfig.isConfigured = false;
		const reload = vi.fn();
		vi.stubGlobal("location", { ...window.location, reload });

		renderGate();
		setNavigatorOnLine(true);
		window.dispatchEvent(new Event("online"));

		expect(reload).toHaveBeenCalledTimes(1);
	});

	it("does not reload on an offline event", () => {
		mockRuntimeConfig.isConfigured = false;
		const reload = vi.fn();
		vi.stubGlobal("location", { ...window.location, reload });

		renderGate();
		setNavigatorOnLine(false);
		window.dispatchEvent(new Event("offline"));

		expect(reload).not.toHaveBeenCalled();
	});

	it("does not reload on reconnect once already configured", () => {
		mockRuntimeConfig.isConfigured = true;
		const reload = vi.fn();
		vi.stubGlobal("location", { ...window.location, reload });

		renderGate();
		setNavigatorOnLine(true);
		window.dispatchEvent(new Event("online"));

		expect(reload).not.toHaveBeenCalled();
	});

	it("reloads the page when Reload is clicked", () => {
		mockRuntimeConfig.isConfigured = false;
		const reload = vi.fn();
		vi.stubGlobal("location", { ...window.location, reload });

		renderGate();
		fireEvent.click(screen.getByRole("button", { name: "Reload" }));

		expect(reload).toHaveBeenCalledTimes(1);
	});
});
