import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";

async function freshLockScroll() {
	const mod = await import("./scrollLock");
	return mod.lockScroll;
}

function setViewport(innerWidth: number, clientWidth: number) {
	window.innerWidth = innerWidth;
	Object.defineProperty(document.documentElement, "clientWidth", {
		value: clientWidth,
		configurable: true,
	});
}

describe("lockScroll", () => {
	beforeEach(() => {
		vi.resetModules();
		document.documentElement.style.cssText = "";
		document.body.style.cssText = "";
		setViewport(1024, 1024);
	});

	afterEach(() => {
		document.documentElement.style.cssText = "";
		document.body.style.cssText = "";
	});

	it("locks the root element, not the body", async () => {
		const lockScroll = await freshLockScroll();
		lockScroll();

		expect(document.documentElement.style.overflow).toBe("hidden");
		expect(document.body.style.overflow).toBe("");
	});

	it("restores the root element's previous inline overflow on release", async () => {
		const lockScroll = await freshLockScroll();
		const release = lockScroll();
		release();

		expect(document.documentElement.style.overflow).toBe("");
	});

	it("restores a pre-existing inline overflow value rather than clearing it", async () => {
		document.documentElement.style.overflow = "auto";
		const lockScroll = await freshLockScroll();
		const release = lockScroll();
		expect(document.documentElement.style.overflow).toBe("hidden");

		release();
		expect(document.documentElement.style.overflow).toBe("auto");
	});

	it("stays locked while an outer holder is still open (nested dialogs)", async () => {
		const lockScroll = await freshLockScroll();
		const releaseOuter = lockScroll();
		const releaseInner = lockScroll();

		releaseInner();
		expect(document.documentElement.style.overflow).toBe("hidden");

		releaseOuter();
		expect(document.documentElement.style.overflow).toBe("");
	});

	it("unlocks regardless of the order holders release in", async () => {
		const lockScroll = await freshLockScroll();
		const releaseOuter = lockScroll();
		const releaseInner = lockScroll();

		releaseOuter();
		expect(document.documentElement.style.overflow).toBe("hidden");

		releaseInner();
		expect(document.documentElement.style.overflow).toBe("");
	});

	it("ignores a release called more than once", async () => {
		const lockScroll = await freshLockScroll();
		const releaseFirst = lockScroll();
		const releaseSecond = lockScroll();

		releaseFirst();
		releaseFirst();

		expect(document.documentElement.style.overflow).toBe("hidden");

		releaseSecond();
		expect(document.documentElement.style.overflow).toBe("");
	});

	it("holds the width of the scrollbar it hid as body padding", async () => {
		setViewport(1024, 1009);
		const lockScroll = await freshLockScroll();
		const release = lockScroll();

		expect(document.body.style.paddingRight).toBe("15px");

		release();
		expect(document.body.style.paddingRight).toBe("");
	});

	it("adds the scrollbar width on top of the body's existing padding", async () => {
		setViewport(1024, 1009);
		document.body.style.paddingRight = "8px";
		const lockScroll = await freshLockScroll();
		const release = lockScroll();

		expect(document.body.style.paddingRight).toBe("23px");

		release();
		expect(document.body.style.paddingRight).toBe("8px");
	});

	it("leaves body padding alone where scrollbars are overlaid", async () => {
		setViewport(1024, 1024);
		const lockScroll = await freshLockScroll();
		const release = lockScroll();

		expect(document.body.style.paddingRight).toBe("");

		release();
		expect(document.body.style.paddingRight).toBe("");
	});

	it("re-locks cleanly after a full release cycle", async () => {
		const lockScroll = await freshLockScroll();
		lockScroll()();

		const release = lockScroll();
		expect(document.documentElement.style.overflow).toBe("hidden");

		release();
		expect(document.documentElement.style.overflow).toBe("");
	});
});
