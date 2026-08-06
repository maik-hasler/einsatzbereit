// Focus-visible styling comes from the global :focus-visible ring in
// global.css (issue #992), not from a low-contrast focus:ring-*/outline-none
// pair here - see Button.tsx's BASE_CLASSES for the same reasoning.

// Shared surface recipe (border/radius/background/shadow/focus) for text
// inputs and select-like triggers alike - kept separate from inputClass's
// own `block`/`mt-1` layout classes so a flex-based trigger (e.g. Dropdown)
// can reuse the same look without a block/flex classname contradiction
// (einsatzbereit#1104).
// min-h-10 (not a fixed h-10) gives text inputs and native <select>s the
// same floor height regardless of each browser's own select chrome (which
// otherwise renders a couple pixels taller than a same-padded input) - see
// Button.tsx's md size for the matching half of this fix (issue #1673:
// three different control heights staggered in one filter row).
export const inputSurfaceClass =
	"min-h-10 w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400";

export const inputClass = `mt-1 block ${inputSurfaceClass} text-gray-900`;

export const textareaClass = `${inputClass} resize-y`;

export const labelClass = "block text-xs font-medium text-gray-600";
