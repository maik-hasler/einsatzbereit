import {
	describe,
	it,
	expect,
	vi,
	beforeAll,
	beforeEach,
	afterEach,
	type Mock,
} from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18next from "i18next";
import en from "../locales/en.json";
import ErrorBoundary from "./ErrorBoundary";
import { renderWithProviders } from "../test/render";

// ErrorBoundary is a class component and so reads the *global* i18next
// singleton rather than the per-test instance renderWithProviders installs -
// it has no hook to read a provider with. Initialising the singleton here is
// what makes the assertions below read real copy instead of raw key strings.
beforeAll(async () => {
	if (!i18next.isInitialized) {
		await i18next.init({
			lng: "en",
			fallbackLng: "en",
			resources: { en },
			interpolation: { escapeValue: false },
			returnNull: false,
			initAsync: false,
		});
	}
	await i18next.changeLanguage("en");
});

const DYNAMIC_IMPORT_ERROR =
	"Failed to fetch dynamically imported module: /assets/OpportunitiesPage-a1b2c3.js";

function Boom({ message }: { message: string }): never {
	throw new Error(message);
}

function setNavigatorOnLine(value: boolean) {
	Object.defineProperty(navigator, "onLine", {
		configurable: true,
		get: () => value,
	});
}

let reload: Mock<() => void>;
let back: Mock<() => void>;
let originalLocation: Location;

beforeEach(() => {
	// React re-throws into console.error for every caught boundary error, and
	// ErrorBoundary.componentDidCatch logs one of its own. Neither is the
	// subject of these tests; leaving them on buries the real output.
	vi.spyOn(console, "error").mockImplementation(() => {});

	reload = vi.fn<() => void>();
	originalLocation = window.location;
	Object.defineProperty(window, "location", {
		configurable: true,
		value: { ...originalLocation, reload },
	});

	back = vi.fn<() => void>();
	vi.spyOn(window.history, "back").mockImplementation(back);
});

afterEach(() => {
	Object.defineProperty(window, "location", {
		configurable: true,
		value: originalLocation,
	});
	setNavigatorOnLine(true);
	vi.restoreAllMocks();
});

describe("ErrorBoundary with nothing to catch", () => {
	it("renders its children untouched", () => {
		renderWithProviders(
			<ErrorBoundary>
				<p>All good</p>
			</ErrorBoundary>,
		);

		expect(screen.getByText("All good")).toBeInTheDocument();
	});
});

describe("ErrorBoundary catching a render error", () => {
	it("replaces the subtree with the generic error state", () => {
		renderWithProviders(
			<ErrorBoundary>
				<Boom message="kaboom" />
			</ErrorBoundary>,
		);

		expect(
			screen.getByRole("heading", { name: en.translation.error.boundaryTitle }),
		).toBeInTheDocument();
		expect(
			screen.getByText(en.translation.error.boundaryMessage),
		).toBeInTheDocument();
	});

	it("logs the error so it survives in the console", () => {
		renderWithProviders(
			<ErrorBoundary>
				<Boom message="kaboom" />
			</ErrorBoundary>,
		);

		expect(console.error).toHaveBeenCalledWith(
			"[ErrorBoundary] Uncaught error:",
			expect.objectContaining({ message: "kaboom" }),
			expect.any(String),
		);
	});

	it("prefers a caller-supplied fallback over the generic state", () => {
		renderWithProviders(
			<ErrorBoundary fallback={<p>Could not load the widget.</p>}>
				<Boom message="kaboom" />
			</ErrorBoundary>,
		);

		expect(screen.getByText("Could not load the widget.")).toBeInTheDocument();
		expect(
			screen.queryByText(en.translation.error.boundaryMessage),
		).not.toBeInTheDocument();
	});

	it("reloads the page from the reload action", async () => {
		renderWithProviders(
			<ErrorBoundary>
				<Boom message="kaboom" />
			</ErrorBoundary>,
		);

		await userEvent.click(
			screen.getByRole("button", { name: en.translation.error.reload }),
		);

		expect(reload).toHaveBeenCalledTimes(1);
	});

	it("goes back in history from the back action", async () => {
		renderWithProviders(
			<ErrorBoundary>
				<Boom message="kaboom" />
			</ErrorBoundary>,
		);

		await userEvent.click(
			screen.getByRole("button", { name: en.translation.error.goBack }),
		);

		expect(back).toHaveBeenCalledTimes(1);
	});
});

describe("ErrorBoundary catching a chunk that could not be fetched", () => {
	it("blames the connection when the device is offline", () => {
		setNavigatorOnLine(false);

		renderWithProviders(
			<ErrorBoundary>
				<Boom message={DYNAMIC_IMPORT_ERROR} />
			</ErrorBoundary>,
		);

		expect(
			screen.getByRole("heading", {
				name: en.translation.routeState.offline.title,
			}),
		).toBeInTheDocument();
		expect(
			screen.queryByText(en.translation.error.boundaryMessage),
		).not.toBeInTheDocument();
	});

	it("still shows the generic state offline when the error is not a failed chunk", () => {
		setNavigatorOnLine(false);

		renderWithProviders(
			<ErrorBoundary>
				<Boom message="kaboom" />
			</ErrorBoundary>,
		);

		expect(
			screen.getByText(en.translation.error.boundaryMessage),
		).toBeInTheDocument();
	});

	// The chunk is only missing because the network was gone; once it is back
	// the page can simply be re-fetched, so the user never has to act.
	it("reloads by itself once the connection returns", () => {
		setNavigatorOnLine(false);

		renderWithProviders(
			<ErrorBoundary>
				<Boom message={DYNAMIC_IMPORT_ERROR} />
			</ErrorBoundary>,
		);
		expect(reload).not.toHaveBeenCalled();

		setNavigatorOnLine(true);
		window.dispatchEvent(new Event("online"));

		expect(reload).toHaveBeenCalledTimes(1);
	});

	it("offers a manual retry that reloads the page", async () => {
		setNavigatorOnLine(false);

		renderWithProviders(
			<ErrorBoundary>
				<Boom message={DYNAMIC_IMPORT_ERROR} />
			</ErrorBoundary>,
		);

		await userEvent.click(
			screen.getByRole("button", { name: en.translation.orgApp.retry }),
		);

		expect(reload).toHaveBeenCalledTimes(1);
	});

	it("does not reload by itself for an ordinary error", () => {
		setNavigatorOnLine(false);

		renderWithProviders(
			<ErrorBoundary>
				<Boom message="kaboom" />
			</ErrorBoundary>,
		);

		setNavigatorOnLine(true);
		window.dispatchEvent(new Event("online"));

		expect(reload).not.toHaveBeenCalled();
	});

	it("does not reload while the device stays offline", () => {
		setNavigatorOnLine(false);

		renderWithProviders(
			<ErrorBoundary>
				<Boom message={DYNAMIC_IMPORT_ERROR} />
			</ErrorBoundary>,
		);

		window.dispatchEvent(new Event("offline"));

		expect(reload).not.toHaveBeenCalled();
	});

	it("stops listening for connectivity once unmounted", () => {
		setNavigatorOnLine(false);

		const { unmount } = renderWithProviders(
			<ErrorBoundary>
				<Boom message={DYNAMIC_IMPORT_ERROR} />
			</ErrorBoundary>,
		);

		unmount();
		setNavigatorOnLine(true);
		window.dispatchEvent(new Event("online"));

		expect(reload).not.toHaveBeenCalled();
	});
});
