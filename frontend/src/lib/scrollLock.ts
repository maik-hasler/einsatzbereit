// Background scroll lock for blocking overlays (Modal, MobileMenu).
//
// Locking `document.body` - the obvious approach, and the one MobileMenu used
// before #1787 - does not work in this app. A UA only propagates the body's
// overflow to the viewport while the *root* element's own overflow computes to
// `visible`, and `global.css` sets `html { overflow-x: clip }`. That clip stops
// the propagation, so `document.body.style.overflow = "hidden"` merely clips
// the body box while the viewport keeps scrolling underneath it. Measured in a
// real browser with the mobile menu open and its body lock applied: the page
// still scrolled 0 -> 1896 under a mouse wheel. Locking the root element is
// what actually holds, and it leaves the current scroll offset untouched, so
// the position is still there when the overlay closes.
//
// Reference-counted rather than per-overlay, because overlays nest: the create
// -opportunity wizard keeps its own Modal mounted while a discard/delete
// confirm Modal opens on top (see `suspended` in Modal.tsx). With a naive
// capture-and-restore per instance, whichever one unmounts first would hand
// the page back its scrollbar while the other dialog is still open, and React
// tears parents down before children, so closing both at once could just as
// easily leave the lock stuck on forever.

let lockCount = 0;
let restore: (() => void) | null = null;

/**
 * Locks background scrolling for as long as the caller holds the returned
 * release function. Safe to nest; the page is only handed back once the last
 * holder releases. Calling a release more than once is a no-op.
 */
export function lockScroll(): () => void {
	if (lockCount === 0) {
		const root = document.documentElement;
		const body = document.body;
		const previousOverflow = root.style.overflow;
		const previousPaddingRight = body.style.paddingRight;

		// Hiding the root's scrollbar widens the viewport by exactly the width
		// that scrollbar occupied, which would shove every centered container
		// (and the sticky header) sideways the instant a dialog opens, then back
		// again on close. Holding that width as body padding keeps the page
		// behind the backdrop still. No-op where scrollbars are overlaid rather
		// than laid out - most touch devices, macOS, and headless Chromium,
		// which is why no automated test here observes a non-zero value.
		const scrollbarWidth = window.innerWidth - root.clientWidth;
		if (scrollbarWidth > 0) {
			const currentPadding =
				parseFloat(getComputedStyle(body).paddingRight) || 0;
			body.style.paddingRight = `${currentPadding + scrollbarWidth}px`;
		}
		root.style.overflow = "hidden";

		restore = () => {
			root.style.overflow = previousOverflow;
			body.style.paddingRight = previousPaddingRight;
		};
	}
	lockCount++;

	let released = false;
	return () => {
		if (released) return;
		released = true;
		lockCount--;
		if (lockCount === 0) {
			restore?.();
			restore = null;
		}
	};
}
