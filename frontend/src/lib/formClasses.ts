// The one border/focus recipe every form control's invalid state must share
// (#2239) - a single source of truth so a red border on error never drifts
// between the wizard's floating fields and every other form's fields.
export function fieldBorderClass(hasError: boolean): string {
	return hasError
		? "border-red-300 focus:border-red-400"
		: "border-gray-200 focus:border-brand-400";
}

export function getInputSurfaceClass(hasError = false): string {
	return `min-h-10 w-full rounded-xl border ${fieldBorderClass(hasError)} bg-white px-3 py-2 text-sm shadow-sm transition`;
}

export function getInputClass(hasError = false): string {
	return `mt-1 block ${getInputSurfaceClass(hasError)} text-gray-900`;
}

export function getTextareaClass(hasError = false): string {
	return `${getInputClass(hasError)} resize-y`;
}

export function getLabelClass(hasError = false): string {
	return hasError
		? "block text-xs font-medium text-red-600"
		: "block text-xs font-medium text-gray-600";
}

export const inputSurfaceClass = getInputSurfaceClass();

export const inputClass = getInputClass();

export const textareaClass = getTextareaClass();

// No background-image chevron here (#2225) - the deployed CSP's img-src has no
// `data:`, which silently strips a CSS-painted arrow while appearance-none has
// already removed the native one. `components/Select.tsx` draws the chevron as
// an inline <svg> instead, so every consumer should render through that
// component rather than applying this class to a bare <select>.
export const selectClass = `${inputClass} appearance-none pr-9`;

export const labelClass = getLabelClass();

// A native checkbox takes its checked fill from `accent-color`, and from
// nothing else: `text-brand-*` sets a text colour the control never reads, so
// four of the eight checkboxes in the app painted the browser default blue
// against the green brand while the other four painted brand green (#2329 F8).
// `border-*` is just as inert on a control the UA paints itself. One class, so
// the two treatments cannot drift apart again.
export const checkboxClass = "shrink-0 accent-brand-600";
