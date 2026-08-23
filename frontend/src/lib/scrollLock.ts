let lockCount = 0;
let restore: (() => void) | null = null;

export function lockScroll(): () => void {
	if (lockCount === 0) {
		const root = document.documentElement;
		const body = document.body;
		const previousOverflow = root.style.overflow;
		const previousPaddingRight = body.style.paddingRight;

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
